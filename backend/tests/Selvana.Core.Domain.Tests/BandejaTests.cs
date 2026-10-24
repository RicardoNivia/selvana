using FluentAssertions;
using Selvana.Core.Domain.Bandeja;

namespace Selvana.Core.Domain.Tests;

public sealed class BandejaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 22);

    private static PropuestaBandeja Vacante(DateOnly? vence = null, bool afectaPersona = false)
    {
        var p = PropuestaBandeja.Crear(Guid.NewGuid(), "Reclutamiento", "VacanteRedactada", Guid.NewGuid(), Ahora);
        p.Actualizar(null, "Vacante redactada · Auxiliar de bodega", "Funza · 25 cupos", "Agente redactor de vacantes", "gemini-3.5-flash-lite",
            Enumerable.Range(1, 7).Select(i => new RazonBandeja { Texto = $"Razón {i}", Fuente = "Frase" }), [], null, "/consola/vacantes/1", afectaPersona, vence, null, Ahora);
        return p;
    }

    [Fact]
    public void GuardaMaximoCincoRazones()
    {
        Vacante().Razones.Should().HaveCount(5);
    }

    [Fact]
    public void ResolverLaSacaDeLosPendientesConQuienDecidio()
    {
        var p = Vacante();

        p.Resolver("Publicada como la propuso la IA", "Laura Moreno", Ahora.AddMinutes(5));

        p.Estado.Should().Be(EstadoPropuesta.Resuelta);
        p.DecididaPor.Should().Be("Laura Moreno");
    }

    [Fact]
    public void SiLaIaVuelveAProponerQuedaPendienteDeNuevo()
    {
        var p = Vacante();
        p.Resolver("Publicada", "Laura", Ahora);

        p.Actualizar(null, "Vacante redactada otra vez", "", "Agente redactor de vacantes", null, [], [], null, "/x", false, null, null, Ahora.AddHours(1));

        p.Estado.Should().Be(EstadoPropuesta.Pendiente);
        p.DecididaPor.Should().BeNull();
    }

    [Fact]
    public void DescartarExigeMotivoYNoSeDescartaDosVeces()
    {
        var p = Vacante();

        p.Descartar(" ", "Laura", Ahora).Error.Should().Be(PropuestaErrors.SinMotivo);
        p.Descartar("El salario está bajo", "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.Decision.Should().Contain("El salario está bajo");
        p.Descartar("otra vez", "Laura", Ahora).Error.Should().Be(PropuestaErrors.YaDecidida);
    }

    [Fact]
    public void EsUrgenteSoloPorElPlazo()
    {
        Vacante(Hoy.AddDays(2)).EsUrgente(Hoy).Should().BeTrue();
        Vacante(Hoy.AddDays(-1)).EsUrgente(Hoy).Should().BeTrue();
        Vacante(Hoy.AddDays(9)).EsUrgente(Hoy).Should().BeFalse();
        Vacante(afectaPersona: true).EsUrgente(Hoy).Should().BeFalse();
    }
}
