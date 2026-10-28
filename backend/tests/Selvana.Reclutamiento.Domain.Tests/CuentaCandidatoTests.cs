using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Portal;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class CuentaCandidatoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 28, 8, 0, 0, TimeSpan.Zero);
    private static readonly AutorizacionDatos Autorizacion = new() { Version = "portal-v1", Texto = "Autorizo", AceptadaEn = Ahora };

    private static CuentaCandidato Cuenta() => CuentaCandidato.Registrar("ana@correo.com", "hash", "Ana Pérez", "CC", "1.023.456.789", Autorizacion, Ahora).Value;

    [Fact]
    public void SeRegistraConLoMinimoYCorreoYCedulaNormalizados()
    {
        var cuenta = CuentaCandidato.Registrar(" Ana.Perez@Correo.COM ", "hash", " Ana Pérez ", "cc", "1.023.456.789", Autorizacion, Ahora).Value;

        cuenta.Correo.Should().Be("ana.perez@correo.com");
        cuenta.TipoDocumento.Should().Be("CC");
        cuenta.Documento.Should().Be("1023456789");
        cuenta.Datos.Nombre.Should().Be("Ana Pérez");
        cuenta.Datos.Documento.Should().Be("1023456789");
        cuenta.ArchivoHojaDeVidaId.Should().BeNull("la hoja de vida se sube después, en el portal");
    }

    [Fact]
    public void ElPasaporteConservaLasLetras()
    {
        CuentaCandidato.Registrar("ana@correo.com", "hash", "Ana", "PA", "ab-123456", Autorizacion, Ahora).Value.Documento.Should().Be("AB123456");
    }

    [Fact]
    public void NoSeRegistraSinCorreoTipoIdentificacionValidosOSinAutorizacion()
    {
        CuentaCandidato.Registrar("ana@", "hash", "Ana", "CC", "1023456789", Autorizacion, Ahora).Error.Should().Be(CuentaErrors.CorreoInvalido);
        CuentaCandidato.Registrar("ana@correo.com", "hash", "Ana", "XX", "1023456789", Autorizacion, Ahora).Error.Should().Be(CuentaErrors.TipoDocumentoInvalido);
        CuentaCandidato.Registrar("ana@correo.com", "hash", "Ana", "CC", "12", Autorizacion, Ahora).Error.Should().Be(CuentaErrors.DocumentoInvalido);
        CuentaCandidato.Registrar("ana@correo.com", "hash", "Ana", "CC", "1023456789", null, Ahora).Error.Should().Be(CuentaErrors.SinAutorizacion);
    }

    [Fact]
    public void CincoIntentosFallidosBloqueanQuinceMinutos()
    {
        var cuenta = Cuenta();

        for (var i = 0; i < CuentaCandidato.IntentosAntesDeBloquear; i++)
        {
            cuenta.Ingresar(contrasenaValida: false, Ahora).Error.Should().Be(CuentaErrors.CredencialesInvalidas);
        }

        cuenta.Ingresar(contrasenaValida: true, Ahora.AddMinutes(5)).Error.Should().Be(CuentaErrors.Bloqueada);
        cuenta.Ingresar(contrasenaValida: true, Ahora.AddMinutes(16)).IsSuccess.Should().BeTrue();
        cuenta.UltimoIngreso.Should().Be(Ahora.AddMinutes(16));
    }

    [Fact]
    public void LaHojaDeVidaNuevaNoCambiaElCorreoNiLaCedulaQueUnenLasPostulaciones()
    {
        var cuenta = Cuenta();

        cuenta.ActualizarHojaDeVida(Guid.NewGuid(), "hv.pdf",
            new DatosCandidato { Nombre = "Ana María Pérez", Documento = "999999", Correo = "otra@correo.com", Celular = "3109998877", Ciudad = "Bogotá, D.C." }, Ahora);

        cuenta.Datos.Nombre.Should().Be("Ana María Pérez");
        cuenta.Datos.Ciudad.Should().Be("Bogotá, D.C.");
        cuenta.Documento.Should().Be("1023456789");
        cuenta.Datos.Documento.Should().Be("1023456789");
        cuenta.Datos.Correo.Should().Be("ana@correo.com");
        cuenta.NombreArchivoHojaDeVida.Should().Be("hv.pdf");
    }
}
