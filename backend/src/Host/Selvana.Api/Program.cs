using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Selvana.Api;
using Selvana.Core.Api;
using Selvana.Core.Infrastructure;
using Selvana.Core.Infrastructure.Empresas;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.Clima.Api;
using Selvana.Clima.Infrastructure;
using Selvana.Clima.Infrastructure.Persistence;
using Selvana.Desempeno.Api;
using Selvana.Desempeno.Infrastructure;
using Selvana.Desempeno.Infrastructure.Persistence;
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
    Selvana.Vinculacion.Application.AssemblyReference.Assembly, Selvana.Expediente.Application.AssemblyReference.Assembly,
    Selvana.Desempeno.Application.AssemblyReference.Assembly, Selvana.Clima.Application.AssemblyReference.Assembly);
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddReclutamientoModule(builder.Configuration);
builder.Services.AddVinculacionModule(builder.Configuration);
builder.Services.AddExpedienteModule(builder.Configuration);
builder.Services.AddDesempenoModule(builder.Configuration);
builder.Services.AddClimaModule(builder.Configuration);

// Hojas de vida cifradas (historia 188) y portal público protegido contra abuso: 60 peticiones por minuto por IP
var proteccion = builder.Services.AddDataProtection().SetApplicationName("Selvana");
// En contenedores las llaves van a un volumen: si se pierden, se cierran las sesiones y los archivos cifrados quedan ilegibles
if (builder.Configuration["DataProtection:RutaLlaves"] is { Length: > 0 } rutaLlaves)
{
    proteccion.PersistKeysToFileSystem(new DirectoryInfo(rutaLlaves));
}

// Detrás del proxy (Caddy) la IP real llega en X-Forwarded-For; sin esto el límite de peticiones sería uno solo para todos
if (builder.Configuration.GetValue<bool>("Proxy:ConfiarEncabezados"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // La API solo es alcanzable por la red interna de Docker, a través del proxy
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}
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
    await app.Services.MigrateDesempenoModuleAsync();
    await app.Services.MigrateClimaModuleAsync();
}

// El ambiente de pruebas en línea también lleva la empresa de ejemplo, con la contraseña desde sus secretos
if ((app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Pruebas")) && Guid.TryParse(app.Configuration["Desarrollo:EmpresaId"], out var empresaDesarrollo))
{
    var propio = app.Configuration.GetSection("Desarrollo:UsuarioPropio");
    await app.Services.SembrarDesarrolloCoreAsync(empresaDesarrollo, app.Configuration["Desarrollo:Usuario"] ?? "Laura Moreno",
        app.Configuration["Desarrollo:Contrasena"],
        string.IsNullOrWhiteSpace(propio["Correo"]) || string.IsNullOrWhiteSpace(propio["Contrasena"]) ? null
            : new UsuarioPropioDesarrollo(propio["Correo"]!, propio["Nombre"] ?? propio["Correo"]!, propio["Contrasena"]!));
}

// Datos de ejemplo del flujo completo (tres empresas), sin IA: los pide el ambiente de pruebas en línea con Ejemplo:Sembrar
if (app.Configuration.GetValue<bool>("Ejemplo:Sembrar"))
{
    var ejemplo = app.Configuration.GetSection("Ejemplo");
    var dueno = ejemplo["NombreDueno"] ?? "Dueño del ambiente";
    var empresas = await app.Services.SembrarEjemploCoreAsync(new AccesoEjemplo(ejemplo["CorreoDueno"] ?? "", dueno,
        ejemplo["ContrasenaDueno"] ?? "", ejemplo["ContrasenaFicticios"] ?? ""));
    var contratables = await app.Services.SembrarEjemploReclutamientoAsync(empresas, dueno);
    // Más volumen: diez vacantes con postulantes en todas las etapas, la planta de cada empresa y cuentas en el portal de la persona
    var ampliada = await app.Services.SembrarEjemploAmpliadoReclutamientoAsync(empresas, dueno);
    await app.Services.SembrarEjemploVinculacionAsync([.. contratables, .. ampliada.Contratables], "Valentina Ríos");
    var planta = await app.Services.SembrarEjemploExpedienteAsync(empresas, "Valentina Ríos");
    await app.Services.SembrarCuentasPortalEjemploAsync([.. ampliada.Personas, .. planta], ejemplo["ContrasenaFicticios"] ?? "");
    // Desempeño y clima sobre la planta: ciclos con evaluaciones y 9-box, encuesta con respuestas y pulsos de ingreso
    await app.Services.SembrarEjemploDesempenoAsync(empresas, "Valentina Ríos");
    await app.Services.SembrarEjemploClimaAsync(empresas, "Valentina Ríos");
}

// Datos maestros por defecto de cada empresa (idempotente): tipos de contrato
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.SembrarTiposContratoAsync();
}

if (app.Configuration.GetValue<bool>("Proxy:ConfiarEncabezados"))
{
    app.UseForwardedHeaders();
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
app.MapEventosEntrantes<DesempenoDbContext>("/internal/eventos/desempeno");
app.MapEventosEntrantes<ClimaDbContext>("/internal/eventos/clima");
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
v1.MapDesempenoEndpoints();
v1.MapClimaEndpoints();
v1.MapClimaPublico();

await app.RunAsync();

/// <summary>Expuesto para las pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
