using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.Infrastructure.Common.Persistence;
using Selvana.Vinculacion.Application;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Application.Alta;
using Selvana.Vinculacion.Application.Documentos;
using Selvana.Vinculacion.Contracts;
using Selvana.Vinculacion.Infrastructure.Persistence;

namespace Selvana.Vinculacion.Infrastructure;

public static class VinculacionModule
{
    /// <summary>Registra el módulo Vinculación: DbContext en el esquema "vinculacion", puertos, outbox e inbox. CQRS lo registra el host.</summary>
    public static IServiceCollection AddVinculacionModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Selvana")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Selvana'.");

        services.Configure<VinculacionOptions>(configuration.GetSection(VinculacionOptions.Section));
        services.AddDbContext<VinculacionDbContext>(o => o.UseModulePostgres(connectionString, VinculacionDbContext.SchemaName));
        services.AddScoped<IUnidadVinculacion>(sp => sp.GetRequiredService<VinculacionDbContext>());
        services.AddScoped<IProcesos, ProcesosRepositorio>();
        services.AddScoped<IProcesosDeLaPersona, ProcesosDeLaPersona>();
        services.AddScoped<IEventosVinculacion, EventosVinculacion>();
        services.AddScoped<IBitacora, Bitacora>();
        services.AddScoped<ILecturaVinculacion, LecturaVinculacion>();
        services.AddSingleton<ITokensEnlace, TokensEnlace>();
        services.AddSingleton<IParametrosLegales, ParametrosLegalesConfigurados>();
        services.AddScoped<IAlmacenDocumentos, AlmacenDocumentos>();
        services.AddSingleton<ICifradoSensible, CifradoSensible>();
        services.AddSingleton<IArchivoAlta, ArchivoAltaCsv>();

        services.AddIntegrationEventConsumers<VinculacionDbContext>(AssemblyReference.Assembly);
        services.AddHostedService<OutboxProcessor<VinculacionDbContext>>();
        services.AddHostedService<InboxProcessor<VinculacionDbContext>>();
        return services;
    }

    public static async Task MigrateVinculacionModuleAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<VinculacionDbContext>().Database.MigrateAsync();
    }
}
