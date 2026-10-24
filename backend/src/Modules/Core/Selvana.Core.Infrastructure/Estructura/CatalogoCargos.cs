using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Estructura;
using Selvana.Core.Domain.Estructura;
using Selvana.Core.Infrastructure.Persistence;

namespace Selvana.Core.Infrastructure.Estructura;

/// <summary>Catálogo de cargos para otros módulos (Reclutamiento parte de aquí al pedir una vacante). Solo cargos activos.</summary>
internal sealed class CatalogoCargos(CoreDbContext db) : ICatalogoCargos
{
    public async Task<CargoCatalogo?> ObtenerAsync(Guid empresaId, Guid cargoId, CancellationToken cancellationToken)
    {
        var cargo = await Activos(empresaId).FirstOrDefaultAsync(c => c.Id == cargoId, cancellationToken);
        return cargo is null ? null : Mapear(cargo);
    }

    public async Task<IReadOnlyList<CargoCatalogo>> BuscarAsync(Guid empresaId, string? texto, int maximo, CancellationToken cancellationToken)
    {
        var consulta = Activos(empresaId);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nombre, patron) || EF.Functions.ILike(c.Area, patron));
        }

        var cargos = await consulta.OrderByDescending(c => c.EstadoPerfil == EstadoPerfil.Aprobado).ThenByDescending(c => c.Empleados).ThenBy(c => c.Nombre)
            .Take(Math.Clamp(maximo, 1, 50)).ToListAsync(cancellationToken);
        return [.. cargos.Select(Mapear)];
    }

    private IQueryable<Cargo> Activos(Guid empresaId) =>
        db.Set<Cargo>().AsNoTracking().Where(c => EF.Property<Guid>(c, "EstructuraId") == empresaId && c.Activo);

    private static CargoCatalogo Mapear(Cargo c) => new(
        c.Id, c.Nombre, c.Area, c.Empleados, c.EstadoPerfil.ToString(),
        c.Perfil?.Mision ?? "", c.Perfil?.Funciones ?? [], c.Perfil?.Requisitos ?? [], c.Perfil?.RiesgoArl ?? "");
}
