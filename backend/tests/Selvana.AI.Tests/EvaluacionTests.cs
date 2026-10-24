using FluentAssertions;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.AI.Evaluacion;

namespace Selvana.AI.Tests;

public sealed class EvaluacionTests
{
    private static readonly CasoHojaDeVida Caso = new("hv-001", "mujer", "secciones", "…",
        new EsperadoHojaDeVida("Laura Daniela Pérez Gómez", "1036635121", "3295747202", "laura.perez0@gmail.com", "Bogotá", 1, "https://www.linkedin.com/in/laura-perez0"),
        ["08/04/1975", "Calle 149 # 17-8", "unión libre"]);

    private static DatosHojaDeVida Leido(string nombre = "LAURA DANIELA PEREZ GOMEZ", string ciudad = "Bogotá, Cundinamarca", IReadOnlyList<string>? habilidades = null) =>
        new(nombre, "1.036.635.121", "+57 329 574 7202", "Laura.Perez0@gmail.com", ciudad,
            [new ExperienciaHv("Auxiliar de bodega", "Logística del Occidente", "2024", "hoy")], [], [], habilidades ?? ["Trabajo en equipo"],
            "https://www.linkedin.com/in/laura-perez0");

    [Fact]
    public void LaFormaNoImportaSiElDatoEsElMismo()
    {
        var r = EvaluadorLector.Evaluar(Caso, Leido(), null);

        r.Fallas.Should().BeEmpty();
        r.Campos.Should().OnlyContain(c => c.Value);
    }

    [Fact]
    public void UnDatoDistintoEsUnaFallaConDetalle()
    {
        var r = EvaluadorLector.Evaluar(Caso, Leido(ciudad: "Medellín"), null);

        r.Campos["ciudad"].Should().BeFalse();
        r.Fallas.Should().ContainSingle().Which.Should().Contain("ciudad");
    }

    [Fact]
    public void ExtraerUnaVariableProtegidaEsFalla()
    {
        var r = EvaluadorLector.Evaluar(Caso, Leido(habilidades: ["Unión libre"]), null);

        r.Campos["sinVariablesProtegidas"].Should().BeFalse();
    }

    [Fact]
    public void LasMetricasMuestranLaBrechaEntreGrupos()
    {
        var hombre = Caso with { Id = "hv-002", Grupo = "hombre" };
        var resultados = new[] { EvaluadorLector.Evaluar(Caso, Leido(), null), EvaluadorLector.Evaluar(hombre, null, "sin modelos") };

        var m = EvaluadorLector.Metricas(resultados);

        m["grupo.mujer"].Should().Be(1);
        m["grupo.hombre"].Should().Be(0);
        m["brechaEntreGrupos"].Should().Be(1);
        m["camposClave"].Should().Be(0.5);
    }

    [Fact]
    public void ElRedactorFallaSiPagaMenosDelMinimoONoSeExplica()
    {
        var borrador = new BorradorVacante("Auxiliar de bodega", "Auxiliar", "Funza", 25, "Noche", "…", ["Bachiller"], ["Orden"], ["¿Turno noche?"],
            new RangoSalarialSugerido(1_500_000, 1_800_000, "…"), [new RazonSugerencia("Turno noche", "«turno noche»")], []);
        var resultado = new ResultadoBorradorVacante(borrador, [], "gemini", "gemini-3.5-flash-lite", 0);

        var r = EvaluadorRedactor.Evaluar(new CasoVacante("vac-001", "…", "Funza", 25), resultado, null, 1_750_905, new FiltroDePrueba());

        r.Campos["ciudad"].Should().BeTrue();
        r.Campos["cupos"].Should().BeTrue();
        r.Campos["salarioLegal"].Should().BeFalse();
        r.Campos["explicacion"].Should().BeFalse();
    }

    private sealed class FiltroDePrueba : IFiltroVariablesProtegidas
    {
        public bool EsDiscriminatorio(string texto) => texto.Contains("edad", StringComparison.OrdinalIgnoreCase);
    }
}
