using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Seleccion;

namespace Selvana.Reclutamiento.Domain.Tests;

public class ReglasAvanceTests
{
    private static readonly ReglasAvance Base = new();

    private static SenalesPostulacion Senales(EtapaPostulacion etapa, int? puntaje = 80, string filtros = "Cumple", string? chat = null, bool? interesado = null,
        decimal? aspiracion = null, string? pruebas = null, bool? cortes = null, bool bateria = true) =>
        new(etapa, puntaje, filtros, filtros == "NoCumple" ? "Licencia C2: no aparece en la hoja de vida" : null, chat, interesado, aspiracion, pruebas, null, cortes, bateria, 2_000_000m);

    [Fact]
    public void RecibidaConAfinidadSuficientePasaAPreseleccionYPuedeSerAutomatica()
    {
        var s = EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Recibida, 75), Base);

        s.Accion.Should().Be("Avanzar");
        s.Destino.Should().Be(EtapaPostulacion.Preseleccion);
        s.Automatica.Should().BeTrue();
        s.Criterios.Should().OnlyContain(c => c.Cumple == true);
    }

    [Fact]
    public void ElUmbralLoDefineLaVacante() =>
        EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Recibida, 75), new ReglasAvance { UmbralAfinidad = 85 }).Accion.Should().Be("Revisar");

    [Fact]
    public void SiNoCumpleUnExcluyenteSugiereDescartarPeroNuncaSolo()
    {
        var s = EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Recibida, 95, "NoCumple"), Base);

        s.Accion.Should().Be("Descartar");
        s.Automatica.Should().BeFalse();
        s.Titulo.Should().Contain("Licencia C2");
    }

    [Fact]
    public void SinPuntajeEspera() =>
        EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Recibida, null), Base).Accion.Should().Be("Esperar");

    [Theory]
    [InlineData(null, "Invitar", true)]
    [InlineData("Invitada", "Esperar", false)]
    [InlineData("Abierta", "Esperar", false)]
    [InlineData("Escalada", "Revisar", false)]
    public void EnPreseleccionSigueElEstadoDelChat(string? chat, string accion, bool automatica)
    {
        var s = EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Preseleccion, chat: chat), Base);

        s.Accion.Should().Be(accion);
        s.Automatica.Should().Be(automatica);
    }

    [Fact]
    public void ChatCompletoConInteresYSalarioEnRangoPasaAPruebas()
    {
        var s = EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Preseleccion, chat: "Completada", interesado: true, aspiracion: 2_150_000m), Base);

        s.Accion.Should().Be("Avanzar");
        s.Destino.Should().Be(EtapaPostulacion.Pruebas);
        s.Automatica.Should().BeTrue();
    }

    [Fact]
    public void AspiracionFueraDeLaToleranciaPideRevision() =>
        EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Preseleccion, chat: "Completada", interesado: true, aspiracion: 2_300_000m), Base).Accion.Should().Be("Revisar");

    [Fact]
    public void SinInteresSugiereDescartar() =>
        EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Preseleccion, chat: "Completada", interesado: false), Base).Accion.Should().Be("Descartar");

    [Fact]
    public void SinBateriaVaALaTernaPeroLaDecideUnaPersona()
    {
        var s = EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Preseleccion, chat: "Completada", interesado: true, bateria: false), Base);

        s.Destino.Should().Be(EtapaPostulacion.Terna);
        s.Automatica.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null, "EnviarPruebas")]
    [InlineData("EnCurso", null, "Esperar")]
    [InlineData("Rechazada", null, "Revisar")]
    [InlineData("Terminada", false, "Revisar")]
    [InlineData("Terminada", true, "Avanzar")]
    public void EnPruebasSigueLosResultados(string? estado, bool? cortes, string accion)
    {
        var s = EvaluadorAvance.Evaluar(Senales(EtapaPostulacion.Pruebas, pruebas: estado, cortes: cortes), Base);

        s.Accion.Should().Be(accion);
        if (accion == "Avanzar")
        {
            s.Destino.Should().Be(EtapaPostulacion.Terna);
            s.Automatica.Should().BeFalse("la terna siempre la aprueba una persona");
        }
    }
}
