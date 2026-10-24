using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Application.Ranking;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure;

/// <summary>
/// Las últimas decisiones de la empresa sobre candidatos (retroalimentación o puntaje ajustado), primero las del mismo cargo.
/// Viajan al agente sin nombre ni datos de contacto: solo cargos, años, habilidades técnicas, el puntaje de la IA y la decisión.
/// </summary>
internal sealed class CalibracionRanking(ReclutamientoDbContext db, TimeProvider reloj) : ICalibracionRanking
{
    public async Task<IReadOnlyList<CriterioEmpresa>> CriteriosAsync(Guid empresaId, string cargo, Guid excluirVacanteId, CancellationToken cancellationToken)
    {
        var vacantes = await db.Set<Vacante>().AsNoTracking().Where(v => v.EmpresaId == empresaId && v.Id != excluirVacanteId)
            .Select(v => new { v.Id, v.Cargo }).ToDictionaryAsync(v => v.Id, v => v.Cargo, cancellationToken);
        if (vacantes.Count == 0)
        {
            return [];
        }

        var ids = vacantes.Keys.ToList();
        var candidatos = await db.Set<Candidato>().AsNoTracking().Include(c => c.Postulaciones)
            .Where(c => c.EmpresaId == empresaId && c.AutorizacionRevocadaEn == null
                && c.Postulaciones.Any(p => ids.Contains(p.VacanteId) && (p.Retroalimentacion != null || p.Afinidad!.PuntajeAjustado != null)))
            .OrderByDescending(c => c.ActualizadoEn).Take(50).ToListAsync(cancellationToken);

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var mismo = Normalizacion.Simplificar(cargo);
        return [.. candidatos
            .SelectMany(c => c.Postulaciones.Where(p => vacantes.ContainsKey(p.VacanteId) && (p.Retroalimentacion is not null || p.Afinidad?.PuntajeAjustado is not null))
                .Select(p => (Candidato: c, Postulacion: p, Cargo: vacantes[p.VacanteId])))
            .OrderByDescending(x => Normalizacion.Simplificar(x.Cargo) == mismo)
            .ThenByDescending(x => x.Postulacion.Retroalimentacion?.En ?? x.Postulacion.Afinidad?.AjustadoEn)
            .Take(Calibracion.Maximo)
            .Select(x => new CriterioEmpresa(x.Cargo, Calibracion.Perfil(x.Candidato.Datos, hoy), x.Postulacion.Afinidad?.PuntajeIa, Calibracion.Decision(x.Postulacion),
                x.Postulacion.Retroalimentacion?.Nota ?? x.Postulacion.Afinidad?.MotivoAjuste))];
    }
}
