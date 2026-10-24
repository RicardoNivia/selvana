using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Selvana.AI.Agentes.Estructura;
using Selvana.AI.Agentes.Reclutamiento;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Agentes.Vinculacion;
using Selvana.AI.Gateway;
using Selvana.AI.Motor;
using Selvana.AI.Persistence;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.AI;

public static class PlataformaIaServiceCollectionExtensions
{
    public static IServiceCollection AddPlataformaIa(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Selvana")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Selvana'.");

        services.Configure<IaOptions>(configuration.GetSection(IaOptions.Section));
        services.AddDbContext<IaDbContext>(o => o.UseModulePostgres(connectionString, IaDbContext.SchemaName),
            optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<IaDbContext>(o => o.UseModulePostgres(connectionString, IaDbContext.SchemaName));
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IProveedorModelos, GeminiProveedor>();
        services.AddSingleton<IProveedorModelos, AnthropicProveedor>();
        services.AddSingleton<IClasificadorFallas, ClasificadorFallas>();
        services.AddSingleton<IRegistroUsoIa, RegistroUsoIaPostgres>();
        services.AddSingleton<IChatClient>(CrearCadena);
        services.AddSingleton<IMemoriaCasos, MemoriaCasosPostgres>();
        services.AddSingleton<IRegistroAccionesAgente, RegistroAccionesAgentePostgres>();
        services.AddSingleton<IMotorAgentes, MotorAgentes>();

        services.Configure<RedactorVacantesOptions>(configuration.GetSection(RedactorVacantesOptions.Section));
        services.AddSingleton<IFiltroVariablesProtegidas, FiltroVariablesProtegidas>();
        services.AddSingleton<IRedactorVacantes, RedactorVacantesAgente>();
        services.AddSingleton<IOrganizadorCargos, OrganizadorCargosAgente>();
        services.AddSingleton<IRedactorPerfilCargo, RedactorPerfilCargoAgente>();
        services.AddSingleton<ILectorHojasDeVida, LectorHojasDeVidaAgente>();
        services.AddSingleton<ICalificadorAfinidad, CalificadorAfinidadAgente>();
        services.AddSingleton<IAgentePreseleccion, PreseleccionAgente>();
        services.AddSingleton<IRedactorOferta, RedactorOfertaAgente>();
        services.AddSingleton<ILectorDocumentosIngreso, LectorDocumentosAgente>();
        services.AddHttpClient("archivos-reclutamiento", c => c.Timeout = TimeSpan.FromSeconds(30));

        return services;
    }

    private static CadenaModelosChatClient CrearCadena(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<IaOptions>>();
        var proveedores = sp.GetServices<IProveedorModelos>().ToDictionary(p => p.Nombre, StringComparer.OrdinalIgnoreCase);

        if (options.Value.Cadena.Count == 0)
        {
            throw new InvalidOperationException("La sección 'Ia:Cadena' no tiene modelos.");
        }

        var eslabones = options.Value.Cadena
            .Select(e => proveedores.TryGetValue(e.Proveedor, out var proveedor)
                ? new EslabonCadena(e, proveedor.CrearCliente(e.Modelo))
                : throw new InvalidOperationException($"Proveedor de IA desconocido: '{e.Proveedor}'."))
            .ToList();

        return new CadenaModelosChatClient(
            eslabones,
            sp.GetRequiredService<IRegistroUsoIa>(),
            sp.GetRequiredService<IClasificadorFallas>(),
            options,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<CadenaModelosChatClient>>());
    }
}
