using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Selvana.Api;
using Selvana.Core.Api;
using Selvana.Core.Infrastructure;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.Expediente.Api;
using Selvana.Expediente.Infrastructure;
using Selvana.Expediente.Infrastructure.Persistence;
using Selvana.Infrastructure.Common.Cqrs;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.Reclutamiento.Api;
using Selvana.Reclutamiento.Infrastructure;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.Vinculacion.Api;
using Selvana.Vinculacion.Infrastructure;
using Selvana.Vinculacion.Infrastructure.Persistence;
using Selvana.Web.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// Un cuerpo mal formado es un 400 con ProblemDetails, no una excepción (en Development se lanzaba y salía 500)
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
builder.Services.AddOpenApi();
builder.Services.AddSelvanaHealthChecks(builder.Configuration);
builder.Services.AddSelvanaCors(builder.Configuration);
builder.Services.AddSelvanaAuth(builder.Configuration, builder.Environment);

// Monolito modular: los módulos se hablan en proceso; con el AI.Worker se habla por HTTP con outbox e inbox (ADR 0008)
builder.Services.AddMensajeriaHttp(builder.Configuration);
builder.Services.AddCqrs(Selvana.Core.Application.AssemblyReference.Assembly, Selvana.Reclutamiento.Application.AssemblyReference.Assembly,
    Selvana.Vinculacion.Application.AssemblyReference.Assembly, Selvana.Expediente.Application.AssemblyReference.Assembly);
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddReclutamientoModule(builder.Configuration);
builder.Services.AddVinculacionModule(builder.Configuration);
builder.Services.AddExpedienteModule(builder.Configuration);

// Hojas de vida cifradas (historia 188) y portal público protegido contra abuso: 60 peticiones por minuto por IP
builder.Services.AddDataProtection().SetApplicationName("Selvana");
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(ClaveInterna.PoliticaPortalPublico, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
    // Ingreso: 10 intentos por minuto por IP, además del bloqueo de 15 minutos por usuario tras 5 fallos
    o.AddPolicy(Sesiones.PoliticaIngreso, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Portal de la persona: ingreso y registro, 10 por minuto por IP
    o.AddPolicy(SesionesPortal.PoliticaIngreso, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateCoreModuleAsync();
    await app.Services.MigrateReclutamientoModuleAsync();
    await app.Services.MigrateVinculacionModuleAsync();
    await app.Services.MigrateExpedienteModuleAsync();
}

if (app.Environment.IsDevelopment() && Guid.TryParse(app.Configuration["Desarrollo:EmpresaId"], out var empresaDesarrollo))
{
    await app.Services.SembrarDesarrolloCoreAsync(empresaDesarrollo, app.Configuration["Desarrollo:Usuario"] ?? "Laura Moreno",
        app.Configuration["Desarrollo:Contrasena"]);
}

// Datos maestros por defecto de cada empresa (idempotente): tipos de contrato
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.SembrarTiposContratoAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsSetup.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
// Swagger UI sobre el documento OpenAPI nativo: http://localhost:5000/swagger
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "Selvana API v1");
    o.DocumentTitle = "Selvana API";
});
app.MapHealthChecks("/health");

// Respuestas del AI.Worker; se autentican con la clave interna, no con el usuario
app.MapEventosEntrantes<CoreDbContext>("/internal/eventos/core");
app.MapEventosEntrantes<ReclutamientoDbContext>("/internal/eventos/reclutamiento");
app.MapEventosEntrantes<VinculacionDbContext>("/internal/eventos/vinculacion");
app.MapEventosEntrantes<ExpedienteDbContext>("/internal/eventos/expediente");
app.MapReclutamientoInterno();
app.MapVinculacionInterno();
app.MapExpedienteInterno();

var v1 = app.MapGroup("/api/v1");
v1.MapCoreEndpoints();
v1.MapUsuariosEndpoints();
v1.MapPrivacidadEndpoints();
v1.MapBandejaEndpoints();
v1.MapReclutamientoEndpoints();
v1.MapVinculacionEndpoints();
v1.MapExpedienteEndpoints();

await app.RunAsync();

/// <summary>Expuesto para las pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
