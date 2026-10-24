using FluentAssertions;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Agentes.Vinculacion;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.Vinculacion.Contracts.Ia;

namespace Selvana.AI.Tests;

public sealed class RedactorOfertaTests
{
    private static readonly CondicionesOferta Condiciones = new("Servicios Andinos", "Logística Funza", "Montacarguista", "Funza", "Tiempo completo",
        "Misión en empresa usuaria", 1_950_000m, true, new DateOnly(2026, 11, 2), new DateOnly(2027, 5, 1), 36, "Operar montacargas en el CEDI", "Opera montacargas",
        ["Licencia de montacargas"]);

    [Fact]
    public void LaPreguntaNoLlevaNombreNiDocumentoYTraeCifrasExactas()
    {
        var texto = RedactorOfertaAgente.Pregunta(Condiciones, new DateOnly(2026, 10, 23));
        texto.Should().Contain("$1.950.000").And.Contain("02/11/2026").And.Contain("Logística Funza").And.Contain("FECHA DE HOY: 23/10/2026");
    }

    [Fact]
    public void AlertaSiElTextoNoTraeElSalarioNiLaFechaYRetiraRazonesDiscriminatorias()
    {
        var crudo = new BorradorOferta("Hola [TRABAJADOR]: bienvenido.",
            [new ClausulaRedactada("PRIMERA. Partes", "[TRABAJADOR], cédula [DOCUMENTO]"), new ClausulaRedactada("", "conducci\\u00f3n segura")],
            [new RazonRedaccion("+", "Se omite por su edad", "Condiciones"), new RazonRedaccion("-", "Periodo de prueba de la quinta parte", "Art. 78 CST")], []);

        var v = RedactorOfertaAgente.Validar(crudo, Condiciones, new FiltroVariablesProtegidas());

        v.Razones.Should().ContainSingle().Which.Signo.Should().Be("−");
        v.Clausulas[1].Titulo.Should().Be("Cláusula");
        v.Clausulas[1].Texto.Should().Be("conducción segura");
        v.Alertas.Should().Contain(a => a.Contains("$1.950.000", StringComparison.Ordinal)).And.Contain(a => a.Contains("02/11/2026", StringComparison.Ordinal));
    }

    [Fact]
    public void SinAlertasCuandoElTextoTraeLoAprobado()
    {
        var crudo = new BorradorOferta("Hola [TRABAJADOR]: ingresas el 02/11/2026 con salario de $1.950.000.",
            [new ClausulaRedactada("PRIMERA. Partes", "[TRABAJADOR], cédula [DOCUMENTO]")], [], []);
        RedactorOfertaAgente.Validar(crudo, Condiciones, new FiltroVariablesProtegidas()).Alertas.Should().BeEmpty();
    }
}
