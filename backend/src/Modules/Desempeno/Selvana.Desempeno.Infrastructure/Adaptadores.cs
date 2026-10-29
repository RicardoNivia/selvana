using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Desempeno.Application.Abstracciones;
using Selvana.Desempeno.Contracts;
using Selvana.Desempeno.Domain.Ciclos;
using Selvana.Desempeno.Domain.Evaluaciones;
using Selvana.Desempeno.Infrastructure.Persistence;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Infrastructure;

internal sealed class CiclosRepositorio(DesempenoDbContext db) : ICiclos
{
    public Task<Ciclo?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<Ciclo>().FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Id == id, cancellationToken);

    public void Agregar(Ciclo ciclo) => db.Add(ciclo);
}

internal sealed class EvaluacionesRepositorio(DesempenoDbContext db) : IEvaluaciones
{
    public Task<Evaluacion?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<Evaluacion>().FirstOrDefaultAsync(e => e.EmpresaId == empresaId && e.Id == id, cancellationToken);

    public Task<Evaluacion?> PorIdInternoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Evaluacion>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> HayDelCicloAsync(Guid cicloId, CancellationToken cancellationToken) =>
        db.Set<Evaluacion>().AnyAsync(e => e.CicloId == cicloId, cancellationToken);

    public Task<Evaluacion?> DeLaPersonaAsync(Guid id, string documento, string correo, CancellationToken cancellationToken) =>
        DeLaPersona(db.Set<Evaluacion>(), documento, correo).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Agregar(Evaluacion evaluacion) => db.Add(evaluacion);

    /// <summary>Por cédula y correo a la vez; el correo sin mayúsculas (ILIKE) y escapado, para que % o _ no funcionen como comodines.</summary>
    public static IQueryable<Evaluacion> DeLaPersona(IQueryable<Evaluacion> q, string documento, string correo)
    {
        var patron = correo.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        return q.Where(e => e.Documento == documento && EF.Functions.ILike(e.Correo, patron, "\\"));
    }
}

internal sealed class LecturaDesempeno(DesempenoDbContext db) : ILecturaDesempeno
{
    private IQueryable<Evaluacion> Visibles(Guid empresaId, Guid cicloId, IReadOnlyCollection<Guid>? alcance) =>
        db.Set<Evaluacion>().AsNoTracking().Where(e => e.EmpresaId == empresaId && e.CicloId == cicloId
            && (alcance == null || e.EmpresaUsuariaId == null || alcance.Contains(e.EmpresaUsuariaId.Value)));

    public async Task<Pagina<FilaCicloDto>> CiclosAsync(Guid empresaId, PaginaSolicitada pagina, CancellationToken cancellationToken)
    {
        var q = db.Set<Ciclo>().AsNoTracking().Where(c => c.EmpresaId == empresaId);
        var total = await q.CountAsync(cancellationToken);
        var ciclos = await q.OrderByDescending(c => c.CreadoEn).Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);
        var ids = ciclos.Select(c => c.Id).ToList();
        var conteos = await db.Set<Evaluacion>().AsNoTracking().Where(e => ids.Contains(e.CicloId))
            .GroupBy(e => e.CicloId).Select(g => new { g.Key, Total = g.Count(), Calificadas = g.Count(e => e.Estado == EstadoEvaluacion.Calificada) })
            .ToDictionaryAsync(x => x.Key, cancellationToken);
        return new Pagina<FilaCicloDto>([.. ciclos.Select(c => new FilaCicloDto(c.Id, c.Nombre, c.Tipo.ToString(), c.Alcance.ToString(), c.Desde, c.Hasta,
            c.Estado.ToString(), c.Formularios.Count, c.Formularios.Count(f => f.AprobadoEn != null), conteos.GetValueOrDefault(c.Id)?.Total ?? 0,
            conteos.GetValueOrDefault(c.Id)?.Calificadas ?? 0))], pagina.Numero, pagina.Tamano, total);
    }

    public async Task<Pagina<FilaEvaluacionDto>> EvaluacionesAsync(Guid empresaId, Guid cicloId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina,
        string? buscar, string? estado, CancellationToken cancellationToken)
    {
        var q = Visibles(empresaId, cicloId, alcance);
        if (Enum.TryParse<EstadoEvaluacion>(estado, out var e))
        {
            q = q.Where(x => x.Estado == e);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.Nombre, patron) || EF.Functions.ILike(x.Cargo, patron) || EF.Functions.ILike(x.Area, patron)
                || EF.Functions.ILike(x.Codigo, patron));
        }

        var total = await q.CountAsync(cancellationToken);
        var filas = await q.OrderBy(x => x.Nombre).Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);
        return new Pagina<FilaEvaluacionDto>([.. filas.Select(x => new FilaEvaluacionDto(x.Id, x.EmpleadoId, x.Codigo, x.Nombre, x.Cargo, x.Area, x.Sede,
            x.Estado.ToString(), x.AutoevaluadaEn != null, x.Puntaje, x.Potencial?.ToString(), x.Caja, x.Ajuste != null, x.CalificadaPor))],
            pagina.Numero, pagina.Tamano, total);
    }

    public async Task<IReadOnlyList<CalificadaDto>> CalificadasAsync(Guid empresaId, Guid cicloId, IReadOnlyCollection<Guid>? alcance,
        CancellationToken cancellationToken) =>
        [.. (await Visibles(empresaId, cicloId, alcance).Where(e => e.Estado == EstadoEvaluacion.Calificada && e.Puntaje != null).ToListAsync(cancellationToken))
            .Select(e => new CalificadaDto(e.Id, e.Nombre, e.Cargo, e.Area, e.CalificadaPor ?? "", e.Puntaje!.Value, e.Caja, e.Ajuste != null))];

    public async Task<IReadOnlyDictionary<string, int>> ConteoAsync(Guid empresaId, Guid cicloId, IReadOnlyCollection<Guid>? alcance,
        CancellationToken cancellationToken)
    {
        var porEstado = await Visibles(empresaId, cicloId, alcance).GroupBy(e => e.Estado).Select(g => new { g.Key, Total = g.Count() }).ToListAsync(cancellationToken);
        var conteo = Enum.GetValues<EstadoEvaluacion>().ToDictionary(e => e.ToString(), e => porEstado.FirstOrDefault(x => x.Key == e)?.Total ?? 0);
        conteo["Autoevaluadas"] = await Visibles(empresaId, cicloId, alcance).CountAsync(e => e.AutoevaluadaEn != null, cancellationToken);
        return conteo;
    }
}

internal sealed class EventosDesempeno(DesempenoDbContext db) : IEventosDesempeno
{
    public void Publicar(IIntegrationEvent evento) => db.AddIntegrationEvent(evento);
}

internal sealed class Bitacora(IEventosDesempeno eventos, TimeProvider reloj) : IBitacora
{
    public void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false) =>
        eventos.Publicar(new AccionAuditada(Guid.NewGuid(), reloj.GetUtcNow(), empresaId, tipoActor, actor, accion, detalle, nota, "Desempeno", entidad, entidadId,
            datoSensible));
}

/// <summary>La última calificación de cada empleado, para las señales de rotación de Clima.</summary>
internal sealed class DesempenoRecienteLectura(DesempenoDbContext db) : IDesempenoReciente
{
    public async Task<IReadOnlyList<DesempenoReciente>> UltimosAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var calificadas = await db.Set<Evaluacion>().AsNoTracking().Where(e => e.EmpresaId == empresaId && e.Estado == EstadoEvaluacion.Calificada && e.Puntaje != null)
            .ToListAsync(cancellationToken);
        var ciclos = await db.Set<Ciclo>().AsNoTracking().Where(c => c.EmpresaId == empresaId).ToDictionaryAsync(c => c.Id, c => c.Nombre, cancellationToken);
        return [.. calificadas.GroupBy(e => e.EmpleadoId).Select(g => g.OrderByDescending(e => e.CalificadaEn).First())
            .Select(e => new DesempenoReciente(e.EmpleadoId, e.Puntaje!.Value, e.Potencial?.ToString(), e.Caja, ciclos.GetValueOrDefault(e.CicloId, "")))];
    }
}

/// <summary>180° y 360° · La autoevaluación desde el portal del empleado, por cédula y correo.</summary>
internal sealed class EvaluacionesDeLaPersona(DesempenoDbContext db, TimeProvider reloj) : IEvaluacionesDeLaPersona
{
    public async Task<IReadOnlyList<AutoevaluacionDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken)
    {
        var evaluaciones = await EvaluacionesRepositorio.DeLaPersona(db.Set<Evaluacion>().AsNoTracking(), documento, correo)
            .Where(e => e.Alcance != AlcanceEvaluacion.Grados90).ToListAsync(cancellationToken);
        var ids = evaluaciones.Select(e => e.CicloId).ToList();
        var ciclos = await db.Set<Ciclo>().AsNoTracking().Where(c => ids.Contains(c.Id) && c.Estado == EstadoCiclo.Abierto).ToDictionaryAsync(c => c.Id, cancellationToken);
        return [.. evaluaciones.Where(e => ciclos.ContainsKey(e.CicloId)).Select(e => new AutoevaluacionDeLaPersona(e.Id, ciclos[e.CicloId].Nombre, e.Cargo,
            ciclos[e.CicloId].Hasta, e.AutoevaluadaEn != null, [.. e.Competencias.Select(c => new CompetenciaParaAutoevaluar(c.Nombre, c.Descripcion, c.Auto))],
            [.. e.Objetivos.Select(o => new ObjetivoDelEmpleado(o.Descripcion, o.Meta, o.Avance))], e.ComentarioAuto))];
    }

    public async Task<Result> AutoevaluarAsync(Guid evaluacionId, string documento, string correo, IReadOnlyDictionary<string, int> calificaciones, string comentario,
        CancellationToken cancellationToken)
    {
        var e = await EvaluacionesRepositorio.DeLaPersona(db.Set<Evaluacion>(), documento, correo).FirstOrDefaultAsync(x => x.Id == evaluacionId, cancellationToken);
        if (e is null || !await db.Set<Ciclo>().AnyAsync(c => c.Id == e.CicloId && c.Estado == EstadoCiclo.Abierto, cancellationToken))
        {
            return Result.Failure(EvaluacionErrors.NoExiste);
        }

        var hecho = e.Autoevaluar(calificaciones, comentario, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        db.AddIntegrationEvent(new AccionAuditada(Guid.NewGuid(), reloj.GetUtcNow(), e.EmpresaId, TipoActor.Titular, e.Nombre, "Se autoevaluó desde su portal",
            e.Codigo, null, "Desempeno", "Evaluacion", e.Id, false));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
