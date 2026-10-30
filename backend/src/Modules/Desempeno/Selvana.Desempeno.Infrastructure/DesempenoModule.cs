using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Desempeno.Application;
using Selvana.Desempeno.Application.Abstracciones;
using Selvana.Desempeno.Infrastructure.Persistence;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Desempeno.Infrastructure;

public static class DesempenoModule
{
    /// <summary>Registra el módulo Desempeño: DbContext en el esquema "desempeno", puertos, outbox e inbox. CQRS lo registra el host.</summary>
    public static IServiceCollection AddDesempenoModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Selvana")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Selvana'.");

        services.AddDbContext<DesempenoDbContext>(o => o.UseModulePostgres(connectionString, DesempenoDbContext.SchemaName));
        services.AddScoped<IUnidadDesempeno>(sp => sp.GetRequiredService<DesempenoDbContext>());
        services.AddScoped<ICiclos, CiclosRepositorio>();
        services.AddScoped<IEvaluaciones, EvaluacionesRepositorio>();
        services.AddScoped<ILecturaDesempeno, LecturaDesempeno>();
        services.AddScoped<IEventosDesempeno, EventosDesempeno>();
        services.AddScoped<IBitacora, Bitacora>();
        services.AddScoped<Contracts.IDesempenoReciente, DesempenoRecienteLectura>();
        services.AddScoped<Contracts.IEvaluacionesDelEquipo, EvaluacionesDelEquipoLectura>();
        services.AddScoped<Contracts.IEvaluacionesDeLaPersona, EvaluacionesDeLaPersona>();

        services.AddIntegrationEventConsumers<DesempenoDbContext>(AssemblyReference.Assembly);
        services.AddHostedService<OutboxProcessor<DesempenoDbContext>>();
        services.AddHostedService<InboxProcessor<DesempenoDbContext>>();
        return services;
    }

    /// <summary>Ambiente de pruebas en línea: datos de ejemplo de desempeño sobre la planta activa, sin IA (idempotente).</summary>
    public static async Task SembrarEjemploDesempenoAsync(this IServiceProvider services, IReadOnlyList<Core.Contracts.Empresas.EmpresaResumen> empresas, string porRrhh)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await Ejemplo.SemillaEjemplo.SembrarAsync(sp.GetRequiredService<DesempenoDbContext>(), sp.GetRequiredService<Expediente.Contracts.IPlantaActiva>(), empresas, porRrhh,
            sp.GetRequiredService<TimeProvider>());
    }

    public static async Task MigrateDesempenoModuleAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DesempenoDbContext>().Database.MigrateAsync();
    }
}
