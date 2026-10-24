using FluentAssertions;
using Selvana.Reclutamiento.Domain.Preseleccion;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class PreseleccionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    private static ConversacionPreseleccion Nueva() =>
        ConversacionPreseleccion.Invitar(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "huella", "Hola, Ana.", "Laura", Ahora);

    [Fact]
    public void ElCandidatoEscribeYElAgenteResponde()
    {
        var c = Nueva();

        c.EscribeCandidato("Sí, me interesa", Ahora).IsSuccess.Should().BeTrue();
        c.Pensando.Should().BeTrue();
        c.EscribeCandidato("¿Hola?", Ahora).Error.Should().Be(ConversacionErrors.Pensando, "no se responde dos veces a la vez");

        c.RespondeAgente("¡Qué bien! ¿Puedes trabajar en turno rotativo?", new ResumenPreseleccion { Interesado = true }, false, null, "haiku", Ahora);

        c.Estado.Should().Be(EstadoConversacion.Abierta);
        c.Pensando.Should().BeFalse();
        c.Mensajes.Select(m => m.Autor).Should().Equal(AutorMensaje.Agente, AutorMensaje.Candidato, AutorMensaje.Agente);
        c.Resumen.Interesado.Should().BeTrue();
    }

    [Fact]
    public void SiPideUnaPersonaSeEscalaYElAgenteYaNoResponde()
    {
        var c = Nueva();
        c.EscribeCandidato("Quiero hablar con alguien", Ahora);
        c.RespondeAgente("Claro, una persona te responde aquí mismo.", new ResumenPreseleccion(), false, "El candidato pidió hablar con una persona", "haiku", Ahora);

        c.Estado.Should().Be(EstadoConversacion.Escalada);
        c.EscribeCandidato("¿Siguen ahí?", Ahora).IsSuccess.Should().BeTrue();
        c.Pensando.Should().BeFalse("escalada, responde una persona");

        c.EscribePersona("Hola, soy Laura de RRHH.", "Laura", cerrar: true, Ahora).IsSuccess.Should().BeTrue();
        c.Estado.Should().Be(EstadoConversacion.Completada);
        c.EscribeCandidato("Gracias", Ahora).Error.Should().Be(ConversacionErrors.Cerrada);
    }

    [Fact]
    public void SiLaIaFallaPasaAUnaPersonaSinDejarAlCandidatoEsperando()
    {
        var c = Nueva();
        c.EscribeCandidato("Hola", Ahora);

        c.FallaAgente("Ningún modelo respondió", Ahora);

        c.Pensando.Should().BeFalse();
        c.Estado.Should().Be(EstadoConversacion.Escalada);
        c.Mensajes[^1].Autor.Should().Be(AutorMensaje.Agente);
    }
}
