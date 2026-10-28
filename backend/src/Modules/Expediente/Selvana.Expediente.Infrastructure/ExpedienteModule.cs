using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Expediente.Application;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Infrastructure.Persistence;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Expediente.Infrastructure;

public static class ExpedienteModule
{
    /// <summary>Registra el módulo Expediente (empleados): DbContext en el esquema "expediente", puertos, outbox e inbox. CQRS lo registra el host.</summary>
    public static IServiceCollection AddExpedienteModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Selvana")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Selvana'.");

        services.AddDbContext<ExpedienteDbContext>(o => o.UseModulePostgres(connectionString, ExpedienteDbContext.SchemaName));
        services.AddScoped<IUnidadExpediente>(sp => sp.GetRequiredService<ExpedienteDbContext>());
        services.AddScoped<IEmpleados, EmpleadosRepositorio>();
        services.AddScoped<ILecturaEmpleados, LecturaEmpleados>();
        services.AddScoped<IEventosExpediente, EventosExpediente>();
        services.AddScoped<IBitacora, Bitacora>();
        services.AddScoped<IAlmacenArchivos, AlmacenArchivos>();

        services.AddIntegrationEventConsumers<ExpedienteDbContext>(AssemblyReference.Assembly);
        services.AddHostedService<OutboxProcessor<ExpedienteDbContext>>();
        services.AddHostedService<InboxProcessor<ExpedienteDbContext>>();
        return services;
    }

    public static async Task MigrateExpedienteModuleAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ExpedienteDbContext>().Database.MigrateAsync();
    }
}
