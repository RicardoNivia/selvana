using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Portal;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class CuentaCandidatoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 28, 8, 0, 0, TimeSpan.Zero);
    private static readonly AutorizacionDatos Autorizacion = new() { Version = "portal-v1", Texto = "Autorizo", AceptadaEn = Ahora };

    private static DatosCandidato Datos(string documento = "1.023.456.789") => new() { Nombre = " Ana Pérez ", Documento = documento, Celular = "3001234567", Ciudad = "Funza" };

    [Fact]
    public void SeRegistraConCorreoNormalizadoYCedulaSoloConDigitos()
    {
        var cuenta = CuentaCandidato.Registrar(" Ana.Perez@Correo.COM ", "hash", Datos(), Autorizacion, null, "", Ahora).Value;

        cuenta.Correo.Should().Be("ana.perez@correo.com");
        cuenta.Documento.Should().Be("1023456789");
        cuenta.Datos.Correo.Should().Be("ana.perez@correo.com");
        cuenta.Datos.Nombre.Should().Be("Ana Pérez");
        cuenta.ArchivoHojaDeVidaId.Should().BeNull();
    }

    [Fact]
    public void NoSeRegistraSinCorreoValidoSinCedulaOSinAutorizacion()
    {
        CuentaCandidato.Registrar("ana@", "hash", Datos(), Autorizacion, null, "", Ahora).Error.Should().Be(CuentaErrors.CorreoInvalido);
        CuentaCandidato.Registrar("ana@correo.com", "hash", Datos("12"), Autorizacion, null, "", Ahora).Error.Should().Be(CuentaErrors.DocumentoInvalido);
        CuentaCandidato.Registrar("ana@correo.com", "hash", Datos(), null, null, "", Ahora).Error.Should().Be(CuentaErrors.SinAutorizacion);
    }

    [Fact]
    public void CincoIntentosFallidosBloqueanQuinceMinutos()
    {
        var cuenta = CuentaCandidato.Registrar("ana@correo.com", "hash", Datos(), Autorizacion, null, "", Ahora).Value;

        for (var i = 0; i < CuentaCandidato.IntentosAntesDeBloquear; i++)
        {
            cuenta.Ingresar(contrasenaValida: false, Ahora).Error.Should().Be(CuentaErrors.CredencialesInvalidas);
        }

        cuenta.Ingresar(contrasenaValida: true, Ahora.AddMinutes(5)).Error.Should().Be(CuentaErrors.Bloqueada);
        cuenta.Ingresar(contrasenaValida: true, Ahora.AddMinutes(16)).IsSuccess.Should().BeTrue();
        cuenta.UltimoIngreso.Should().Be(Ahora.AddMinutes(16));
    }

    [Fact]
    public void ActualizarDatosNoCambiaElCorreoNiLaCedulaQueUnenLasPostulaciones()
    {
        var cuenta = CuentaCandidato.Registrar("ana@correo.com", "hash", Datos(), Autorizacion, null, "", Ahora).Value;

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
