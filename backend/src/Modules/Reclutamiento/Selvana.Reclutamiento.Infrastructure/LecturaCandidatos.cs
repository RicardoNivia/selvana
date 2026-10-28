using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Application.Candidatos;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Infrastructure;

/// <summary>
/// Banco de talento y ficha del candidato. Quien tiene alcance limitado a ciertas empresas usuarias solo ve candidatos
/// que se postularon a vacantes que él ve, y en la ficha solo esas postulaciones.
/// </summary>
internal sealed class LecturaCandidatos(ReclutamientoDbContext db) : ILecturaCandidatos
{
    public async Task<Pagina<FilaCandidato>> BuscarAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, InterpretacionBusqueda busqueda, string? textoLibre,
        PaginaSolicitada pagina, CancellationToken cancellationToken)
    {
        var baseQ = Visibles(empresaId, alcance);
        var q = baseQ;
        if (busqueda.Documento is { } documento)
        {
            q = q.Where(c => c.Documento.Contains(documento));
        }
        else if (!busqueda.Vacia)
        {
            foreach (var termino in busqueda.Terminos.Append(busqueda.Ciudad is null ? null : Normalizacion.Simplificar(busqueda.Ciudad)).OfType<string>())
            {
                var patron = $"%{termino}%";
                q = q.Where(c => EF.Functions.ILike(c.TextoBusqueda, patron));
            }
        }

        var total = await q.CountAsync(cancellationToken);
        if (total == 0 && !string.IsNullOrWhiteSpace(textoLibre) && busqueda.Documento is null)
        {
            // Si no coincide ningún perfil, puede ser un nombre
            var patron = $"%{textoLibre.Trim()}%";
            q = baseQ.Where(c => EF.Functions.ILike(c.Datos.Nombre, patron));
            total = await q.CountAsync(cancellationToken);
        }

        var filas = await q.OrderByDescending(c => c.ActualizadoEn).Skip(pagina.Omitir).Take(pagina.Tamano)
            .Select(c => new { c.Id, c.Documento, c.Datos, Total = c.Postulaciones.Count, Ultima = c.Postulaciones.Max(p => (DateTimeOffset?)p.RecibidaEn) })
            .ToListAsync(cancellationToken);
        return new Pagina<FilaCandidato>([.. filas.Select(f => new FilaCandidato(f.Id, f.Documento, f.Datos, f.Total, f.Ultima))], pagina.Numero, pagina.Tamano, total);
    }

    public async Task<FichaCandidato?> FichaAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, Guid candidatoId, CancellationToken cancellationToken)
    {
        var c = await db.Set<Candidato>().AsNoTracking().Include(x => x.Postulaciones)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == candidatoId, cancellationToken);
        if (c is null)
        {
            return null;
        }

        var ids = c.Postulaciones.Select(p => p.VacanteId).Distinct().ToList();
        var vacantes = await db.Set<Vacante>().AsNoTracking().Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, v.Codigo, v.Titulo, v.Estado, v.EmpresaUsuariaId }).ToDictionaryAsync(v => v.Id, cancellationToken);
        var postulaciones = c.Postulaciones
            .Where(p => vacantes.TryGetValue(p.VacanteId, out var v) && (alcance is null || v.EmpresaUsuariaId is not { } u || alcance.Contains(u)))
            .OrderByDescending(p => p.RecibidaEn)
            .Select(p =>
            {
                var v = vacantes[p.VacanteId];
                return new PostulacionCandidatoDto(p.Id, p.Numero, v.Id, v.Codigo, v.Titulo, v.Estado.ToString(), p.Canal.ToString(), p.RecibidaEn,
                    p.Respuestas.Count(r => r.EsNegativa), p.ArchivoHojaDeVidaId);
            }).ToList();
        if (alcance is not null && postulaciones.Count == 0)
        {
            return null;
        }

        var ultima = c.Postulaciones.MaxBy(p => p.RecibidaEn)?.Autorizacion;
        return new FichaCandidato(c.Id, c.Documento, c.Datos, c.Suprimido, c.CreadoEn, c.ActualizadoEn, postulaciones,
            ultima is null || string.IsNullOrWhiteSpace(ultima.Version) ? null : $"{ultima.Version} · {ultima.AceptadaEn:yyyy-MM-dd}",
            c.Suprimido ? [] : await DuplicadosAsync(c, cancellationToken));
    }

    /// <summary>Otra cédula con el mismo celular o correo: puede ser la misma persona con un documento mal escrito.</summary>
    private async Task<IReadOnlyList<PosibleDuplicadoDto>> DuplicadosAsync(Candidato c, CancellationToken cancellationToken)
    {
        var celular = new string([.. c.Datos.Celular.Where(char.IsDigit)]);
        var correo = c.Datos.Correo.Trim().ToLowerInvariant();
        if (celular.Length < 7 && correo.Length == 0)
        {
            return [];
        }

        // Igualdad sin distinguir mayúsculas; «_» y «%» del correo se escapan para que no actúen como comodines
        var patronCorreo = correo.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal);
        var otros = await db.Set<Candidato>().AsNoTracking()
            .Where(x => x.EmpresaId == c.EmpresaId && x.Id != c.Id && x.AutorizacionRevocadaEn == null
                && ((celular.Length >= 7 && x.Datos.Celular.Contains(celular)) || (correo.Length > 0 && EF.Functions.ILike(x.Datos.Correo, patronCorreo))))
            .Select(x => new { x.Id, x.Datos.Nombre, x.Documento, x.Datos.Celular, x.Datos.Correo }).Take(5).ToListAsync(cancellationToken);
        return [.. otros.Select(o => new PosibleDuplicadoDto(o.Id, o.Nombre, FormatoCandidato.Enmascarar(o.Documento),
            o.Correo.Trim().Equals(correo, StringComparison.OrdinalIgnoreCase) && correo.Length > 0 ? "Mismo correo" : "Mismo celular"))];
    }

    private IQueryable<Candidato> Visibles(Guid empresaId, IReadOnlyCollection<Guid>? alcance)
    {
        var q = db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId && c.AutorizacionRevocadaEn == null);
        if (alcance is null)
        {
            return q;
        }

        var visibles = db.Set<Vacante>().Where(v => v.EmpresaId == empresaId && (v.EmpresaUsuariaId == null || alcance.Contains(v.EmpresaUsuariaId.Value))).Select(v => v.Id);
        return q.Where(c => c.Postulaciones.Any(p => visibles.Contains(p.VacanteId)));
    }
}
