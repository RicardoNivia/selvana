using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Portal;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class MisDocumentosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 29);

    private static CuentaCandidato Cuenta() => CuentaCandidato.Registrar("ana@correo.com", "hash", "Ana Pérez", "CC", "1023456789",
        new AutorizacionDatos { Version = "portal-v1", Texto = "Autorizo", AceptadaEn = Ahora }, Ahora).Value;

    [Fact]
    public void GuardaUnoPorTipoYAlReemplazarDevuelveElArchivoAnterior()
    {
        var c = Cuenta();
        var primero = Guid.NewGuid();
        c.GuardarDocumento("Cedula", primero, "cedula.pdf", "application/pdf", null, Ahora).Value.ArchivoAnterior.Should().BeNull();

        var r = c.GuardarDocumento("Cedula", Guid.NewGuid(), "cedula-nueva.pdf", "application/pdf", null, Ahora).Value;

        r.ArchivoAnterior.Should().Be(primero, "el archivo viejo se borra");
        c.Documentos.Should().ContainSingle().Which.NombreArchivo.Should().Be("cedula-nueva.pdf");
    }

    [Fact]
    public void UnTipoQueNoExisteFalla()
    {
        Cuenta().GuardarDocumento("Pasado judicial", Guid.NewGuid(), "x.pdf", "application/pdf", null, Ahora).Error.Should().Be(DocumentosPersonaErrors.TipoInvalido);
    }

    [Fact]
    public void UnDocumentoVencidoNoSeEnvia()
    {
        var c = Cuenta();
        c.GuardarDocumento("CertificadoBancario", Guid.NewGuid(), "banco.pdf", "application/pdf", new DateOnly(2026, 10, 1), Ahora);

        c.DocumentoParaEnviar("CertificadoBancario", Hoy).Error.Should().Be(DocumentosPersonaErrors.Vencido);
        c.DocumentoParaEnviar("Cedula", Hoy).Error.Should().Be(DocumentosPersonaErrors.NoExiste);
    }

    [Fact]
    public void ElEnvioQuedaRegistradoConLaEmpresa()
    {
        var c = Cuenta();
        var d = c.GuardarDocumento("Cedula", Guid.NewGuid(), "cedula.pdf", "application/pdf", null, Ahora).Value.Documento;

        c.RegistrarEnvio(d.Id, Guid.NewGuid(), "Logística del Occidente", Ahora);

        c.Documentos.Single().Envios.Should().ContainSingle().Which.Empresa.Should().Be("Logística del Occidente");
    }

    [Fact]
    public void QuitarDevuelveElArchivoParaBorrarlo()
    {
        var c = Cuenta();
        var archivo = Guid.NewGuid();
        var d = c.GuardarDocumento("Cedula", archivo, "cedula.pdf", "application/pdf", null, Ahora).Value.Documento;

        c.QuitarDocumento(d.Id).Value.Should().Be(archivo);
        c.Documentos.Should().BeEmpty();
    }

    [Fact]
    public void CompartirLaHojaDeVidaEsPorEmpresaYSeRetira()
    {
        var c = Cuenta();
        var empresa = Guid.NewGuid();

        c.CompartirHojaDeVida(empresa, true);
        c.CompartirHojaDeVida(empresa, true);
        c.HojaDeVidaCompartidaCon.Should().ContainSingle().Which.Should().Be(empresa);

        c.CompartirHojaDeVida(empresa, false);
        c.HojaDeVidaCompartidaCon.Should().BeEmpty();
    }
}
