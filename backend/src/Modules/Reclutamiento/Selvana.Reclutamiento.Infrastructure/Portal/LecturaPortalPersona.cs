using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Application.Portal;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure.Portal;

/// <summary>
/// Las postulaciones de la persona en todas las empresas, buscadas por cédula y correo a la vez (ADR 0013): con solo el correo
/// nadie ve lo de otra persona.
/// </summary>
internal sealed class LecturaPortalPersona(ReclutamientoDbContext db, TimeProvider reloj) : ILecturaPortalPersona
{
    public async Task<IReadOnlyList<PostulacionDeLaPersona>> PostulacionesAsync(string documento, string correo, CancellationToken cancellationToken)
    {
        // El correo se compara sin mayúsculas (ILIKE) y escapado, para que % o _ no funcionen como comodines
        var patron = correo.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        var filas = await db.Set<Candidato>().AsNoTracking()
            .Where(c => c.Documento == documento && EF.Functions.ILike(c.Datos.Correo, patron, "\\"))
            .SelectMany(c => c.Postulaciones, (c, p) => new { c.EmpresaId, p.Id, p.Numero, p.VacanteId, p.RecibidaEn, p.Etapa, Revisada = p.Afinidad != null })
            .ToListAsync(cancellationToken);
        if (filas.Count == 0)
        {
            return [];
        }

        var vacanteIds = filas.Select(f => f.VacanteId).Distinct().ToList();
        var vacantes = await db.Set<Vacante>().AsNoTracking().Where(v => vacanteIds.Contains(v.Id))
            .Select(v => new
            {
                v.Id, v.Codigo, v.Titulo, v.Ciudad, Cerrada = v.Estado == EstadoVacante.Cerrada,
                Empresa = v.EmpresaConfidencial ? Vacante.NombreConfidencial : v.EmpresaUsuariaNombre ?? v.EmpresaNombre,
            })
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var postulacionIds = filas.Select(f => f.Id).ToList();
        var conversaciones = (await db.Set<ConversacionPreseleccion>().AsNoTracking().Where(c => postulacionIds.Contains(c.PostulacionId)).ToListAsync(cancellationToken))
            .GroupBy(c => c.PostulacionId).ToDictionary(g => g.Key, g => g.MaxBy(c => c.CreadaEn)!);
        var aplicaciones = (await db.Set<AplicacionPruebas>().AsNoTracking().Where(a => postulacionIds.Contains(a.PostulacionId)).ToListAsync(cancellationToken))
            .GroupBy(a => a.PostulacionId).ToDictionary(g => g.Key, g => g.MaxBy(a => a.VenceEn)!);

        var ahora = reloj.GetUtcNow();
        return [.. filas.Where(f => vacantes.ContainsKey(f.VacanteId)).Select(f =>
        {
            var v = vacantes[f.VacanteId];
            var c = conversaciones.GetValueOrDefault(f.Id);

            // Le toca a la persona si la conversación sigue abierta y el último mensaje no es suyo (le escribió el agente o una persona)
            var chatPorResponder = c is { Activa: true, Pensando: false } && c.Mensajes.LastOrDefault()?.Autor != AutorMensaje.Candidato;
            var a = aplicaciones.GetValueOrDefault(f.Id);
            var porPresentar = a is not null && a.PorPresentar(ahora);
            return new PostulacionDeLaPersona(f.Id, f.EmpresaId, f.Numero, v.Codigo, v.Titulo, v.Empresa, v.Ciudad, v.Cerrada, f.RecibidaEn,
                f.Etapa.ToString(), f.Revisada, c?.Id, chatPorResponder, porPresentar ? a!.Id : null, porPresentar ? a!.VenceEn : null);
        })];
    }
}

/// <summary>Vacantes publicadas para cruzar con la hoja de vida; respeta la oferta confidencial y el salario oculto.</summary>
internal sealed class LecturaOfertas(ReclutamientoDbContext db) : ILecturaOfertas
{
    public async Task<IReadOnlyList<VacanteParaComparar>> PublicadasAsync(IReadOnlyCollection<string> codigosYaPostulados, int maximo, CancellationToken cancellationToken)
    {
        var codigos = codigosYaPostulados.ToList();
        var filas = await db.Set<Vacante>().AsNoTracking()
            .Where(v => v.Estado == EstadoVacante.Publicada && !codigos.Contains(v.Codigo))
            .OrderByDescending(v => v.PublicadaEn).Take(maximo)
            .Select(v => new
            {
                v.Codigo, v.Titulo, v.Cargo, v.Ciudad, v.Jornada, v.Descripcion, v.Requisitos, v.SalarioOculto, v.SalarioMinimo, v.SalarioMaximo, v.PublicadaEn,
                Empresa = v.EmpresaConfidencial ? Vacante.NombreConfidencial : v.EmpresaUsuariaNombre ?? v.EmpresaNombre,
            })
            .ToListAsync(cancellationToken);
        return [.. filas.Select(v => new VacanteParaComparar(v.Codigo, v.Titulo, v.Cargo, v.Empresa, v.Ciudad, v.Jornada,
            v.SalarioOculto ? 0 : v.SalarioMinimo, v.SalarioOculto ? 0 : v.SalarioMaximo, $"{string.Join(' ', v.Requisitos)} {v.Descripcion}",
            v.PublicadaEn ?? DateTimeOffset.MinValue))];
    }
}
