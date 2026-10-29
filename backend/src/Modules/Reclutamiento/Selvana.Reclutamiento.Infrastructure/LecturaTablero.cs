using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Application.Ranking;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure;

/// <summary>Las postulaciones de la vacante con lo justo para pintar el tablero (sin tracking).</summary>
internal sealed class LecturaTablero(ReclutamientoDbContext db) : ILecturaTablero
{
    public async Task<IReadOnlyList<FilaTablero>> PostulacionesAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken)
    {
        var filas = await db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId)
            .SelectMany(c => c.Postulaciones.Where(p => p.VacanteId == vacanteId), (c, p) => new
            {
                p.Id, CandidatoId = c.Id, p.Numero, c.Datos.Nombre, c.Datos.Ciudad, p.Etapa, p.Afinidad, p.Respuestas, p.RecibidaEn, p.MotivoDescarte, p.ProcesoVinculacionId, c.Datos, p.RescatadaPor, p.Historial,
                Suprimido = c.AutorizacionRevocadaEn != null,
            })
            .ToListAsync(cancellationToken);
        return [.. filas.Select(f => new FilaTablero(f.Id, f.CandidatoId, f.Numero, f.Nombre, f.Ciudad, f.Etapa, f.Afinidad, f.Respuestas.Count(r => !r.Respuesta),
            f.RecibidaEn, f.MotivoDescarte, f.Suprimido, f.ProcesoVinculacionId, f.Datos, f.Respuestas, f.RescatadaPor, f.Historial))];
    }
}
