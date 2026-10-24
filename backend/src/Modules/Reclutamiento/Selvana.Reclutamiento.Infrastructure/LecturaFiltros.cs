using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Application.Filtros;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure;

/// <summary>Las postulaciones de la vacante con la hoja de vida, las respuestas y la afinidad para evaluar los filtros ATS (sin tracking).</summary>
internal sealed class LecturaFiltros(ReclutamientoDbContext db) : ILecturaFiltros
{
    public async Task<IReadOnlyList<PostulacionFiltrable>> PostulacionesAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken)
    {
        var filas = await db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId)
            .SelectMany(c => c.Postulaciones.Where(p => p.VacanteId == vacanteId), (c, p) => new
            {
                p.Id, CandidatoId = c.Id, p.Numero, c.Datos, p.Etapa, p.Respuestas, p.Afinidad, p.RescatadaPor, Suprimido = c.AutorizacionRevocadaEn != null,
            })
            .ToListAsync(cancellationToken);
        return [.. filas.Select(f => new PostulacionFiltrable(f.Id, f.CandidatoId, f.Numero, f.Datos.Nombre, f.Etapa, f.Datos, f.Respuestas, f.Afinidad, f.RescatadaPor,
            f.Suprimido))];
    }
}
