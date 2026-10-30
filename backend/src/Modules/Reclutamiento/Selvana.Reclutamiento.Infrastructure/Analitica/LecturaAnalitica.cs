using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Application.Analitica;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure.Analitica;

/// <summary>Proyecciones sin tracking para el tablero de selección: solo las columnas que se usan.</summary>
internal sealed class LecturaAnalitica(ReclutamientoDbContext db) : ILecturaAnalitica
{
    public async Task<IReadOnlyList<VacanteAnalitica>> VacantesAsync(Guid empresaId, CancellationToken cancellationToken) =>
        await db.Set<Vacante>().AsNoTracking().Where(v => v.EmpresaId == empresaId)
            .Select(v => new VacanteAnalitica(v.Id, v.Codigo, v.Titulo, v.Ciudad, v.Estado, v.EmpresaUsuariaId, v.Cupos, v.CreadaEn, v.PublicadaEn, v.CerradaEn, v.CostoIaUsd))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PostulacionAnalitica>> PostulacionesAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var filas = await db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId)
            .SelectMany(c => c.Postulaciones, (c, p) => new { p.VacanteId, p.Etapa, p.Canal, p.RecibidaEn, p.Historial, p.CostoIaUsd, p.ProcesoVinculacionId })
            .ToListAsync(cancellationToken);
        return [.. filas.Select(p => new PostulacionAnalitica(p.VacanteId, p.Etapa, p.Canal, p.RecibidaEn, p.Historial, p.CostoIaUsd, p.ProcesoVinculacionId != null))];
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> CostoPreseleccionAsync(Guid empresaId, CancellationToken cancellationToken) =>
        await db.Set<ConversacionPreseleccion>().AsNoTracking().Where(c => c.EmpresaId == empresaId)
            .GroupBy(c => c.VacanteId).Select(g => new { g.Key, Costo = g.Sum(c => c.CostoIaUsd) })
            .ToDictionaryAsync(x => x.Key, x => x.Costo, cancellationToken);
}
