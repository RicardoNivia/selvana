using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Clima.Application;
using Selvana.Clima.Application.Abstracciones;
using Selvana.Clima.Infrastructure.Persistence;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Clima.Infrastructure;

public static class ClimaModule
{
    /// <summary>Registra el módulo Clima: DbContext en el esquema "clima", puertos, outbox, inbox y el programador de pulsos.</summary>
    public static IServiceCollection AddClimaModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Selvana")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Selvana'.");

        services.AddDbContext<ClimaDbContext>(o => o.UseModulePostgres(connectionString, ClimaDbContext.SchemaName));
        services.AddScoped<IUnidadClima>(sp => sp.GetRequiredService<ClimaDbContext>());
        services.AddScoped<IEncuestas, EncuestasRepositorio>();
        services.AddScoped<IPulsos, PulsosRepositorio>();
        services.AddScoped<ILecturaClima, LecturaClima>();
        services.AddSingleton<ITokensEncuesta, TokensEncuesta>();
        services.AddScoped<IEventosClima, EventosClima>();
        services.AddScoped<IBitacora, Bitacora>();
        services.AddScoped<Contracts.IPulsosDeLaPersona, PulsosDeLaPersona>();
        services.AddScoped<Application.Pulsos.CalculoRiesgoRotacion>();
        services.AddScoped<Contracts.IRiesgoDelEquipo>(sp => sp.GetRequiredService<Application.Pulsos.CalculoRiesgoRotacion>());
        services.AddHostedService<ProgramadorPulsos>();

        services.AddIntegrationEventConsumers<ClimaDbContext>(AssemblyReference.Assembly);
        services.AddHostedService<OutboxProcessor<ClimaDbContext>>();
        services.AddHostedService<InboxProcessor<ClimaDbContext>>();
        return services;
    }

    /// <summary>Ambiente de pruebas en línea: datos de ejemplo de clima sobre la planta activa, sin IA (idempotente).</summary>
    public static async Task SembrarEjemploClimaAsync(this IServiceProvider services, IReadOnlyList<Core.Contracts.Empresas.EmpresaResumen> empresas, string porRrhh)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await Ejemplo.SemillaEjemplo.SembrarAsync(sp.GetRequiredService<ClimaDbContext>(), sp.GetRequiredService<Expediente.Contracts.IPlantaActiva>(), empresas, porRrhh,
            sp.GetRequiredService<TimeProvider>());
    }

    public static async Task MigrateClimaModuleAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClimaDbContext>().Database.MigrateAsync();
    }
}
