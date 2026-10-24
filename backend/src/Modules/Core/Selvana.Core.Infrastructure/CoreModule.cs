using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Bandeja;
using Selvana.Core.Application.Estructura;
using Selvana.Core.Application.Privacidad;
using Selvana.Core.Application.Usuarios;
using Selvana.Core.Contracts.Empresas;
using Selvana.Core.Contracts.Estructura;
using Selvana.Core.Infrastructure.Bandeja;
using Selvana.Core.Infrastructure.Empresas;
using Selvana.Core.Infrastructure.Estructura;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.Core.Infrastructure.Privacidad;
using Selvana.Core.Infrastructure.Sesion;
using Selvana.Core.Infrastructure.Usuarios;
using Selvana.Infrastructure.Common.Cqrs;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.Infrastructure.Common.Persistence;
using Selvana.Integrations.Nomina;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Infrastructure;

public static class CoreModule
{
    /// <summary>Registra el módulo Core: DbContext en el esquema "core", outbox e inbox. CQRS lo registra el host para todos los módulos.</summary>
    public static IServiceCollection AddCoreModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Selvana")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Selvana'.");

        services.AddDbContext<CoreDbContext>(o => o.UseModulePostgres(connectionString, CoreDbContext.SchemaName));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CoreDbContext>());

        services.AddScoped<IEstructuras, EstructurasRepositorio>();
        services.AddScoped<IEventosSalientes, EventosSalientes>();
        services.AddScoped<ILecturaEstructura, LecturaEstructura>();
        services.AddScoped<ILectorNomina, LectorNomina>();
        services.AddSingleton<IConectorNomina, ArchivoCsvConector>();
        services.AddScoped<IDirectorioEmpresas, DirectorioEmpresas>();
        services.AddScoped<ICatalogoCargos, CatalogoCargos>();
        services.AddScoped<IUsuarios, UsuariosRepositorio>();
        services.AddScoped<IEmpresas, EmpresasRepositorio>();
        services.AddScoped<ILecturaUsuarios, LecturaUsuarios>();
        services.AddSingleton<IGeneradorTokens, GeneradorTokens>();
        services.AddScoped<IBitacora, BitacoraCore>();
        services.AddScoped<ISolicitudesTitular, SolicitudesTitularRepositorio>();
        services.AddScoped<ILecturaPrivacidad, LecturaPrivacidad>();
        services.AddScoped<IPropuestas, PropuestasRepositorio>();
        services.AddScoped<ILecturaBandeja, LecturaBandeja>();
        services.AddScoped<IBandeja, BandejaAprobaciones>();
        services.AddScoped<ILecturaSesion, LecturaSesion>();
        services.AddSingleton<IHasherContrasenas, HasherPbkdf2>();

        services.AddIntegrationEventConsumers<CoreDbContext>(Application.AssemblyReference.Assembly);
        services.AddHostedService<OutboxProcessor<CoreDbContext>>();
        services.AddHostedService<InboxProcessor<CoreDbContext>>();

        return services;
    }

    public static async Task MigrateCoreModuleAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CoreDbContext>().Database.MigrateAsync();
    }

    /// <summary>Solo en desarrollo: crea la empresa de ejemplo y sus usuarios si no existen, con la contraseña de los user-secrets.</summary>
    public static async Task SembrarDesarrolloCoreAsync(this IServiceProvider services, Guid empresaId, string usuario, string? contrasena)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await SemillaDesarrollo.SembrarAsync(sp.GetRequiredService<CoreDbContext>(), empresaId, usuario, contrasena,
            sp.GetRequiredService<IHasherContrasenas>(), sp.GetRequiredService<TimeProvider>());
    }
}
