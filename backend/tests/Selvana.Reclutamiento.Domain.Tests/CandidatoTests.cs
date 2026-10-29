using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class CandidatoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static readonly AutorizacionDatos Autorizacion = new()
    {
        Version = "v3", Texto = "Autorizo el tratamiento de mis datos.", AceptadaEn = Ahora, Ip = "127.0.0.1", Navegador = "Pruebas",
    };

    private static Candidato Candidato() => Domain.Candidatos.Candidato.Registrar(
        Guid.NewGuid(), new DatosCandidato { Nombre = "Andrés Felipe Salcedo", Documento = "1.020.456.789", Celular = "3001234567" }, Ahora);

    [Fact]
    public void ElDocumentoSeGuardaSoloConDigitos()
    {
        Candidato().Documento.Should().Be("1020456789");
    }

    [Fact]
    public void PostularseSinAutorizacionDeDatosFalla()
    {
        var resultado = Candidato().Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], null, Guid.NewGuid(), [], Ahora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(CandidatoErrors.SinAutorizacion);
    }

    [Fact]
    public void PostularseRegistraLaAutorizacionYLosCamposCorregidos()
    {
        var candidato = Candidato();

        var postulacion = candidato.Postularse(Guid.NewGuid(), CanalPostulacion.Portal,
            [new RespuestaFiltro { Pregunta = "¿Turno noche?", Respuesta = true }], Autorizacion, Guid.NewGuid(), ["celular"], Ahora).Value;

        candidato.Postulaciones.Should().ContainSingle();
        postulacion.Autorizacion.Version.Should().Be("v3");
        postulacion.CamposCorregidos.Should().Equal("celular");
        postulacion.Numero.Should().MatchRegex(@"^P-2026-[0-9A-HJKMNP-TV-Z]{8}$");
    }

    [Fact]
    public void NoSePuedePostularDosVecesALaMismaVacante()
    {
        var candidato = Candidato();
        var vacanteId = Guid.NewGuid();
        candidato.Postularse(vacanteId, CanalPostulacion.Portal, [], Autorizacion, Guid.NewGuid(), [], Ahora);

        var otra = candidato.Postularse(vacanteId, CanalPostulacion.Portal, [], Autorizacion, Guid.NewGuid(), [], Ahora);

        otra.Error.Should().Be(CandidatoErrors.YaPostulado);
        candidato.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], Autorizacion, Guid.NewGuid(), [], Ahora).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("linkedin.com/in/andres-salcedo", "https://www.linkedin.com/in/andres-salcedo")]
    [InlineData("https://co.linkedin.com/in/andres-salcedo/", "https://www.linkedin.com/in/andres-salcedo")]
    [InlineData("https://www.linkedin.com/in/andres-salcedo?utm_source=share", "https://www.linkedin.com/in/andres-salcedo")]
    [InlineData("https://www.linkedin.com/company/selvana", "")]
    [InlineData("https://ejemplo.com/in/andres", "")]
    public void SoloSeGuardanPerfilesDeLinkedIn(string entrada, string esperado)
    {
        Domain.Candidatos.Candidato.NormalizarLinkedIn(entrada).Should().Be(esperado);
    }
}
