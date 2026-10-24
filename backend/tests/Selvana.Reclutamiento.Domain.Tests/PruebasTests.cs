using FluentAssertions;
using Selvana.Reclutamiento.Domain.Pruebas;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class PruebasTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LaSuiteBasicaTieneSusCuatroPruebasYNingunaEstaValidadaTodavia()
    {
        SuitePsicotecnica.Todas.Select(p => p.Codigo).Should().Equal("ATENCION", "NUMERICO", "SJT-SERVICIO", "PERSONALIDAD");
        SuitePsicotecnica.Todas.Should().OnlyContain(p => !p.Validada);
        SuitePsicotecnica.Personalidad.Sensible.Should().BeTrue();
        SuitePsicotecnica.Personalidad.Preguntas.GroupBy(p => p.Dimension).Should().HaveCount(5).And.OnlyContain(g => g.Count() == 4);
    }

    [Fact]
    public void LaAptitudSeCalificaSobreCien()
    {
        var correctas = SuitePsicotecnica.Atencion.Preguntas.ToDictionary(p => p.Id, p => p.Opciones.First(o => o.Puntaje == 1).Id);
        CalificadorPruebas.Calificar(SuitePsicotecnica.Atencion, correctas).Puntaje.Should().Be(100);

        var mitad = correctas.Take(5).ToDictionary();
        CalificadorPruebas.Calificar(SuitePsicotecnica.Atencion, mitad).Puntaje.Should().Be(50);
    }

    [Fact]
    public void EnPersonalidadLasPreguntasInversasRestan()
    {
        // «Muy de acuerdo» en todo: las directas suben y las inversas bajan; cada rasgo tiene 2 y 2 o 1 y 3
        var todoCinco = SuitePsicotecnica.Personalidad.Preguntas.ToDictionary(p => p.Id, _ => "5");

        var r = CalificadorPruebas.Calificar(SuitePsicotecnica.Personalidad, todoCinco);

        r.Puntaje.Should().BeNull();
        r.Dimensiones.Single(d => d.Nombre == "Extraversión").Puntaje.Should().Be(50);
        r.Dimensiones.Single(d => d.Nombre == "Apertura").Puntaje.Should().Be(25);
    }

    [Fact]
    public void LaPersonalidadNoAdmiteCorte()
    {
        Baterias.Validar([new PruebaEnBateria { Codigo = "PERSONALIDAD", Peso = 1, Corte = 60 }]).Error.Should().Be(BateriaErrors.CorteEnPersonalidad);
        Baterias.Validar([new PruebaEnBateria { Codigo = "NO-EXISTE" }]).Error.Should().Be(BateriaErrors.PruebaNoExiste);
    }

    [Theory]
    [InlineData("Asesor de servicio al cliente", "SJT-SERVICIO")]
    [InlineData("AUXILIAR DE BODEGA", "ATENCION")]
    [InlineData("Analista contable", "ATENCION")]
    public void LaBateriaSeSugiereSegunElCargo(string cargo, string primera)
    {
        Baterias.Sugerir(cargo).Pruebas[0].Codigo.Should().Be(primera);
        Baterias.ClaveCargo("  Auxiliar  de Bodéga ").Should().Be("auxiliar de bodega");
    }

    private static AplicacionPruebas Enviada() => AplicacionPruebas.Enviar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Andrés Salcedo",
        [new PruebaEnBateria { Codigo = "ATENCION", Peso = 2, Corte = 70 }, new PruebaEnBateria { Codigo = "PERSONALIDAD", Peso = 1 }], "huella", "Laura", Ahora);

    [Fact]
    public void SinConsentimientoNoSeEmpieza()
    {
        var a = Enviada();

        a.Iniciar("ATENCION", Ahora).Error.Should().Be(AplicacionErrors.SinConsentimiento);
        a.ResponderConsentimiento(acepta: false, Ahora).IsSuccess.Should().BeTrue();
        a.Estado.Should().Be(EstadoAplicacion.Rechazada);
    }

    [Fact]
    public void AlTerminarTodasSeCalculaElTotalSinLaPersonalidad()
    {
        var a = Enviada();
        a.ResponderConsentimiento(true, Ahora);
        a.Iniciar("ATENCION", Ahora).Value.Should().Be(4 * 60);
        var correctas = SuitePsicotecnica.Atencion.Preguntas.Select(p => new RespuestaPrueba { PreguntaId = p.Id, OpcionId = p.Opciones.First(o => o.Puntaje == 1).Id, Milisegundos = 4000 }).ToList();
        a.Terminar("ATENCION", correctas, 0, Ahora.AddMinutes(3)).IsSuccess.Should().BeTrue();
        a.Iniciar("PERSONALIDAD", Ahora.AddMinutes(4));
        a.Terminar("PERSONALIDAD", [.. SuitePsicotecnica.Personalidad.Preguntas.Select(p => new RespuestaPrueba { PreguntaId = p.Id, OpcionId = "4", Milisegundos = 3000 })], 0, Ahora.AddMinutes(7));

        a.Estado.Should().Be(EstadoAplicacion.Terminada);
        a.PuntajeTotal().Should().Be(100);
        a.Pruebas[0].Alertas.Should().BeEmpty();
        a.Terminar("ATENCION", correctas, 0, Ahora.AddMinutes(8)).Error.Should().Be(AplicacionErrors.YaTerminada);
    }

    [Fact]
    public void ResponderMuyRapidoOFueraDeTiempoDejaAlertasNoDescalifica()
    {
        var a = Enviada();
        a.ResponderConsentimiento(true, Ahora);
        a.Iniciar("ATENCION", Ahora);
        var alAzar = SuitePsicotecnica.Atencion.Preguntas.Take(8).Select(p => new RespuestaPrueba { PreguntaId = p.Id, OpcionId = "a", Milisegundos = 300 }).ToList();

        a.Terminar("ATENCION", alAzar, cambiosDePestana: 4, Ahora.AddMinutes(9)).IsSuccess.Should().BeTrue();

        a.Pruebas[0].Alertas.Should().HaveCount(4);
        a.Pruebas[0].Puntaje.Should().NotBeNull();
    }

    [Fact]
    public void ElEnlaceVenceALas72Horas()
    {
        var a = Enviada();

        a.ResponderConsentimiento(true, Ahora.AddHours(73)).Error.Should().Be(AplicacionErrors.Vencida);
        a.Reenviar("nueva", "Laura", Ahora.AddHours(73));
        a.ResponderConsentimiento(true, Ahora.AddHours(74)).IsSuccess.Should().BeTrue();
    }
}
