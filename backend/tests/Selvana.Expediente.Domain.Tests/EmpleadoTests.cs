using FluentAssertions;
using Selvana.Expediente.Domain.Empleados;

namespace Selvana.Expediente.Domain.Tests;

public class EmpleadoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 28);

    private static readonly SituacionLaboral Situacion =
        new("Montacarguista", "Operaciones", "CC-201", "Funza", null, null, "Término fijo", 1_900_000m, new DateOnly(2027, 5, 1));

    private static Empleado Nuevo(Afiliaciones? afiliaciones = null) =>
        Empleado.Crear(Guid.NewGuid(), "Vinculación", "1016554209", "Diana Gómez", "diana@correo.co", "3001234567", "Funza", Situacion, new DateOnly(2026, 11, 2),
            afiliaciones ?? new Afiliaciones { Eps = "Sura", FondoPension = "Porvenir", Arl = "Positiva", ClaseRiesgoArl = 3, CajaCompensacion = "Compensar" }, "Laura", Ahora);

    [Fact]
    public void NaceActivoConLaNovedadDeIngreso()
    {
        var e = Nuevo();

        e.Estado.Should().Be(EstadoEmpleado.Activo);
        e.Codigo.Should().StartWith("EMP-");
        e.Novedades.Should().ContainSingle().Which.Tipo.Should().Be(TipoNovedad.Ingreso);
        e.ProximoVencimiento.Should().Be(new DateOnly(2027, 5, 1));
        e.AfiliacionesCompletas.Should().BeTrue();
    }

    [Fact]
    public void LaNovedadGuardaAntesYDespues()
    {
        var e = Nuevo();

        e.RegistrarNovedad(TipoNovedad.CambioCargo, Situacion with { Cargo = "Coordinador de bodega", SalarioCop = 2_600_000m }, new DateOnly(2027, 1, 1),
            "Ascenso", "Laura", Ahora).IsSuccess.Should().BeTrue();

        e.Cargo.Should().Be("Coordinador de bodega");
        e.Novedades[^1].Cambios.Should().Contain("Cargo: Montacarguista → Coordinador de bodega").And.Contain("Salario: $1.900.000 → $2.600.000");
        e.RegistrarNovedad(TipoNovedad.Traslado, e.Situacion, Hoy, "Nada", "Laura", Ahora).Error.Should().Be(EmpleadoErrors.SinCambios);
    }

    [Fact]
    public void LosDocumentosTienenVersionesYAlertasDeVencimiento()
    {
        var e = Nuevo();
        var doc = e.GuardarDocumento(null, "Licencia", "Licencia de montacargas", Guid.NewGuid(), "lic.pdf", Hoy.AddDays(10), null, "Laura", Ahora).Value;

        e.Alertas(Hoy).Should().Contain(a => a.Nivel == NivelAlertaEmpleado.PorVencer && a.Texto.Contains("Licencia de montacargas", StringComparison.Ordinal));
        e.ProximoVencimiento.Should().Be(Hoy.AddDays(10));

        e.GuardarDocumento(doc.Id, "", "", Guid.NewGuid(), "lic-2027.pdf", Hoy.AddYears(1), "Renovada", "Laura", Ahora).IsSuccess.Should().BeTrue();

        doc.Versiones.Should().HaveCount(2);
        doc.Vigente!.NombreArchivo.Should().Be("lic-2027.pdf");
        e.Alertas(Hoy).Should().NotContain(a => a.Texto.Contains("Licencia", StringComparison.Ordinal));
    }

    [Fact]
    public void AvisaLasAfiliacionesQueFaltanYElContratoPorTerminar()
    {
        var e = Nuevo(new Afiliaciones { Eps = "Sura" });

        e.AfiliacionesCompletas.Should().BeFalse();
        e.Alertas(new DateOnly(2027, 4, 1)).Should().Contain(a => a.Nivel == NivelAlertaEmpleado.Falta)
            .And.Contain(a => a.Fuente == "Art. 46 CST");
    }

    [Fact]
    public void RetiradoNoSeModificaNiTieneAlertas()
    {
        var e = Nuevo();

        e.Retirar(new DateOnly(2026, 10, 1), "Renuncia", "Laura", Ahora).Error.Should().Be(EmpleadoErrors.FechaRetiro);
        e.Retirar(new DateOnly(2027, 2, 1), "Renuncia voluntaria", "Laura", Ahora).IsSuccess.Should().BeTrue();

        e.Estado.Should().Be(EstadoEmpleado.Retirado);
        e.Alertas(Hoy).Should().BeEmpty();
        e.ProximoVencimiento.Should().BeNull();
        e.RegistrarNovedad(TipoNovedad.CambioSalario, Situacion with { SalarioCop = 2_000_000m }, Hoy, "x", "Laura", Ahora).Error.Should().Be(EmpleadoErrors.Retirado);
    }
}
