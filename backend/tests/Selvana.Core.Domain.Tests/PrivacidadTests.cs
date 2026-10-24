using FluentAssertions;
using Selvana.Core.Domain.Auditoria;
using Selvana.Core.Domain.Privacidad;

namespace Selvana.Core.Domain.Tests;

public sealed class CalendarioColombiaTests
{
    [Fact]
    public void LosFestivosDe2026CoincidenConElCalendarioOficial()
    {
        DateOnly[] esperados =
        [
            new(2026, 1, 1), new(2026, 1, 12), new(2026, 3, 23), new(2026, 4, 2), new(2026, 4, 3), new(2026, 5, 1), new(2026, 5, 18),
            new(2026, 6, 8), new(2026, 6, 15), new(2026, 6, 29), new(2026, 7, 20), new(2026, 8, 7), new(2026, 8, 17), new(2026, 10, 12),
            new(2026, 11, 2), new(2026, 11, 16), new(2026, 12, 8), new(2026, 12, 25),
        ];

        CalendarioColombia.Festivos(2026).Should().BeEquivalentTo(esperados);
    }

    [Fact]
    public void UnaConsultaRecibidaElJueves22DeOctubreVenceEl6DeNoviembre()
    {
        // 10 días hábiles desde el día siguiente; el lunes 2 de noviembre es festivo
        CalendarioColombia.SumarDiasHabiles(new DateOnly(2026, 10, 22), 10).Should().Be(new DateOnly(2026, 11, 6));
    }

    [Fact]
    public void UnReclamoRecibidoElJueves22DeOctubreVenceEl13DeNoviembre()
    {
        CalendarioColombia.SumarDiasHabiles(new DateOnly(2026, 10, 22), 15).Should().Be(new DateOnly(2026, 11, 13));
    }

    [Fact]
    public void LosDiasRestantesNoCuentanFinesDeSemanaNiFestivos()
    {
        CalendarioColombia.DiasHabilesRestantes(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 4)).Should().Be(2);
        CalendarioColombia.DiasHabilesRestantes(new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 4)).Should().Be(-2);
    }
}

public sealed class BitacoraTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static List<EntradaBitacora> Cadena(int lineas)
    {
        var cadena = new List<EntradaBitacora>();
        var anterior = "";
        for (var i = 0; i < lineas; i++)
        {
            var e = EntradaBitacora.Crear(Guid.NewGuid(), Empresa, Ahora.AddMinutes(i).AddTicks(7), "Persona", "Laura Moreno", $"Acción {i}", "detalle", null,
                "Core", "Usuario", null, false, anterior);
            cadena.Add(e);
            anterior = e.Huella;
        }

        return cadena;
    }

    [Fact]
    public void UnaCadenaSinCambiosEsIntegra()
    {
        var v = CadenaBitacora.Verificar(Cadena(5));

        v.Integra.Should().BeTrue();
        v.Revisadas.Should().Be(5);
    }

    [Fact]
    public void LaFechaSeTruncaAMicrosegundosParaQueLaHuellaSobrevivaAPostgres()
    {
        var e = Cadena(1)[0];

        (e.Fecha.UtcTicks % 10).Should().Be(0);
        e.CalcularHuella().Should().Be(e.Huella);
    }

    [Fact]
    public void SiAlguienCambiaUnaLineaLaVerificacionLaSenala()
    {
        var cadena = Cadena(4);
        typeof(EntradaBitacora).GetProperty(nameof(EntradaBitacora.Detalle))!.SetValue(cadena[2], "detalle alterado");

        var v = CadenaBitacora.Verificar(cadena);

        v.Integra.Should().BeFalse();
        v.PrimeraAlterada.Should().Be(cadena[2].Id);
    }

    [Fact]
    public void SiAlguienBorraUnaLineaLaCadenaSeRompe()
    {
        var cadena = Cadena(4);
        cadena.RemoveAt(1);

        CadenaBitacora.Verificar(cadena).Integra.Should().BeFalse();
    }
}

public sealed class SolicitudTitularTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    private static SolicitudTitular Registrar(TipoSolicitud tipo) => SolicitudTitular.Registrar(Guid.NewGuid(), tipo, "Sara Gómez", "1.020.330.330",
        "sara@correo.co", "Correo", "Quiero que borren mis datos", "Borrador", "Laura Moreno", Ahora, Colombia).Value;

    [Theory]
    [InlineData(TipoSolicitud.Consulta, 10)]
    [InlineData(TipoSolicitud.Supresion, 15)]
    [InlineData(TipoSolicitud.Actualizacion, 15)]
    public void ElPlazoEsElDeLaLey1581(TipoSolicitud tipo, int dias)
    {
        var s = Registrar(tipo);

        SolicitudTitular.PlazoDiasHabiles(tipo).Should().Be(dias);
        s.Vence.Should().Be(CalendarioColombia.SumarDiasHabiles(new DateOnly(2026, 10, 22), dias));
        s.Documento.Should().Be("1020330330");
    }

    [Fact]
    public void SinContactoNoSePuedeRegistrar()
    {
        SolicitudTitular.Registrar(Guid.NewGuid(), TipoSolicitud.Consulta, "Sara", "1020330330", " ", "Correo", "", "", "Laura", Ahora, Colombia)
            .Error.Should().Be(SolicitudErrors.SinContacto);
    }

    [Fact]
    public void SoloEnUnaSupresionSeApruebaBorrarLosDatos()
    {
        var consulta = Registrar(TipoSolicitud.Consulta);
        var supresion = Registrar(TipoSolicitud.Supresion);

        consulta.Responder("Tenemos tus datos…", aprobarSupresion: true, "Laura", Ahora).IsSuccess.Should().BeTrue();
        supresion.Responder("Eliminamos tus datos…", aprobarSupresion: true, "Laura", Ahora).IsSuccess.Should().BeTrue();

        consulta.SupresionAprobada.Should().BeFalse();
        supresion.SupresionAprobada.Should().BeTrue();
        supresion.Responder("otra", false, "Laura", Ahora).Error.Should().Be(SolicitudErrors.YaRespondida);
    }
}
