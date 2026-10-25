using FluentAssertions;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Agentes.Vinculacion;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.Vinculacion.Contracts.Ia;

namespace Selvana.AI.Tests;

public sealed class PlanificadorInduccionTests
{
    private static readonly DatosInduccion Datos = new("Servicios Andinos", "Logística Funza", "Montacarguista", "Operaciones", "Funza", "Misión en empresa usuaria",
        "Tiempo completo", new DateOnly(2026, 11, 2), 36, 3, "Opera montacargas en el CEDI", ["Licencia de montacargas"],
        [new TareaExistente("Inducción en seguridad y salud en el trabajo", "Seguridad y salud en el trabajo", 0)]);

    [Fact]
    public void LaPreguntaNoLlevaDatosDeLaPersonaYListaLasTareasExistentes()
    {
        var texto = PlanificadorInduccionAgente.Pregunta(Datos);
        texto.Should().Contain("Montacarguista").And.Contain("02/11/2026").And.Contain("Logística Funza")
            .And.Contain("Día 0: Inducción en seguridad y salud en el trabajo");
    }

    [Fact]
    public void ValidarQuitaRepetidasDiscriminatoriasYNormalizaResponsableYDia()
    {
        var crudo = new PropuestaInduccion(
        [
            new TareaInduccionPropuesta("Inducción en seguridad y salud en el trabajo", "Repite la legal", "SST", 0, ""),
            new TareaInduccionPropuesta("Recorrer la bodega", "Con el líder", "líder inmediato", 0, "Descripción del cargo"),
            new TareaInduccionPropuesta("Recorrer la bodega", "Duplicada", "Líder inmediato", 1, ""),
            new TareaInduccionPropuesta("Confirmar el estado civil", "Para el turno", "Recursos Humanos", 1, ""),
            new TareaInduccionPropuesta("Seguimiento a los 90 días", "Con el líder", "el jefe", 200, ""),
        ],
        [new RazonRedaccion("-", "Primera semana acompañada", "Cargo"), new RazonRedaccion("+", "Por su edad", "Cargo")], ["Confirme el WMS"]);

        var v = PlanificadorInduccionAgente.Validar(crudo, Datos, new FiltroVariablesProtegidas());

        v.Tareas.Select(t => t.Titulo).Should().Equal("Recorrer la bodega", "Seguimiento a los 90 días");
        v.Tareas[0].Responsable.Should().Be("Líder inmediato");
        v.Tareas[1].Responsable.Should().Be("Recursos Humanos");
        v.Tareas[1].Dia.Should().Be(90);
        v.Tareas[1].Fuente.Should().Be("Descripción del cargo");
        v.Razones.Should().ContainSingle().Which.Signo.Should().Be("−");
        v.Alertas.Should().Equal("Confirme el WMS");
    }
}
