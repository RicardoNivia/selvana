using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure.Analitica;

internal sealed class ResumenReclutamientoLectura(ReclutamientoDbContext db) : IResumenReclutamiento
{
    public async Task<ResumenReclutamiento> ResumenAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var vacantes = await db.Set<Vacante>().AsNoTracking().Where(v => v.EmpresaId == empresaId).GroupBy(v => v.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() }).ToListAsync(cancellationToken);
        var postulaciones = await db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId).SelectMany(c => c.Postulaciones).CountAsync(cancellationToken);
        return new ResumenReclutamiento(vacantes.Sum(v => v.Total), vacantes.Where(v => v.Estado == EstadoVacante.Publicada).Sum(v => v.Total), postulaciones);
    }
}
