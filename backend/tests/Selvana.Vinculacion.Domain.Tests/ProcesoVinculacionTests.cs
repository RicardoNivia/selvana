using FluentAssertions;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Domain.Tests;

public class ProcesoVinculacionTests
{
    private static readonly ParametrosLegales Legales = new(1_750_905m, 249_095m);
    private static readonly DateOnly Hoy = new(2026, 10, 23);
    private static readonly DateTimeOffset Ahora = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);

    private static CondicionesContratacion Condiciones(TipoContrato tipo = TipoContrato.Indefinido, decimal salario = 1_900_000m, DateOnly? fin = null,
        int prueba = 60, string? labor = null) => new()
        {
            Tipo = tipo,
            SalarioCop = salario,
            FechaIngreso = new DateOnly(2026, 11, 3),
            FechaFin = fin,
            PeriodoPruebaDias = prueba,
            Labor = labor,
            Jornada = "Tiempo completo",
            Ciudad = "Funza",
        };

    private static ProcesoVinculacion Proceso(CondicionesContratacion? c = null) =>
        ProcesoVinculacion.Abrir(Guid.NewGuid(), "Servicios Andinos", null, null,
            new PersonaVinculada { Nombre = "Yeimy Rojas", Documento = "1.023.456.789", Correo = "y@correo.co", Celular = "3001234567", Ciudad = "Funza" },
            new VacanteOrigen { VacanteId = Guid.NewGuid(), PostulacionId = Guid.NewGuid(), Codigo = "VAC-1", Titulo = "Montacarguista", Cargo = "Montacarguista" },
            c ?? Condiciones(), "Laura Moreno", Ahora);

    private static ProcesoVinculacion ConBorrador()
    {
        var p = Proceso();
        p.RegistrarBorrador("Hola [TRABAJADOR]: te ofrecemos el cargo.", [new Clausula { Titulo = "PRIMERA. Partes", Texto = "[TRABAJADOR], cédula [DOCUMENTO]." }],
            [new RazonOferta { Texto = "Usa el salario aprobado", Fuente = "Condiciones" }], [], "claude-haiku-5-5", 0.01m, Ahora).Should().BeTrue();
        return p;
    }

    [Fact]
    public void SalarioMenorAlMinimoBloquea()
    {
        var alertas = ReglasContrato.Revisar(Condiciones(salario: 1_500_000m), Legales, Hoy, null);
        alertas.Should().Contain(a => a.Nivel == NivelAlerta.Bloquea && a.Fuente == "Salario mínimo");
        ReglasContrato.Bloquea(alertas).Should().BeTrue();
    }

    [Fact]
    public void TerminoFijoSinFechaFinBloqueaYPruebaMaximaEsLaQuintaParte()
    {
        ReglasContrato.Revisar(Condiciones(TipoContrato.TerminoFijo), Legales, Hoy, null).Should().Contain(a => a.Nivel == NivelAlerta.Bloquea && a.Fuente == "Art. 46 CST");

        // 3 meses (91 días): máximo 18 días de prueba
        var corto = Condiciones(TipoContrato.TerminoFijo, fin: new DateOnly(2027, 2, 1), prueba: 30);
        ReglasContrato.Revisar(corto, Legales, Hoy, null).Should().Contain(a => a.Nivel == NivelAlerta.Bloquea && a.Texto.Contains("quinta parte", StringComparison.Ordinal));
    }

    [Fact]
    public void MisionDeMasDeUnAnoBloqueaYSinLaborTambien()
    {
        var mision = Condiciones(TipoContrato.MisionTemporal, fin: new DateOnly(2028, 1, 1), prueba: 60);
        var alertas = ReglasContrato.Revisar(mision, Legales, Hoy, "Logística Funza");
        alertas.Should().Contain(a => a.Nivel == NivelAlerta.Bloquea && a.Fuente == "Art. 77 Ley 50 de 1990");
        alertas.Should().Contain(a => a.Nivel == NivelAlerta.Bloquea && a.Fuente == "Art. 45 CST");
    }

    [Fact]
    public void HastaDosMinimosTieneAuxilioDeTransporte()
    {
        ReglasContrato.TieneAuxilioTransporte(Condiciones(salario: 2_000_000m), Legales).Should().BeTrue();
        ReglasContrato.TieneAuxilioTransporte(Condiciones(salario: 4_000_000m), Legales).Should().BeFalse();
        ReglasContrato.Revisar(Condiciones(salario: 2_000_000m), Legales, Hoy, null).Should().Contain(a => a.Nivel == NivelAlerta.Info && a.Texto.Contains("auxilio", StringComparison.Ordinal));
    }

    [Fact]
    public void SugiereMisionParaEmpresaUsuariaConAspiracionEnRangoYSinAlertasQueBloqueen()
    {
        var s = SugeridorCondiciones.Sugerir("Montacarguista", "Logística Funza", "Tiempo completo", "Funza", 1_800_000m, 2_100_000m, 1_950_000m, Legales, Hoy);
        s.Condiciones.Tipo.Should().Be(TipoContrato.MisionTemporal);
        s.Condiciones.SalarioCop.Should().Be(1_950_000m);
        s.Condiciones.FechaIngreso.DayOfWeek.Should().Be(DayOfWeek.Monday);
        s.Condiciones.FechaIngreso.Should().BeOnOrAfter(Hoy.AddDays(7));
        s.Razones.Should().HaveCountGreaterThanOrEqualTo(3).And.HaveCountLessThanOrEqualTo(5);
        ReglasContrato.Bloquea(ReglasContrato.Revisar(s.Condiciones, Legales, Hoy, "Logística Funza", 1_800_000m, 2_100_000m)).Should().BeFalse();
    }

    [Fact]
    public void SugiereIndefinidoParaPlantaPropiaYNuncaMenosDelMinimo()
    {
        var s = SugeridorCondiciones.Sugerir("Analista de nómina", null, "Tiempo completo", "Bogotá", 0, 0, null, Legales, Hoy);
        s.Condiciones.Tipo.Should().Be(TipoContrato.Indefinido);
        s.Condiciones.SalarioCop.Should().Be(Legales.SalarioMinimoCop);
        s.Condiciones.PeriodoPruebaDias.Should().Be(60);
    }

    [Fact]
    public void NoSeApruebaConAlertasQueBloquean()
    {
        var p = ConBorrador();
        var alertas = ReglasContrato.Revisar(Condiciones(salario: 1_000_000m), Legales, Hoy, null);
        p.AprobarOferta(alertas, "huella", "Laura Moreno", Ahora).Error.Should().Be(ProcesoErrors.CondicionesBloquean);
        p.Oferta.Estado.Should().Be(EstadoOferta.PorAprobar);
    }

    [Fact]
    public void LaAceptacionExigeElDocumentoDeLaPostulacionYDejaEvidenciaIntegra()
    {
        var p = ConBorrador();
        p.AprobarOferta([], "huella", "Laura Moreno", Ahora).IsSuccess.Should().BeTrue();

        p.ResponderOferta(true, "Yeimy Rojas", "999", null, "10.0.0.1", "Chrome", Ahora).Error.Should().Be(ProcesoErrors.DocumentoNoCoincide);
        p.ResponderOferta(true, "Yeimy Rojas", "1023456789", null, "10.0.0.1", "Chrome", Ahora.AddMinutes(5)).IsSuccess.Should().BeTrue();

        p.Oferta.Estado.Should().Be(EstadoOferta.Aceptada);
        p.Etapa.Should().Be(EtapaVinculacion.Documentos);
        p.Oferta.Respuesta!.Huella.Should().HaveLength(64);
        p.EvidenciaIntegra().Should().BeTrue();
        p.TextoOferta().Should().Contain("Yeimy Rojas").And.Contain("1.023.456.789").And.NotContain("[TRABAJADOR]");
    }

    [Fact]
    public void SiCambiaElTextoDespuesDeAceptarLaEvidenciaNoCoincide()
    {
        var p = ConBorrador();
        p.AprobarOferta([], "huella", "Laura Moreno", Ahora);
        p.ResponderOferta(true, "Yeimy Rojas", "1023456789", null, "10.0.0.1", "Chrome", Ahora);

        p.Oferta.Clausulas[0] = new Clausula { Titulo = "PRIMERA. Partes", Texto = "Otro texto" };
        p.EvidenciaIntegra().Should().BeFalse();
    }

    [Fact]
    public void LaEdicionHumanaQuedaRegistradaYCambiarCondicionesVuelveARedactar()
    {
        var p = ConBorrador();
        p.EditarOferta("Hola [TRABAJADOR]: carta corregida.", [new Clausula { Titulo = "PRIMERA", Texto = "Texto" }], "Laura Moreno", Ahora).IsSuccess.Should().BeTrue();
        p.Oferta.EditadaPor.Should().Be("Laura Moreno");

        p.CambiarCondiciones(Condiciones(salario: 2_000_000m), Ahora).IsSuccess.Should().BeTrue();
        p.Oferta.Estado.Should().Be(EstadoOferta.Redactando);
        p.Oferta.EditadaPor.Should().BeNull();
        p.Condiciones.SalarioCop.Should().Be(2_000_000m);
    }

    [Fact]
    public void RechazarNoAvanzaYCancelarCierraElProceso()
    {
        var p = ConBorrador();
        p.AprobarOferta([], "huella", "Laura Moreno", Ahora);
        p.ResponderOferta(false, null, null, "Acepté otra oferta", "10.0.0.1", "Chrome", Ahora).IsSuccess.Should().BeTrue();
        p.Oferta.Estado.Should().Be(EstadoOferta.Rechazada);
        p.Etapa.Should().Be(EtapaVinculacion.Oferta);

        p.Cancelar("El candidato no aceptó", Ahora).IsSuccess.Should().BeTrue();
        p.Abierto.Should().BeFalse();
        p.Cancelar("otra vez", Ahora).Error.Should().Be(ProcesoErrors.Terminado);
    }
}
