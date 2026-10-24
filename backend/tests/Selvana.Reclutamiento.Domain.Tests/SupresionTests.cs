using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class SupresionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static AutorizacionDatos Autorizacion(string documento) => new()
    {
        Version = "v3", Texto = "Autorizo…", AceptadaEn = Ahora, Ip = "127.0.0.1", Navegador = "Pruebas",
        Huella = AutorizacionDatos.CalcularHuella("v3", "Autorizo…", Ahora, "127.0.0.1", "Pruebas", documento),
    };

    private static Candidato ConPostulacion(Guid archivo)
    {
        var c = Candidato.Registrar(Guid.NewGuid(), new DatosCandidato { Nombre = "Sara Gómez", Documento = "1020330330", Celular = "3001234567" }, Ahora);
        c.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [new RespuestaFiltro { Pregunta = "¿Turno noche?", Respuesta = true }],
            Autorizacion("1020330330"), archivo, ["celular"], Ahora);
        return c;
    }

    [Fact]
    public void SuprimirBorraLosDatosYDevuelveLasHojasDeVida()
    {
        var archivo = Guid.NewGuid();
        var c = ConPostulacion(archivo);

        var archivos = c.Suprimir(Ahora.AddDays(5));

        archivos.Should().Equal(archivo);
        c.Suprimido.Should().BeTrue();
        c.Datos.Celular.Should().BeEmpty();
        c.Datos.Nombre.Should().NotContain("Sara");
        c.Postulaciones[0].Respuestas.Should().BeEmpty();
        c.Postulaciones[0].ArchivoHojaDeVidaId.Should().Be(Guid.Empty);
        // La constancia queda: documento, número y versión aceptada
        c.Documento.Should().Be("1020330330");
        c.Postulaciones[0].Autorizacion.Version.Should().Be("v3");
    }

    [Fact]
    public void VolverAPostularseEsUnaNuevaAutorizacion()
    {
        var c = ConPostulacion(Guid.NewGuid());
        c.Suprimir(Ahora);

        c.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], Autorizacion("1020330330"), Guid.NewGuid(), [], Ahora.AddDays(30)).IsSuccess.Should().BeTrue();

        c.Suprimido.Should().BeFalse();
    }

    [Fact]
    public void LaHuellaDeLaAutorizacionCambiaSiCambiaCualquierDato()
    {
        var a = AutorizacionDatos.CalcularHuella("v3", "Autorizo…", Ahora, "127.0.0.1", "Pruebas", "1020330330");

        a.Should().HaveLength(64);
        AutorizacionDatos.CalcularHuella("v3", "Autorizo…", Ahora, "127.0.0.1", "Pruebas", "1020330331").Should().NotBe(a);
        AutorizacionDatos.CalcularHuella("v2", "Autorizo…", Ahora, "127.0.0.1", "Pruebas", "1020330330").Should().NotBe(a);
    }
}
