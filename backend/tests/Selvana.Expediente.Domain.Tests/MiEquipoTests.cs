using FluentAssertions;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Expediente.Domain.Equipo;

namespace Selvana.Expediente.Domain.Tests;

public class MiEquipoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 30, 15, 0, 0, TimeSpan.Zero);

    private static Empleado Nuevo() =>
        Empleado.Crear(Guid.NewGuid(), "Manual", "1016554209", "Diana Gómez", "diana@correo.co", "3001234567", "Funza",
            new SituacionLaboral("Montacarguista", "Bodega", "CC-201", "Funza", null, null, "Término fijo", 1_900_000m, new DateOnly(2027, 5, 1)),
            new DateOnly(2026, 1, 2), new Afiliaciones { Eps = "Sura", FondoPension = "Porvenir", Arl = "Positiva", CajaCompensacion = "Compensar" }, "Laura", Ahora);

    [Theory]
    [InlineData("¿Quién tiene el curso de alturas vencido?", FiltroRapido.DocumentosVencidos, "altura")]
    [InlineData("¿a quién le termina el contrato?", FiltroRapido.ContratoPorTerminar, null)]
    [InlineData("quién tiene riesgo de irse", FiltroRapido.RiesgoRotacion, null)]
    [InlineData("¿A quién me falta evaluar?", FiltroRapido.PorEvaluar, null)]
    [InlineData("los nuevos de bodega", FiltroRapido.Nuevos, "bodega")]
    [InlineData("¿qué documentos vencen pronto?", FiltroRapido.DocumentosPorVencer, null)]
    [InlineData("faltan afiliaciones de EPS", FiltroRapido.Afiliaciones, null)]
    public void EntiendeLasPreguntasDelLider(string pregunta, FiltroRapido filtro, string? termino)
    {
        var i = PreguntaEquipo.Interpretar(pregunta);

        i.Filtro.Should().Be(filtro);
        if (termino is null)
        {
            i.Terminos.Should().BeEmpty();
        }
        else
        {
            i.Terminos.Should().Equal(termino);
        }

        i.Entendido.Should().NotBeEmpty("siempre se muestra lo que se entendió");
    }

    [Fact]
    public void SinPreguntaNoFiltra() => PreguntaEquipo.Interpretar("  ").Vacia.Should().BeTrue();

    [Fact]
    public void LoQueNoReconoceLoBuscaComoTermino()
    {
        var i = PreguntaEquipo.Interpretar("montacarguistas de Funza");

        i.Filtro.Should().Be(FiltroRapido.Todos);
        i.Terminos.Should().Equal("montacarguista", "funza");
    }

    [Fact]
    public void RrhhAsignaYQuitaElLider()
    {
        var e = Nuevo();
        var lider = Guid.NewGuid();

        e.AsignarLider(lider, " Jorge Castro ", Ahora).IsSuccess.Should().BeTrue();
        e.LiderId.Should().Be(lider);
        e.LiderNombre.Should().Be("Jorge Castro");

        e.AsignarLider(null, "Jorge Castro", Ahora);
        e.LiderId.Should().BeNull();
        e.LiderNombre.Should().BeNull();
    }

    [Fact]
    public void ARetiradoNoSeLeAsignaLider()
    {
        var e = Nuevo();
        e.Retirar(new DateOnly(2026, 11, 1), "Renuncia voluntaria", "Laura", Ahora);

        e.AsignarLider(Guid.NewGuid(), "Jorge", Ahora).Error.Should().Be(EmpleadoErrors.Retirado);
    }
}
