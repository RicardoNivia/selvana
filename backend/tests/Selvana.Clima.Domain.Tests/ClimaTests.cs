using FluentAssertions;
using Selvana.Clima.Domain.Encuestas;
using Selvana.Clima.Domain.Pulsos;
using Selvana.Clima.Domain.Rotacion;

namespace Selvana.Clima.Domain.Tests;

public class ClimaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 29);

    private static Encuesta Abierta()
    {
        var e = Encuesta.Crear(Guid.NewGuid(), "Clima 2026", PlantillaClima.Preguntas(), true, PlantillaClima.PreguntaAbierta, ["Bodega", "Ventas"], ["Funza"], "Laura", Ahora);
        e.Abrir("huella", Ahora).IsSuccess.Should().BeTrue();
        return e;
    }

    private static RespuestaClima Responder(Encuesta e, string area, int valor, int enps, string comentario = "") =>
        e.Responder(area, "Funza", e.Preguntas.ToDictionary(p => p.Id, _ => valor), enps, comentario, Ahora).Value;

    [Fact]
    public void ConMenosDeCincoRespuestasNoMuestraResultados()
    {
        var e = Abierta();
        var respuestas = Enumerable.Range(0, 4).Select(_ => Responder(e, "Bodega", 4, 9)).ToList();

        var r = CalculoResultados.Calcular(e, respuestas);

        r.Promedio.Should().BeNull();
        r.Grupos.Should().BeEmpty();
    }

    [Fact]
    public void OcultaLosGruposPequenosYCalculaElEnps()
    {
        var e = Abierta();
        var respuestas = Enumerable.Range(0, 6).Select(i => Responder(e, "Bodega", 4, i < 3 ? 10 : 5)).Append(Responder(e, "Ventas", 2, 3)).ToList();

        var r = CalculoResultados.Calcular(e, respuestas);

        r.Grupos.Where(g => g.Tipo == "Área").Should().ContainSingle().Which.Nombre.Should().Be("Bodega");
        r.GruposOcultos.Should().Be(1, "Ventas tiene una sola respuesta");
        // 3 promotores y 4 detractores de 7 → 43 % − 57 % = −14
        r.Enps.Should().Be(-14);
    }

    [Fact]
    public void UnaRespuestaIncompletaSeRechaza()
    {
        var e = Abierta();

        e.Responder("Bodega", "Funza", new Dictionary<string, int> { ["lid1"] = 4 }, 9, null, Ahora).Error.Should().Be(EncuestaErrors.Respuesta);
    }

    [Fact]
    public void UnAreaQueNoEstaEnLaListaQuedaVacia()
    {
        var e = Abierta();

        Responder(e, "Gerencia secreta", 3, 7).Area.Should().BeEmpty();
    }

    [Fact]
    public void LosTemasPorReglasAgrupanYDanTono()
    {
        var (temas, _) = TemasPorReglas.Analizar(["Los turnos de noche son muy pesados y no hay descanso", "Mucha carga los domingos",
            "Mi jefe no escucha", "Excelente ambiente con los compañeros", "El salario no alcanza"]);

        temas[0].Nombre.Should().Be("Turnos y carga de trabajo");
        temas[0].Cantidad.Should().Be(2);
        temas.Should().Contain(t => t.Nombre == "Compañerismo y ambiente" && t.Sentimiento == "Positivo");
    }

    [Theory]
    [InlineData(29, null)]
    [InlineData(30, 30)]
    [InlineData(59, 30)]
    [InlineData(60, 60)]
    [InlineData(95, 90)]
    [InlineData(125, null)]
    public void ElPulsoSeAbreEnSuHito(int dias, int? hito) => Pulso.HitoVigente(Hoy.AddDays(-dias), Hoy).Should().Be(hito);

    [Fact]
    public void UnPulsoBajoQuedaEnAlerta()
    {
        var p = Pulso.Programar(Guid.NewGuid(), Guid.NewGuid(), "Ana", "1", "a@b.co", "Cajera", "Tiendas", null, Hoy.AddDays(-30), 30);

        p.Responder(2, 3, 2, 8, "Me siento sola", Ahora).IsSuccess.Should().BeTrue();

        p.Alerta.Should().BeTrue();
        p.Responder(5, 5, 5, 10, null, Ahora).Error.Should().Be(PulsoErrors.YaRespondido);
    }

    [Fact]
    public void ElRiesgoSumaSenalesConEvidencia()
    {
        var r = RiesgoRotacion.Calcular(new DatosRiesgo(Hoy.AddDays(-40), Hoy.AddDays(20), 2.0m, 5, 30, 2.9m, -20, null, null), Hoy);

        r.Nivel.Should().Be("Alto");
        r.Senales.Should().HaveCount(4);
        r.Acciones.Should().NotBeEmpty();
    }

    [Fact]
    public void SinSenalesElRiesgoEsBajo() =>
        RiesgoRotacion.Calcular(new DatosRiesgo(Hoy.AddYears(-3), null, 4.5m, 10, 90, 4.1m, 40, 3.4m, "Medio"), Hoy).Nivel.Should().Be("Bajo");
}
