using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Filtros;
using Selvana.Reclutamiento.Domain.Vacantes;

namespace Selvana.Reclutamiento.Domain.Tests;

public class FiltrosAtsTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 23);

    private static DatosCandidato Hv(string ciudad = "Funza", params string[] cursos) => new()
    {
        Nombre = "Yeimy Rojas",
        Ciudad = ciudad,
        Cursos = [.. cursos],
        Habilidades = ["Pase C2"],
        Experiencia = [new ExperienciaLaboral { Cargo = "Montacarguista", Empresa = "Logística SAS", Desde = "01/2023", Hasta = "09/2026" }],
    };

    private static FiltrosVacante Filtros(string[]? requisitos = null, string[]? preguntas = null, string[]? ciudades = null, int? meses = null) =>
        ReglasFiltros.Armar(["Licencia de conducción C2", "2 años de experiencia como montacarguista", "Curso de trabajo en alturas", "Manejo de SAP"],
            ["¿Tiene disponibilidad para turno noche?"], requisitos ?? [], preguntas ?? [], ciudades ?? [], meses, "Laura", DateTimeOffset.UnixEpoch).Value;

    [Fact]
    public void SinFiltrosNoEvalua() =>
        EvaluadorFiltros.Evaluar(null, Hv(), [], null, Hoy).Resultado.Should().Be(ResultadoFiltros.SinFiltros);

    [Fact]
    public void ReconoceSinonimosYMideLosAnosConFechas()
    {
        var e = EvaluadorFiltros.Evaluar(Filtros(["Licencia de conducción C2", "2 años de experiencia como montacarguista"]), Hv(), [], null, Hoy);
        e.Resultado.Should().Be(ResultadoFiltros.Cumple);
        e.Chequeos.Should().OnlyContain(c => c.Estado == EstadoChequeo.Cumple);
        e.Chequeos[0].Evidencia.Should().Be("HV · habilidades y cursos");
        e.Chequeos[1].Evidencia.Should().Be("HV · experiencia");
    }

    [Fact]
    public void UnNoEnPreguntaExcluyenteNoCumpleConMotivoCitado()
    {
        var e = EvaluadorFiltros.Evaluar(Filtros(preguntas: ["¿Tiene disponibilidad para turno noche?"]), Hv(),
            [new RespuestaFiltro { Pregunta = "¿Tiene disponibilidad para turno noche?", Respuesta = false }], null, Hoy);
        e.Resultado.Should().Be(ResultadoFiltros.NoCumple);
        e.MotivoDescarte.Should().StartWith("Filtro ATS · ¿Tiene disponibilidad para turno noche?:").And.Contain("Respondió No");
        VariablesProtegidas.Menciona(e.MotivoDescarte).Should().BeFalse();
    }

    [Fact]
    public void SinEvidenciaQuedaPorRevisarYElRankingDecideSiLoDemostro()
    {
        var f = Filtros(["Manejo de SAP"]);
        EvaluadorFiltros.Evaluar(f, Hv(), [], null, Hoy).Resultado.Should().Be(ResultadoFiltros.PorRevisar);
        EvaluadorFiltros.Evaluar(f, Hv(), [], new AfinidadPostulacion { Faltantes = ["Manejo de SAP"] }, Hoy).Resultado.Should().Be(ResultadoFiltros.NoCumple);
        EvaluadorFiltros.Evaluar(f, Hv(), [], new AfinidadPostulacion { Cumplidos = ["manejo de sap"] }, Hoy).Resultado.Should().Be(ResultadoFiltros.Cumple);
    }

    [Fact]
    public void CiudadYExperienciaMinima()
    {
        var e = EvaluadorFiltros.Evaluar(Filtros(ciudades: ["Funza", "Mosquera"], meses: 60), Hv("Bogotá"), [], null, Hoy);
        e.Resultado.Should().Be(ResultadoFiltros.NoCumple);
        e.Chequeos.Should().Contain(c => c.Tipo == EvaluadorFiltros.TipoCiudad && c.Estado == EstadoChequeo.NoCumple);
        e.Chequeos.Should().Contain(c => c.Tipo == EvaluadorFiltros.TipoExperiencia && c.Estado == EstadoChequeo.NoCumple);
    }

    [Fact]
    public void NoAceptaFiltrosConVariablesProtegidasNiFueraDeLaVacante()
    {
        ReglasFiltros.Armar(["Menor de 35 años"], [], ["Menor de 35 años"], [], [], null, "Laura", DateTimeOffset.UnixEpoch).Error.Should().Be(FiltrosErrors.Protegido);
        ReglasFiltros.Armar(["Licencia C2"], [], ["Otra cosa"], [], [], null, "Laura", DateTimeOffset.UnixEpoch).Error.Should().Be(FiltrosErrors.NoEsRequisito);
    }

    [Fact]
    public void NoConfundeAnosDeEdadConExperiencia() =>
        EvaluadorFiltros.AniosPedidos("Entre 18 y 30 años de edad").Should().BeNull();
}
