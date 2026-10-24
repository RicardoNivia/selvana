using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Selvana.Core.Application.Estado.Obtener;

namespace Selvana.Core.IntegrationTests;

/// <summary>Host sin migraciones ni semilla: estas pruebas no necesitan PostgreSQL.</summary>
public sealed class ApiSinBaseDeDatos : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Desarrollo:EmpresaId", "");
    }
}

public class ApiTests(ApiSinBaseDeDatos factory) : IClassFixture<ApiSinBaseDeDatos>
{
    [Fact]
    public async Task EstadoDeCoreResponde()
    {
        using var client = factory.CreateClient();

        var estado = await client.GetFromJsonAsync<EstadoDto>("/api/v1/core/estado");

        estado.Should().NotBeNull();
        estado!.Modulo.Should().Be("Core");
    }

    [Theory]
    [InlineData("/api/v1/sesion")]
    [InlineData("/api/v1/core/empresa")]
    [InlineData("/api/v1/reclutamiento/vacantes")]
    [InlineData("/api/v1/core/usuarios")]
    [InlineData("/api/v1/core/empresas-usuarias")]
    [InlineData("/api/v1/core/privacidad/bitacora")]
    [InlineData("/api/v1/reclutamiento/autorizaciones")]
    public async Task LaConsolaExigeSesion(string ruta)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(ruta, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublicaElDocumentoOpenApi()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("/api/v1/core/estado");
    }

    [Fact]
    public async Task CorsPermiteElFrontendDeDesarrollo()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("http://localhost:5173"));

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle("http://localhost:5173");
    }

    [Fact]
    public async Task CorsRechazaOrigenesNoConfigurados()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("https://sitio-no-autorizado.com"));

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/core/estado");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }
}
