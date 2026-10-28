using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.Infrastructure.Common.Persistence;
using Selvana.Reclutamiento.Application;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Candidatos;
using Selvana.Reclutamiento.Application.Filtros;
using Selvana.Reclutamiento.Application.Preseleccion;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Application.Ranking;
using Selvana.Reclutamiento.Application.Recontacto;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.Reclutamiento.Infrastructure.Preseleccion;
using Selvana.Reclutamiento.Infrastructure.Pruebas;
using Selvana.Reclutamiento.Infrastructure.Recontacto;

namespace Selvana.Reclutamiento.Infrastructure;

public static class ReclutamientoModule
{
    /// <summary>Registra el módulo Reclutamiento: DbContext en el esquema "reclutamiento", puertos, outbox e inbox. CQRS lo registra el host.</summary>
    public static IServiceCollection AddReclutamientoModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Selvana")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Selvana'.");

        services.AddDbContext<ReclutamientoDbContext>(o => o.UseModulePostgres(connectionString, ReclutamientoDbContext.SchemaName));
        services.AddScoped<IUnidadReclutamiento>(sp => sp.GetRequiredService<ReclutamientoDbContext>());
        services.AddScoped<IVacantes, VacantesRepositorio>();
        services.AddScoped<ICandidatos, CandidatosRepositorio>();
        services.AddScoped<IBorradoresPostulacion, BorradoresRepositorio>();
        services.AddScoped<IEventosReclutamiento, EventosReclutamiento>();
        services.AddScoped<IAlmacenHojasDeVida, AlmacenHojasDeVida>();
        services.AddScoped<ILecturaReclutamiento, LecturaReclutamiento>();
        services.AddScoped<IBitacora, Bitacora>();
        services.AddScoped<IBateriasCargo, BateriasCargoRepositorio>();
        services.AddScoped<IAplicacionesPruebas, AplicacionesPruebasRepositorio>();
        services.AddScoped<ILecturaPruebas, LecturaPruebas>();
        services.AddSingleton<ITokensPruebas, TokensPruebas>();
        services.AddScoped<IDatosTitulares, DatosTitulares>();
        services.AddScoped<ILecturaCandidatos, LecturaCandidatos>();
        services.AddScoped<ILecturaTablero, LecturaTablero>();
        services.AddScoped<ICalibracionRanking, CalibracionRanking>();
        services.AddScoped<IConversaciones, ConversacionesRepositorio>();
        services.AddScoped<IPostulacionesContratables, PostulacionesContratables>();
        services.AddScoped<ILecturaFiltros, LecturaFiltros>();
        services.AddScoped<ISugerenciasBanco, SugerenciasBancoRepositorio>();
        services.AddScoped<IBancoTalento, BancoTalento>();
        services.AddScoped<ILecturaRecontacto, LecturaRecontacto>();

        services.AddIntegrationEventConsumers<ReclutamientoDbContext>(AssemblyReference.Assembly);
        services.AddHostedService<OutboxProcessor<ReclutamientoDbContext>>();
        services.AddHostedService<InboxProcessor<ReclutamientoDbContext>>();
        return services;
    }

    /// <summary>
    /// Ambiente de pruebas en línea: vacantes, candidatos y postulaciones de ejemplo en cada empresa (si aún no tiene vacantes), sin IA.
    /// Devuelve las postulaciones de la terna que deben seguir a Vinculación y hasta qué paso.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid EmpresaId, PostulanteContratable Postulante, string Avance)>> SembrarEjemploReclutamientoAsync(
        this IServiceProvider services, IReadOnlyList<Core.Contracts.Empresas.EmpresaResumen> empresas, string nombreDueno)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        return await Ejemplo.SemillaEjemplo.SembrarAsync(sp.GetRequiredService<ReclutamientoDbContext>(), sp.GetRequiredService<IAlmacenHojasDeVida>(),
            sp.GetRequiredService<ITokensPruebas>(), empresas, nombreDueno, sp.GetRequiredService<TimeProvider>());
    }

    public static async Task MigrateReclutamientoModuleAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReclutamientoDbContext>();
        await db.Database.MigrateAsync();

        // Candidatos de antes del banco de talento: se les arma el texto de búsqueda una sola vez
        var pendientes = await db.Set<Candidato>().Where(c => c.TextoBusqueda == "" && c.AutorizacionRevocadaEn == null).ToListAsync();
        foreach (var candidato in pendientes)
        {
            candidato.RecalcularBusqueda();
        }

        if (pendientes.Count > 0)
        {
            await db.SaveChangesAsync();
        }
    }
}
