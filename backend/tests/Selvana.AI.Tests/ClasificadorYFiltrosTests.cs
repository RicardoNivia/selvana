using System.Net;
using FluentAssertions;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Gateway;

namespace Selvana.AI.Tests;

public sealed class ClasificadorFallasTests
{
    private readonly ClasificadorFallas _clasificador = new();

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, TipoFalla.CuotaAgotada)]
    [InlineData(HttpStatusCode.NotFound, TipoFalla.ModeloNoDisponible)]
    [InlineData(HttpStatusCode.Unauthorized, TipoFalla.ModeloNoDisponible)]
    [InlineData(HttpStatusCode.InternalServerError, TipoFalla.Transitoria)]
    public void ClasificaPorCodigoHttp(HttpStatusCode codigo, TipoFalla esperado) =>
        _clasificador.Clasificar(new HttpRequestException("x", null, codigo)).Should().Be(esperado);

    [Theory]
    [InlineData("429 RESOURCE_EXHAUSTED: quota exceeded", TipoFalla.CuotaAgotada)]
    [InlineData("rate_limit_error", TipoFalla.CuotaAgotada)]
    [InlineData("models/gemini-2.5-pro is NOT_FOUND", TipoFalla.ModeloNoDisponible)]
    [InlineData("connection reset", TipoFalla.Transitoria)]
    public void ClasificaPorElMensajeCuandoNoHayCodigo(string mensaje, TipoFalla esperado) =>
        _clasificador.Clasificar(new InvalidOperationException(mensaje)).Should().Be(esperado);

    [Fact]
    public void LeeElCodigoCuandoElSdkRedefineStatusCode() =>
        _clasificador.Clasificar(new ErrorDerivadoDelSdk()).Should().Be(TipoFalla.ModeloNoDisponible);

    private class ErrorBaseDelSdk : Exception
    {
        public int StatusCode { get; } = 500;
    }

    private sealed class ErrorDerivadoDelSdk : ErrorBaseDelSdk
    {
        public new int StatusCode { get; } = 404;
    }
}

public sealed class FiltroVariablesProtegidasTests
{
    private readonly FiltroVariablesProtegidas _filtro = new();

    [Theory]
    [InlineData("Hombres entre 20 y 35 años")]
    [InlineData("Mujer soltera sin hijos")]
    [InlineData("Excelente presentación y buena presencia")]
    [InlineData("Adjuntar foto reciente")]
    [InlineData("Menor de 40 años")]
    [InlineData("Que viva en estrato 3")]
    public void DetectaRequisitosDiscriminatorios(string requisito) =>
        _filtro.EsDiscriminatorio(requisito).Should().BeTrue();

    [Theory]
    [InlineData("Curso de montacargas vigente")]
    [InlineData("6 meses de experiencia en bodega")]
    [InlineData("Disponibilidad para turno noche")]
    [InlineData("Bachiller")]
    public void DejaPasarRequisitosDelCargo(string requisito) =>
        _filtro.EsDiscriminatorio(requisito).Should().BeFalse();
}

public sealed class AjusteSalarioMinimoTests
{
    private static BorradorVacante Borrador(decimal minimo, decimal maximo) => new(
        "Auxiliar", "Auxiliar de bodega", "Funza", 25, "Nocturna", "",
        [], [], [], new RangoSalarialSugerido(minimo, maximo, ""), [], []);

    [Fact]
    public void SubeElRangoAlMinimoLegalYLoDeclaraComoSupuesto()
    {
        var ajustado = RedactorVacantesAgente.AjustarAlMinimo(Borrador(1_400_000, 1_600_000), 1_750_905);

        ajustado.RangoSalarial.MinimoCop.Should().Be(1_750_905);
        ajustado.RangoSalarial.MaximoCop.Should().Be(1_750_905);
        ajustado.Supuestos.Should().ContainSingle(s => s.Contains("salario mínimo", StringComparison.Ordinal));
    }

    [Fact]
    public void NoTocaUnRangoValido()
    {
        var original = Borrador(1_900_000, 2_200_000);

        RedactorVacantesAgente.AjustarAlMinimo(original, 1_750_905).Should().BeSameAs(original);
    }
}

public sealed class PerfilDelCargoTests
{
    [Fact]
    public void SinCargoDelCatalogoSeRedactaDesdeCero() =>
        Selvana.AI.Agentes.RedactorVacantes.RedactorVacantesAgente.TextoPerfil(null).Should().BeEmpty();

    [Fact]
    public void ConPerfilAprobadoEsLaBaseYSeCita()
    {
        var texto = Selvana.AI.Agentes.RedactorVacantes.RedactorVacantesAgente.TextoPerfil(new Selvana.Reclutamiento.Contracts.PerfilCargoVacante(
            "Auxiliar de bodega", "Logística", true, "Recibir y despachar mercancía", ["Cargar y descargar"], ["Bachiller"], "Clase III"));

        texto.Should().Contain("APROBADO").And.Contain("«Auxiliar de bodega»").And.Contain("Cargar y descargar").And.Contain("Perfil del cargo").And.Contain("Clase III");
    }

    [Fact]
    public void UnPerfilEnBorradorEsSoloReferencia() =>
        Selvana.AI.Agentes.RedactorVacantes.RedactorVacantesAgente.TextoPerfil(new Selvana.Reclutamiento.Contracts.PerfilCargoVacante(
            "Cajero", "Ventas", false, "Atender la caja", [], [], "")).Should().Contain("BORRADOR").And.NotContain("APROBADO");
}

public sealed class RankingAfinidadTests
{
    [Fact]
    public void ElAgenteNuncaRecibeDatosQueIdentifiquenAlCandidato()
    {
        var campos = typeof(Selvana.Reclutamiento.Contracts.PerfilAnonimo).GetProperties().Select(p => p.Name.ToLowerInvariant());

        campos.Should().NotContain(["nombre", "documento", "celular", "correo", "linkedin", "edad", "genero", "direccion"]);
    }

    [Fact]
    public void ElPuntajeSeValidaYSeRetiranLasRazonesConVariablesProtegidas()
    {
        var crudo = new Selvana.AI.Agentes.Reclutamiento.PuntajeAfinidad(130,
        [
            new("+", "3 años como montacarguista", "HV · experiencia 1"),
            new("-", "Es muy joven para el cargo", "HV"),
            new("+", "Curso vigente", "la hoja"),
        ], ["Curso"], []);

        var v = Selvana.AI.Agentes.Reclutamiento.CalificadorAfinidadAgente.Validar(crudo, new Selvana.AI.Agentes.RedactorVacantes.FiltroVariablesProtegidas());

        v.Puntaje.Should().Be(100);
        v.Razones.Should().HaveCount(2).And.NotContain(r => r.Texto.Contains("joven"));
        v.Razones[1].Evidencia.Should().Be("HV", "la evidencia siempre cita la hoja de vida o las preguntas filtro");
    }
}

public sealed class CalibracionRankingTests
{
    [Fact]
    public void LasDecisionesDeLaEmpresaLleganAlAgenteSinIdentificarANadie()
    {
        var vacante = new Selvana.Reclutamiento.Contracts.RequisitosVacante("Montacarguista", "Montacarguista", "Funza", "Noche", "…", ["Curso de montacargas"], []);
        var perfil = new Selvana.Reclutamiento.Contracts.PerfilAnonimo("Funza", [], [], [], [], []);
        var criterios = new[] { new Selvana.Reclutamiento.Contracts.CriterioEmpresa("Montacarguista", "Auxiliar de bodega; 2 años", 80, "no encaja", "Sin curso vigente") };

        var texto = Selvana.AI.Agentes.Reclutamiento.CalificadorAfinidadAgente.Pregunta(vacante, perfil, criterios);

        texto.Should().Contain("CRITERIOS DE ESTA EMPRESA").And.Contain("no encaja").And.Contain("Sin curso vigente");
        Selvana.AI.Agentes.Reclutamiento.CalificadorAfinidadAgente.Pregunta(vacante, perfil).Should().NotContain("CRITERIOS");
    }
}

public sealed class PreseleccionAgenteTests
{
    [Fact]
    public void SiElAgenteMencionaUnaVariableProtegidaSuMensajeSeRetiraYSeEscala()
    {
        var filtro = new Selvana.AI.Agentes.RedactorVacantes.FiltroVariablesProtegidas();
        var turno = new Selvana.AI.Agentes.Reclutamiento.TurnoPreseleccion("¿Qué edad tienes?",
            new Selvana.AI.Contracts.Reclutamiento.ResumenConversacion(true, 1_800_000, "Turno rotativo", "Funza", [], [], "Casado, vive en Funza"), false, false, "");

        var v = Selvana.AI.Agentes.Reclutamiento.PreseleccionAgente.Validar(turno, filtro);

        v.Mensaje.Should().Be(Selvana.AI.Agentes.Reclutamiento.PreseleccionAgente.MensajeRetirado);
        v.Escalar.Should().BeTrue();
        v.Resumen.Texto.Should().BeEmpty("el resumen nunca guarda el estado civil");
        v.Resumen.AspiracionSalarialCop.Should().Be(1_800_000);
    }
}

public sealed class FechaEnAgentesTests
{
    [Fact]
    public void ElAgenteDeRankingSabeQueDiaEsParaJuzgarVigencias()
    {
        var vacante = new Selvana.Reclutamiento.Contracts.RequisitosVacante("Montacarguista", "Montacarguista", "Funza", "Rotativo", "…", ["Curso de montacargas vigente"], []);
        var perfil = new Selvana.Reclutamiento.Contracts.PerfilAnonimo("Funza", [], [], ["Curso de montacargas, vigente hasta 03/2026"], [], []);

        var texto = Selvana.AI.Agentes.Reclutamiento.CalificadorAfinidadAgente.Pregunta(vacante, perfil, null, new DateOnly(2026, 10, 23));

        texto.Should().Contain("FECHA DE HOY: 2026-10-23").And.Contain("VENCIDO");
    }
}

public sealed class TextoDelModeloTests
{
    [Fact]
    public void LasTildesEscapadasVuelvenATextoNormal() =>
        Selvana.AI.Agentes.Reclutamiento.CalificadorAfinidadAgente.SinEscapes("Certificado de aptitud para la conducci\u00f3n").Should().Be("Certificado de aptitud para la conducción");
}
