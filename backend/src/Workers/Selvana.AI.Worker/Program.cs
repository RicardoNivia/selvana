using Microsoft.EntityFrameworkCore;
using Selvana.AI;
using Selvana.AI.Persistence;
using Selvana.AI.Worker;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.Web.Common;

// Modo demo: dotnet run --project src/Workers/Selvana.AI.Worker -- probar-vacante "necesito 25 auxiliares de bodega en Funza"
if (args is ["probar-vacante", ..])
{
    var demo = Host.CreateApplicationBuilder([]);
    demo.Services.AddPlataformaIa(demo.Configuration);
    using var host = demo.Build();
    return await PruebaRedactorVacantes.EjecutarAsync(host.Services, string.Join(' ', args.Skip(1)));
}

// Demo del motor de agentes: dotnet run --project src/Workers/Selvana.AI.Worker -- probar-agente ["<mensaje>"]
if (args is ["probar-agente", ..])
{
    var demo = Host.CreateApplicationBuilder([]);
    demo.Services.AddPlataformaIa(demo.Configuration);
    using var host = demo.Build();
    await using (var db = await host.Services.GetRequiredService<IDbContextFactory<IaDbContext>>().CreateDbContextAsync())
    {
        await db.Database.MigrateAsync();
    }

    return await PruebaMotorAgentes.EjecutarAsync(host.Services, string.Join(' ', args.Skip(1)));
}

// Banco de pruebas: dotnet run --project src/Workers/Selvana.AI.Worker -- evaluar [lector|redactor|todo] [--max N | --completo] (por defecto, 3 casos por agente)
if (args is ["evaluar", ..])
{
    var evaluacion = Host.CreateApplicationBuilder([]);
    evaluacion.Services.AddPlataformaIa(evaluacion.Configuration);
    using var host = evaluacion.Build();
    return await EjecutorEvaluacion.EjecutarAsync(host.Services, args);
}

// AI.Worker: recibe eventos por HTTP en su inbox, los agentes trabajan en segundo plano
// y las respuestas vuelven a la API por el outbox (ADR 0008).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlataformaIa(builder.Configuration);
builder.Services.AddMensajeriaHttp(builder.Configuration);
builder.Services.AddIntegrationEventConsumers<IaDbContext>(typeof(PlataformaIaServiceCollectionExtensions).Assembly);
builder.Services.AddHostedService<InboxProcessor<IaDbContext>>();
builder.Services.AddHostedService<OutboxProcessor<IaDbContext>>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IaDbContext>().Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok("Healthy"));
app.MapEventosEntrantes<IaDbContext>("/eventos");

await app.RunAsync();
return 0;
