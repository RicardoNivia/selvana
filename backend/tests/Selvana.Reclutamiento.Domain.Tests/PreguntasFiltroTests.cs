using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class PreguntasFiltroTests
{
    [Theory]
    [InlineData("¿Tienes curso de montacargas vigente?", TipoPreguntaFiltro.SiNo)]
    [InlineData("¿Puedes trabajar en turno noche?", TipoPreguntaFiltro.SiNo)]
    [InlineData("¿Cuántos años de experiencia tienes como desarrollador senior?", TipoPreguntaFiltro.Numero)]
    [InlineData("¿cuantas personas has tenido a cargo?", TipoPreguntaFiltro.Numero)]
    [InlineData("¿Cuál es tu aspiración salarial mensual en pesos?", TipoPreguntaFiltro.Pesos)]
    [InlineData("¿Cuál es tu expectativa salarial mensual en pesos colombianos?", TipoPreguntaFiltro.Pesos)]
    public void ElTipoDeRespuestaSaleDeComoEstaEscritaLaPregunta(string pregunta, TipoPreguntaFiltro tipo) =>
        PreguntasFiltro.Tipo(pregunta).Should().Be(tipo);

    [Fact]
    public void LasRespuestasConCifraSeMuestranConSuValorYNoCuentanComoNegativas()
    {
        var anios = new RespuestaFiltro { Pregunta = "¿Cuántos años de experiencia tienes como cajero?", Respuesta = true, Valor = 3 };
        var salario = new RespuestaFiltro { Pregunta = "¿Cuál es tu aspiración salarial mensual en pesos?", Respuesta = true, Valor = 2_500_000 };
        var no = new RespuestaFiltro { Pregunta = "¿Tienes curso de alturas?", Respuesta = false };

        anios.Texto.Should().Be("3");
        salario.Texto.Should().Contain("2.500.000");
        no.Texto.Should().Be("No");
        new[] { anios, salario, no }.Count(r => r.EsNegativa).Should().Be(1);
    }
}
