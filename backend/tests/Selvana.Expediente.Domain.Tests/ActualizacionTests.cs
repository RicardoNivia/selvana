using FluentAssertions;
using Selvana.Expediente.Domain.Empleados;

namespace Selvana.Expediente.Domain.Tests;

public class ActualizacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);

    private static (Empleado E, DocumentoEmpleado D, Guid Archivo) ConDocumento(string tipo, string nombre)
    {
        var e = Empleado.Crear(Guid.NewGuid(), "Manual", "1016554209", "Diana Gómez", "", "", "Funza",
            new SituacionLaboral("Montacarguista", "", "", "Funza", null, null, "Término indefinido", 1_900_000m, null), new DateOnly(2025, 1, 10),
            new Afiliaciones { Eps = "Sura", FondoPension = "Porvenir", Arl = "Positiva", CajaCompensacion = "Compensar" }, "Laura", Ahora);
        var archivo = Guid.NewGuid();
        var d = e.GuardarDocumento(null, tipo, nombre, archivo, "doc.pdf", null, null, "Laura", Ahora).Value;
        e.PedirLectura(d.Id, Ahora).IsSuccess.Should().BeTrue();
        return (e, d, archivo);
    }

    private static CampoLeido Campo(string campo, string valor) => new() { Campo = campo, Valor = valor };

    [Fact]
    public void ElCertificadoDeEpsProponeCambiarLaEntidad()
    {
        var (e, d, archivo) = ConDocumento("Afiliación", "Certificado EPS");

        e.RegistrarLectura(d.Id, archivo, "Certificado de afiliación EPS", true, true, [Campo("número de documento", "1016554209"), Campo("entidad", "Sanitas EPS")],
            [], [], "haiku", 0.01m, Ahora).Should().BeTrue();

        var cambio = d.Vigente!.Lectura!.Cambios.Should().ContainSingle().Subject;
        cambio.Clave.Should().Be("Eps");
        cambio.Actual.Should().Be("Sura");
        cambio.Propuesto.Should().Be("Sanitas EPS");
    }

    [Fact]
    public void AplicarDejaElValorQueConfirmaRrhhYUnaNovedad()
    {
        var (e, d, archivo) = ConDocumento("Licencia", "Licencia de montacargas");
        e.RegistrarLectura(d.Id, archivo, "Licencia", true, true, [Campo("fecha de vencimiento", "15/11/2027")], [], [], "haiku", 0, Ahora);

        e.AplicarLectura(d.Id, [new CambioAplicado("VenceEn", "20/11/2027")], "Laura", Ahora).IsSuccess.Should().BeTrue();

        d.Vigente!.VenceEn.Should().Be(new DateOnly(2027, 11, 20));
        d.Vigente.Lectura!.Estado.Should().Be(EstadoLectura.Aplicada);
        e.Novedades[^1].Cambios.Should().ContainSingle().Which.Should().Contain("20/11/2027");
        e.ProximoVencimiento.Should().Be(new DateOnly(2027, 11, 20));
    }

    [Fact]
    public void SiElDocumentoEsDeOtraPersonaNoProponeNada()
    {
        var (e, d, archivo) = ConDocumento("Afiliación", "Certificado EPS");

        e.RegistrarLectura(d.Id, archivo, "Certificado EPS", true, true, [Campo("número de documento", "52999888"), Campo("entidad", "Sanitas")], [], [], "haiku", 0, Ahora);

        d.Vigente!.Lectura!.Cambios.Should().BeEmpty();
        d.Vigente.Lectura.Estado.Should().Be(EstadoLectura.PorRevisar);
        d.Vigente.Lectura.Observaciones.Should().Contain(o => o.Contains("52999888", StringComparison.Ordinal));
    }

    [Fact]
    public void SinNadaQueCambiarQuedaSinCambiosYSePuedeDescartarLoPendiente()
    {
        var (e, d, archivo) = ConDocumento("Afiliación", "Certificado EPS");

        e.RegistrarLectura(d.Id, archivo, "Certificado EPS", true, true, [Campo("entidad", "Sura")], [], [], "haiku", 0, Ahora).Should().BeFalse();

        d.Vigente!.Lectura!.Estado.Should().Be(EstadoLectura.SinCambios);
        e.DescartarLectura(d.Id, "Laura", Ahora).Error.Should().Be(ActualizacionErrors.SinLectura);
        e.AplicarLectura(d.Id, [], "Laura", Ahora).Error.Should().Be(ActualizacionErrors.SinLectura);
    }

    [Fact]
    public void NoAceptaClavesFueraDeLaLista()
    {
        var (e, d, archivo) = ConDocumento("Afiliación", "Certificado EPS");
        e.RegistrarLectura(d.Id, archivo, "Certificado EPS", true, true, [Campo("entidad", "Sanitas")], [], [], "haiku", 0, Ahora);

        e.AplicarLectura(d.Id, [new CambioAplicado("SalarioCop", "9000000")], "Laura", Ahora).Error.Should().Be(ActualizacionErrors.ClaveInvalida);
    }
}
