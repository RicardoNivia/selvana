using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class AfinidadTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static (Candidato Candidato, Guid PostulacionId) Postulado()
    {
        var c = Candidato.Registrar(Guid.NewGuid(), new DatosCandidato { Nombre = "Ana", Documento = "1036635121" }, Ahora);
        var p = c.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], new AutorizacionDatos { Version = "v3" }, Guid.NewGuid(), [], Ahora).Value;
        return (c, p.Id);
    }

    [Fact]
    public void LaIaCalculaYUnaPersonaAjustaConMotivoSinBorrarLaSugerencia()
    {
        var (c, id) = Postulado();
        c.PedirAfinidad(id).Value.Afinidad!.Estado.Should().Be(EstadoAfinidad.Calculando);
        c.AjustarAfinidad(id, 90, "Tiene la licencia vigente", "Laura", Ahora).Error.Should().Be(AfinidadErrors.SinCalcular);

        c.RegistrarAfinidad(id, 140, [new RazonPuntaje { Texto = "3 años como montacarguista", Evidencia = "HV · experiencia 1" }], ["Curso de montacargas"], [], "claude-haiku-5-5", Ahora);
        var a = c.Postulaciones[0].Afinidad!;
        a.PuntajeIa.Should().Be(100, "el puntaje va de 0 a 100");

        c.AjustarAfinidad(id, 70, " ", "Laura", Ahora).Error.Should().Be(AfinidadErrors.SinMotivo);
        c.AjustarAfinidad(id, 70, "El curso venció en 2025", "Laura", Ahora).IsSuccess.Should().BeTrue();

        a.Puntaje.Should().Be(70);
        a.PuntajeIa.Should().Be(100);
        a.AjustadoPor.Should().Be("Laura");
    }

    [Fact]
    public void AlSuprimirSeBorraLaAfinidad()
    {
        var (c, id) = Postulado();
        c.RegistrarAfinidad(id, 80, [], [], [], "haiku", Ahora);

        c.Suprimir(Ahora);

        c.Postulaciones[0].Afinidad.Should().BeNull();
    }
}

public sealed class TableroTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MoverDejaHistorialYDescartarExigeMotivo()
    {
        var c = Candidato.Registrar(Guid.NewGuid(), new DatosCandidato { Nombre = "Ana", Documento = "1036635121" }, Ahora);
        var id = c.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], new AutorizacionDatos { Version = "v3" }, Guid.NewGuid(), [], Ahora).Value.Id;

        c.MoverPostulacion(id, EtapaPostulacion.Preseleccion, null, "Laura", Ahora).IsSuccess.Should().BeTrue();
        c.MoverPostulacion(id, EtapaPostulacion.Descartada, "  ", "Laura", Ahora).Error.Should().Be(EtapaErrors.SinMotivo);
        c.MoverPostulacion(id, EtapaPostulacion.Descartada, "No tiene la licencia C1", "Laura", Ahora).IsSuccess.Should().BeTrue();

        var p = c.Postulaciones[0];
        p.Etapa.Should().Be(EtapaPostulacion.Descartada);
        p.MotivoDescarte.Should().Be("No tiene la licencia C1");
        p.Historial.Select(h => (h.De, h.A)).Should().Equal((EtapaPostulacion.Recibida, EtapaPostulacion.Preseleccion), (EtapaPostulacion.Preseleccion, EtapaPostulacion.Descartada));

        c.MoverPostulacion(id, EtapaPostulacion.Recibida, null, "Laura", Ahora).IsSuccess.Should().BeTrue("se puede reconsiderar a un descartado");
        p.MotivoDescarte.Should().BeNull();
    }

    [Fact]
    public void UnTitularSuprimidoNoSeMueve()
    {
        var c = Candidato.Registrar(Guid.NewGuid(), new DatosCandidato { Nombre = "Ana", Documento = "1036635121" }, Ahora);
        var id = c.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], new AutorizacionDatos { Version = "v3" }, Guid.NewGuid(), [], Ahora).Value.Id;
        c.Suprimir(Ahora);

        c.MoverPostulacion(id, EtapaPostulacion.Terna, null, "Laura", Ahora).Error.Should().Be(EtapaErrors.Suprimido);
    }
}

public sealed class RetroalimentacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static (Candidato, Guid) Postulado()
    {
        var c = Candidato.Registrar(Guid.NewGuid(), new DatosCandidato { Nombre = "Ana", Documento = "1036635121" }, Ahora);
        return (c, c.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], new AutorizacionDatos { Version = "v3" }, Guid.NewGuid(), [], Ahora).Value.Id);
    }

    [Theory]
    [InlineData("Es muy joven para el cargo")]
    [InlineData("Mejor un hombre para cargar")]
    [InlineData("Tiene hijos pequeños")]
    [InlineData("Vive en estrato 1")]
    public void NingunaDecisionSePuedeBasarEnVariablesProtegidas(string motivo)
    {
        var (c, id) = Postulado();
        c.RegistrarAfinidad(id, 80, [], [], [], "haiku", Ahora);

        c.MoverPostulacion(id, EtapaPostulacion.Descartada, motivo, "Laura", Ahora).Error.Should().Be(RetroalimentacionErrors.MotivoDiscriminatorio);
        c.AjustarAfinidad(id, 40, motivo, "Laura", Ahora).Error.Should().Be(RetroalimentacionErrors.MotivoDiscriminatorio);
        c.Retroalimentar(id, ValorRetroalimentacion.NoEncaja, motivo, "Laura", Ahora).Error.Should().Be(RetroalimentacionErrors.MotivoDiscriminatorio);
    }

    [Fact]
    public void LaRetroalimentacionQuedaConQuienYCuando()
    {
        var (c, id) = Postulado();

        c.Retroalimentar(id, ValorRetroalimentacion.Bueno, "Excelente manejo de inventarios", "Laura", Ahora).IsSuccess.Should().BeTrue();
        c.MoverPostulacion(id, EtapaPostulacion.Descartada, "No tiene la licencia C1 que pide la vacante", "Laura", Ahora).IsSuccess.Should().BeTrue();

        c.Postulaciones[0].Retroalimentacion.Should().BeEquivalentTo(new { Valor = ValorRetroalimentacion.Bueno, Por = "Laura" });
    }
}
