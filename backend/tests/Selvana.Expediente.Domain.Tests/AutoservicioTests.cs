using FluentAssertions;
using Selvana.Expediente.Domain.Empleados;

namespace Selvana.Expediente.Domain.Tests;

public class AutoservicioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

    private static Empleado Empleado() => Empleados.Empleado.Crear(Guid.NewGuid(), "Manual", "1016554209", "Diana Gómez", "diana@correo.co", "3001234567", "Funza",
        new SituacionLaboral("Montacarguista", "", "", "Funza", null, null, "Término indefinido", 1_900_000m, null), new DateOnly(2025, 1, 10),
        new Afiliaciones { Eps = "Sura", FondoPension = "Porvenir", Arl = "Positiva", CajaCompensacion = "Compensar" }, "Laura", Ahora);

    [Fact]
    public void ProponerGuardaSoloLoQueCambiaConAntesYDespues()
    {
        var e = Empleado();

        var r = e.ProponerDatos("3109876543", "Funza", "Sanitas", "Porvenir", "Compensar", Ahora);

        r.IsSuccess.Should().BeTrue();
        e.CambioPropuesto!.Cambios.Should().BeEquivalentTo(["Celular: 3001234567 → 3109876543", "EPS: Sura → Sanitas"]);
        e.Celular.Should().Be("3001234567", "nada cambia hasta que RRHH aplique");
    }

    [Fact]
    public void ProponerSinCambiosFalla()
    {
        Empleado().ProponerDatos("3001234567", "Funza", "Sura", "Porvenir", "Compensar", Ahora).Error.Should().Be(AutoservicioErrors.SinCambios);
    }

    [Fact]
    public void AplicarDejaLosDatosYUnaNovedadConElMotivo()
    {
        var e = Empleado();
        e.ProponerDatos("3109876543", "Mosquera", "Sanitas", "Porvenir", "Compensar", Ahora);

        e.AplicarCambioPropuesto("Laura Moreno", Ahora).IsSuccess.Should().BeTrue();

        e.Celular.Should().Be("3109876543");
        e.Ciudad.Should().Be("Mosquera");
        e.Afiliaciones.Eps.Should().Be("Sanitas");
        e.Afiliaciones.Arl.Should().Be("Positiva", "la ARL no la cambia el empleado");
        e.CambioPropuesto.Should().BeNull();
        var n = e.Novedades[^1];
        n.Motivo.Should().Be("Lo propuso el empleado desde su portal");
        n.Por.Should().Be("Laura Moreno");
    }

    [Fact]
    public void DescartarNoTocaElExpediente()
    {
        var e = Empleado();
        e.ProponerDatos("3109876543", "Funza", "Sura", "Porvenir", "Compensar", Ahora);

        e.DescartarCambioPropuesto(Ahora).IsSuccess.Should().BeTrue();

        e.Celular.Should().Be("3001234567");
        e.CambioPropuesto.Should().BeNull();
    }

    [Fact]
    public void LaCampanaSePideUnaVezYSeRespondeConfirmando()
    {
        var e = Empleado();

        e.PedirActualizacion(Ahora).Should().BeTrue();
        e.PedirActualizacion(Ahora).Should().BeFalse("ya estaba pedida");
        e.ConfirmarDatos(Ahora.AddDays(1)).IsSuccess.Should().BeTrue();

        e.ActualizacionPedidaEn.Should().BeNull();
        e.DatosConfirmadosEn.Should().Be(Ahora.AddDays(1));
        e.Novedades[^1].Cambios.Should().ContainSingle("Confirmó que sus datos siguen iguales");
    }

    [Fact]
    public void ConfirmarSinCampanaFalla()
    {
        Empleado().ConfirmarDatos(Ahora).Error.Should().Be(AutoservicioErrors.SinCampana);
    }

    [Fact]
    public void ProponerTambienRespondeLaCampana()
    {
        var e = Empleado();
        e.PedirActualizacion(Ahora);

        e.ProponerDatos("3109876543", "Funza", "Sura", "Porvenir", "Compensar", Ahora);

        e.ActualizacionPedidaEn.Should().BeNull();
    }

    [Fact]
    public void ElEmpleadoVeAvisosDeSusDocumentosPorVencer()
    {
        var e = Empleado();
        e.GuardarDocumento(null, "Licencia", "Licencia de montacargas", Guid.NewGuid(), "lic.pdf", new DateOnly(2026, 11, 10), null, "Laura", Ahora);

        e.AvisosParaElEmpleado(new DateOnly(2026, 10, 29)).Should().ContainSingle().Which.Should().Contain("Licencia de montacargas");
    }
}
