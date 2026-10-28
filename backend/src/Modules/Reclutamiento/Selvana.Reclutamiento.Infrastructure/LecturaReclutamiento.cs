using Microsoft.EntityFrameworkCore;
using Selvana.Reclutamiento.Application;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Privacidad;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Postulaciones;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Infrastructure;

internal sealed class LecturaReclutamiento(ReclutamientoDbContext db) : ILecturaReclutamiento
{
    public async Task<Pagina<VacanteResumenDto>> VacantesAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, Guid? empresaUsuariaId, PaginaSolicitada pagina,
        string? buscar, string? estado, CancellationToken ct)
    {
        var q = db.Set<Vacante>().AsNoTracking().Where(v => v.EmpresaId == empresaId);
        if (alcance is not null)
        {
            // Las vacantes de la propia EST (sin empresa usuaria) las ven todos; las de un cliente, solo quien lo tiene asignado
            var ids = alcance.ToList();
            q = q.Where(v => v.EmpresaUsuariaId == null || ids.Contains(v.EmpresaUsuariaId.Value));
        }

        if (empresaUsuariaId is { } elegida)
        {
            q = q.Where(v => v.EmpresaUsuariaId == elegida);
        }
        if (Enum.TryParse<EstadoVacante>(estado, ignoreCase: true, out var e))
        {
            q = q.Where(v => v.Estado == e);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(v => EF.Functions.ILike(v.Titulo, patron) || EF.Functions.ILike(v.Ciudad, patron) || EF.Functions.ILike(v.Codigo, patron) || EF.Functions.ILike(v.Frase, patron));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(v => v.CreadaEn).Skip(pagina.Omitir).Take(pagina.Tamano)
            .Select(v => new VacanteResumenDto(v.Id, v.Codigo, v.Titulo, v.EmpresaUsuariaNombre ?? v.EmpresaNombre, v.Ciudad, v.Cupos, v.Estado.ToString(),
                db.Set<Postulacion>().Count(p => p.VacanteId == v.Id), v.CreadaEn, v.PublicadaEn))
            .ToListAsync(ct);
        return new Pagina<VacanteResumenDto>(items, pagina.Numero, pagina.Tamano, total);
    }

    public async Task<VacanteDto?> VacanteAsync(Guid empresaId, Guid vacanteId, CancellationToken ct)
    {
        var v = await db.Set<Vacante>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == vacanteId && x.EmpresaId == empresaId, ct);
        if (v is null)
        {
            return null;
        }

        var canales = await db.Set<Postulacion>().AsNoTracking().Where(p => p.VacanteId == vacanteId)
            .GroupBy(p => p.Canal).Select(g => new { Canal = g.Key, Total = g.Count() }).ToListAsync(ct);

        return new VacanteDto(v.Id, v.Codigo, v.Frase, v.Estado.ToString(), v.EmpresaVisible, v.EmpresaUsuariaId,
            v.Titulo, v.Cargo, v.Ciudad, v.Cupos, v.Jornada, v.Descripcion, v.SalarioMinimo, v.SalarioMaximo, v.JustificacionSalario,
            v.Requisitos, v.Competencias, v.PreguntasFiltro, v.Supuestos,
            [.. v.Razones.Select(r => new RazonDto(r.Signo, r.Texto, r.Fuente))], v.ModeloIa, v.MensajeIa, v.EditadaPorPersona,
            v.CreadaEn, v.CreadaPor, v.PublicadaEn, canales.Sum(c => c.Total),
            [.. canales.OrderByDescending(c => c.Total).Select(c => new ConteoDto(c.Canal.ToString(), c.Total))],
            [.. v.Pruebas.Select(p => new Application.Pruebas.PruebaEnBateriaDto(p.Codigo, p.Peso, p.Corte))],
            new AprobacionRequisicionDto([.. FlujoRequisicion.Etapas.Select(FlujoRequisicion.Nombre)],
                v.EtapaPendiente is { } e ? FlujoRequisicion.Nombre(e) : null, v.EtapaPendiente is { } q ? FlujoRequisicion.QuienesAprueban(q) : [],
                v.AprobacionPedidaPor, v.MotivoDevolucion,
                [.. v.Aprobaciones.Select(a => new AprobacionDto(FlujoRequisicion.Nombre(a.Etapa), a.Decision, a.Por, a.Rol, a.Motivo, a.Automatica, a.En))],
                PuedeDecidir: false),
            v.CargoId, CodigoMunicipio: v.CodigoMunicipio, EmpresaConfidencial: v.EmpresaConfidencial, SalarioOculto: v.SalarioOculto, Aclaraciones: [.. v.Aclaraciones.Select(a => new AclaracionDto(a.Pregunta, a.Opciones))]);
    }

    public async Task<Pagina<PostulanteDto>> PostulantesAsync(Guid empresaId, Guid vacanteId, PaginaSolicitada pagina, string? buscar, string? canal, CancellationToken ct)
    {
        var q = db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId)
            .SelectMany(c => c.Postulaciones.Where(p => p.VacanteId == vacanteId), (c, p) => new { Candidato = c, Postulacion = p });

        if (Enum.TryParse<CanalPostulacion>(canal, ignoreCase: true, out var filtroCanal))
        {
            q = q.Where(x => x.Postulacion.Canal == filtroCanal);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            var patron = $"%{texto}%";
            var digitos = Candidato.NormalizarDocumento(texto);
            q = q.Where(x => EF.Functions.ILike(x.Candidato.Datos.Nombre, patron) || (digitos.Length > 0 && x.Candidato.Documento.Contains(digitos)));
        }

        var total = await q.CountAsync(ct);
        // Primero los de mayor afinidad (el puntaje de la persona manda); sin puntaje, los más recientes al final
        var filas = await q.OrderByDescending(x => x.Postulacion.Afinidad!.PuntajeAjustado ?? x.Postulacion.Afinidad!.PuntajeIa ?? -1)
            .ThenByDescending(x => x.Postulacion.RecibidaEn).Skip(pagina.Omitir).Take(pagina.Tamano)
            .Select(x => new { x.Candidato.Id, x.Candidato.Documento, x.Candidato.Datos, x.Postulacion, Otras = x.Candidato.Postulaciones.Count - 1 })
            .ToListAsync(ct);

        var items = filas.Select(f => new PostulanteDto(
            f.Postulacion.Id, f.Id, f.Postulacion.Numero, f.Datos.Nombre, Enmascarar(f.Documento), f.Datos.Celular, f.Datos.Ciudad,
            f.Postulacion.Canal.ToString(),
            string.Join(" · ", f.Postulacion.Respuestas.Select(r => r.Respuesta ? "Sí" : "No")),
            f.Postulacion.Respuestas.Count(r => !r.Respuesta),
            $"Aceptada {f.Postulacion.Autorizacion.Version}", f.Postulacion.RecibidaEn, f.Otras, f.Postulacion.CamposCorregidos, f.Datos.LinkedIn,
            Afinidad(f.Postulacion.Afinidad), f.Postulacion.Retroalimentacion?.Valor.ToString(), f.Postulacion.Retroalimentacion?.Nota)).ToList();

        return new Pagina<PostulanteDto>(items, pagina.Numero, pagina.Tamano, total);
    }

    public async Task<PortalDto> PortalAsync(PaginaSolicitada pagina, string? buscar, string? ciudad, string? jornada, CancellationToken ct)
    {
        var publicadas = db.Set<Vacante>().AsNoTracking().Where(v => v.Estado == EstadoVacante.Publicada);
        var q = publicadas;
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(v => EF.Functions.ILike(v.Titulo, patron) || EF.Functions.ILike(v.Cargo, patron) || EF.Functions.ILike(v.Descripcion, patron)
                || EF.Functions.ILike(v.EmpresaNombre, patron) || (v.EmpresaUsuariaNombre != null && EF.Functions.ILike(v.EmpresaUsuariaNombre, patron)));
        }

        var facetas = q;
        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            q = q.Where(v => v.Ciudad == ciudad);
        }

        if (!string.IsNullOrWhiteSpace(jornada))
        {
            q = q.Where(v => v.Jornada == jornada);
        }

        var total = await q.CountAsync(ct);
        var filas = await q.OrderByDescending(v => v.PublicadaEn).Skip(pagina.Omitir).Take(pagina.Tamano)
            .Select(v => new { v.Codigo, v.Titulo, Empresa = v.EmpresaConfidencial ? Vacante.NombreConfidencial : v.EmpresaUsuariaNombre ?? v.EmpresaNombre, v.Ciudad, v.Jornada, v.Cupos,
                v.SalarioMinimo, v.SalarioMaximo, v.SalarioOculto, v.Descripcion, v.PublicadaEn })
            .ToListAsync(ct);
        var items = filas.Select(v => new VacantePublicaDto(v.Codigo, v.Titulo, v.Empresa, v.Ciudad, v.Jornada, v.Cupos,
            v.SalarioOculto ? 0 : v.SalarioMinimo, v.SalarioOculto ? 0 : v.SalarioMaximo,
            v.Descripcion.Length > 160 ? string.Concat(v.Descripcion.AsSpan(0, 160), "…") : v.Descripcion, v.PublicadaEn ?? DateTimeOffset.MinValue, v.SalarioOculto)).ToList();

        // Se ordena por el conteo antes de armar el DTO: EF no traduce un orden sobre un record construido
        var ciudades = (await facetas.GroupBy(v => v.Ciudad).Select(g => new { g.Key, Total = g.Count() }).OrderByDescending(x => x.Total).Take(12).ToListAsync(ct))
            .Select(x => new ConteoDto(x.Key, x.Total)).ToList();
        var jornadas = (await facetas.GroupBy(v => v.Jornada).Select(g => new { g.Key, Total = g.Count() }).OrderByDescending(x => x.Total).Take(8).ToListAsync(ct))
            .Select(x => new ConteoDto(x.Key, x.Total)).ToList();
        var empresas = await publicadas.Select(v => v.EmpresaUsuariaNombre ?? v.EmpresaNombre).Distinct().CountAsync(ct);

        return new PortalDto(new Pagina<VacantePublicaDto>(items, pagina.Numero, pagina.Tamano, total), ciudades, jornadas, empresas);
    }

    public async Task<VacantePublicaDetalleDto?> VacantePublicaAsync(string codigo, CancellationToken ct)
    {
        var normalizado = codigo.Trim().ToUpperInvariant();
        var v = await db.Set<Vacante>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Codigo == normalizado && (x.Estado == EstadoVacante.Publicada || x.Estado == EstadoVacante.Cerrada), ct);
        return v is null ? null : new VacantePublicaDetalleDto(v.Codigo, v.Titulo, v.EmpresaEnPortal, v.Ciudad, v.Jornada, v.Cupos,
            v.SalarioOculto ? 0 : v.SalarioMinimo, v.SalarioOculto ? 0 : v.SalarioMaximo, v.Descripcion, v.Requisitos, v.PreguntasFiltro,
            v.PublicadaEn ?? v.CreadaEn, v.RecibePostulaciones, v.SalarioOculto);
    }

    public async Task<BorradorPostulacionDto?> BorradorAsync(Guid borradorId, CancellationToken ct)
    {
        var b = await db.Set<BorradorPostulacion>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == borradorId, ct);
        if (b is null)
        {
            return null;
        }

        var v = await db.Set<Vacante>().AsNoTracking().FirstAsync(x => x.Id == b.VacanteId, ct);
        var d = b.DatosLeidos;
        return new BorradorPostulacionDto(b.Id, b.Estado.ToString(), b.Mensaje, b.NombreArchivo,
            d is null ? null : new DatosCandidatoDto(d.Nombre, d.Documento, d.Celular, d.Correo, d.Ciudad,
                [.. d.Experiencia.Select(x => new ExperienciaDto(x.Cargo, x.Empresa, x.Desde, x.Hasta))],
                [.. d.Formacion.Select(x => new FormacionDto(x.Titulo, x.Institucion, x.Anio))], d.Cursos, d.Habilidades),
            v.Codigo, v.Titulo, v.EmpresaEnPortal, v.Ciudad, v.PreguntasFiltro, PoliticaDatos.Version, PoliticaDatos.Texto(v.ResponsableDatos));
    }

    private static AfinidadDto? Afinidad(AfinidadPostulacion? a) => a is null ? null : new AfinidadDto(
        a.Estado.ToString(), a.Puntaje, a.PuntajeIa, [.. a.Razones.Select(r => new RazonDto(r.Signo, r.Texto, r.Evidencia))], a.Cumplidos, a.Faltantes,
        a.Modelo, a.Mensaje, a.CalculadaEn, a.PuntajeAjustado is not null, a.MotivoAjuste, a.AjustadoPor, a.RazonesRetiradas);

    private static string Enmascarar(string documento) => documento.Length <= 3 ? "CC ***" : $"CC ***.{documento[^3..]}";

    public async Task<Pagina<AutorizacionDto>> AutorizacionesAsync(Guid empresaId, PaginaSolicitada pagina, string? buscar, string? estado, CancellationToken ct)
    {
        var q = db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId)
            .SelectMany(c => c.Postulaciones, (c, p) => new { Candidato = c, Postulacion = p });

        if (estado == "Vigente")
        {
            q = q.Where(x => x.Candidato.AutorizacionRevocadaEn == null);
        }
        else if (estado == "Revocada")
        {
            q = q.Where(x => x.Candidato.AutorizacionRevocadaEn != null);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            var patron = $"%{texto}%";
            var digitos = Candidato.NormalizarDocumento(texto);
            q = q.Where(x => EF.Functions.ILike(x.Candidato.Datos.Nombre, patron) || EF.Functions.ILike(x.Postulacion.Numero, patron)
                || (digitos.Length > 0 && x.Candidato.Documento.Contains(digitos)));
        }

        var total = await q.CountAsync(ct);
        var filas = await q.OrderByDescending(x => x.Postulacion.RecibidaEn).Skip(pagina.Omitir).Take(pagina.Tamano)
            .Select(x => new { x.Candidato.Datos.Nombre, x.Candidato.Documento, x.Candidato.AutorizacionRevocadaEn, x.Postulacion })
            .ToListAsync(ct);

        var ids = filas.Select(f => f.Postulacion.VacanteId).Distinct().ToList();
        var codigos = await db.Set<Vacante>().AsNoTracking().Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Codigo, ct);
        var items = filas.Select(f => new AutorizacionDto(
            f.Postulacion.Id, f.Nombre, Enmascarar(f.Documento), CanalLegible(f.Postulacion.Canal), f.Postulacion.Autorizacion.Version,
            f.Postulacion.Autorizacion.AceptadaEn, f.AutorizacionRevocadaEn is null ? "Vigente" : "Revocada", f.Postulacion.Autorizacion.Huella,
            codigos.GetValueOrDefault(f.Postulacion.VacanteId, ""))).ToList();
        return new Pagina<AutorizacionDto>(items, pagina.Numero, pagina.Tamano, total);
    }

    public async Task<(int Vigentes, int RevocadasEnElAnio)> ConteoAutorizacionesAsync(Guid empresaId, int anio, CancellationToken ct)
    {
        var desde = new DateTimeOffset(anio, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var candidatos = db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId);
        var vigentes = await candidatos.CountAsync(c => c.AutorizacionRevocadaEn == null, ct);
        var revocadas = await candidatos.CountAsync(c => c.AutorizacionRevocadaEn >= desde, ct);
        return (vigentes, revocadas);
    }

    private static string CanalLegible(CanalPostulacion canal) => canal switch
    {
        CanalPostulacion.Portal => "Portal de empleo",
        CanalPostulacion.LinkedIn => "LinkedIn",
        CanalPostulacion.Computrabajo => "Computrabajo",
        CanalPostulacion.Referido => "Referido",
        CanalPostulacion.Banco => "Banco de talento",
        _ => "Otro",
    };
}
