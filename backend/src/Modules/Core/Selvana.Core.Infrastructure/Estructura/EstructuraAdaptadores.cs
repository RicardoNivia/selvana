using Microsoft.EntityFrameworkCore;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Domain.Estructura;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.Integrations.Nomina;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Infrastructure.Estructura;

internal static class ConsultasEstructura
{
    public static IQueryable<EstructuraEmpresa> ConTodo(this IQueryable<EstructuraEmpresa> query) => query
        .Include(e => e.Sedes).Include(e => e.Areas).Include(e => e.CentrosCosto).Include(e => e.Cargos)
        .Include(e => e.Importaciones).ThenInclude(i => i.Propuestas)
        .AsSplitQuery();
}

internal sealed class EstructurasRepositorio(CoreDbContext db) : IEstructuras
{
    public Task<EstructuraEmpresa?> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken) =>
        db.Set<EstructuraEmpresa>().ConTodo().FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);

    public void Agregar(EstructuraEmpresa estructura) => db.Add(estructura);
}

internal sealed class EventosSalientes(CoreDbContext db) : IEventosSalientes
{
    public void Publicar(IIntegrationEvent evento) => db.AddIntegrationEvent(evento);
}

/// <summary>Adapta el contrato canónico de Selvana.Integrations al dominio. Hoy: archivo CSV (programado o carga manual).</summary>
internal sealed class LectorNomina(IConectorNomina conector) : ILectorNomina
{
    public async Task<LecturaArchivoNomina> LeerAsync(Stream contenido, CancellationToken cancellationToken)
    {
        var lectura = await conector.LeerAsync(contenido, cancellationToken);
        return new LecturaArchivoNomina(
            [.. lectura.Empleados.Select(e => new EmpleadoLeido(e.Documento, e.CargoCodigo, e.CargoNombre, e.Area, e.CentroCostoCodigo, e.CentroCostoNombre, e.Sede))],
            lectura.Errores);
    }
}
