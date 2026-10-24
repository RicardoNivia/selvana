using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure;

/// <summary>Lo que Vinculación lee de una postulación para contratar (Contracts). Sin hoja de vida ni resultados de pruebas.</summary>
internal sealed class PostulacionesContratables(ReclutamientoDbContext db) : IPostulacionesContratables
{
    public async Task<PostulanteContratable?> ObtenerAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken)
    {
        var c = await db.Set<Candidato>().AsNoTracking().Include(x => x.Postulaciones)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Postulaciones.Any(p => p.Id == postulacionId), cancellationToken);
        var p = c?.Postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (c is null || p is null || c.Suprimido)
        {
            return null;
        }

        var v = await db.Set<Vacante>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.VacanteId, cancellationToken);
        if (v is null)
        {
            return null;
        }

        var aspiracion = await db.Set<ConversacionPreseleccion>().AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.PostulacionId == postulacionId).Select(x => x.Resumen.AspiracionSalarialCop).FirstOrDefaultAsync(cancellationToken);
        return new PostulanteContratable(p.Id, p.Numero, c.Id, v.Id, v.Codigo, p.Etapa.ToString(), p.ProcesoVinculacionId,
            c.Datos.Nombre, c.Datos.Documento, c.Datos.Correo, c.Datos.Celular, c.Datos.Ciudad,
            v.Titulo, v.Cargo, v.Ciudad, v.Jornada, v.Descripcion, v.Requisitos, v.Competencias, v.SalarioMinimo, v.SalarioMaximo, aspiracion,
            v.EmpresaVisible, v.EmpresaUsuariaId, v.EmpresaUsuariaNombre);
    }
}
