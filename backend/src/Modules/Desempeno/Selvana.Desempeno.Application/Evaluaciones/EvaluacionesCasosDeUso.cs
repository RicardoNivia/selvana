using FluentValidation;
using Selvana.AI.Contracts.Desempeno;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Desempeno.Application.Abstracciones;
using Selvana.Desempeno.Application.Ciclos;
using Selvana.Desempeno.Contracts.Ia;
using Selvana.Desempeno.Domain.Ciclos;
using Selvana.Desempeno.Domain.Evaluaciones;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Application.Evaluaciones;

/* ---------- DTO ---------- */

public sealed record EvidenciaDto(string Texto, DateOnly Fecha, string Por);

public sealed record ObjetivoDto(Guid Id, string Descripcion, string Meta, int Peso, int Avance, IReadOnlyList<EvidenciaDto> Evidencias);

public sealed record CalificacionDto(string Nombre, string Descripcion, int Peso, int? Lider, int? Auto, decimal? PromedioPares, int Pares);

public sealed record BorradorDto(
    string Estado, IReadOnlyList<string> Fortalezas, IReadOnlyList<string> Oportunidades, string Comentario, IReadOnlyList<RazonDto> Razones, string? Modelo,
    string? Error);

public sealed record AccionPlanDto(Guid Id, string Competencia, string Accion, DateOnly Fecha, bool Cumplida, string Origen);

public sealed record AjusteDto(int Caja, string Motivo, string Por, DateTimeOffset En);

public sealed record EvaluacionDto(
    Guid Id, Guid CicloId, string Ciclo, string EstadoCiclo, string Alcance, int PesoObjetivos, Guid EmpleadoId, string Codigo, string Nombre, string Cargo, string Area,
    string Sede, string Estado, IReadOnlyList<ObjetivoDto> Objetivos, IReadOnlyList<CalificacionDto> Competencias, string ComentarioLider, string ComentarioAuto,
    DateTimeOffset? AutoevaluadaEn, string? CalificadaPor, DateTimeOffset? CalificadaEn, BorradorDto? Borrador, string? Potencial, decimal? Puntaje,
    decimal PuntajeEnCurso, int? Caja, string? NombreCaja, AjusteDto? Ajuste, IReadOnlyList<AccionPlanDto> Plan);

public sealed record ObjetivoRequest(Guid? Id, string Descripcion, string Meta, int Peso);

public sealed record AccionPlanRequest(Guid? Id, string Competencia, string Accion, DateOnly Fecha, bool Cumplida);

/* ---------- Consultas y comandos ---------- */

public sealed record ListarEvaluacionesQuery(Guid CicloId, int? Pagina, int? Tamano, string? Buscar, string? Estado) : IQuery<Pagina<FilaEvaluacionDto>>;

public sealed record ObtenerEvaluacionQuery(Guid Id) : IQuery<EvaluacionDto>;

public sealed record DefinirObjetivosCommand(Guid Id, IReadOnlyList<ObjetivoRequest> Objetivos) : ICommand;

public sealed record RegistrarAvanceCommand(Guid Id, Guid ObjetivoId, int Avance, string? Evidencia) : ICommand;

/// <summary>360° · la calificación de un par, anónima.</summary>
public sealed record RegistrarParCommand(Guid Id, IReadOnlyDictionary<string, int> Calificaciones) : ICommand;

/// <summary>D3 · Que el copiloto redacte un borrador con las evidencias (sin nombre ni documento).</summary>
public sealed record PedirBorradorCommand(Guid Id) : ICommand;

public sealed record UsarBorradorCommand(Guid Id) : ICommand;

/// <summary>Decisión humana: el líder califica las competencias, comenta y marca el potencial.</summary>
public sealed record CalificarEvaluacionCommand(Guid Id, IReadOnlyDictionary<string, int> Calificaciones, string Comentario, string Potencial) : ICommand;

public sealed record ReabrirEvaluacionCommand(Guid Id) : ICommand;

/// <summary>D4 · El comité mueve a la persona en la matriz, con motivo.</summary>
public sealed record CalibrarEvaluacionCommand(Guid Id, int Caja, string Motivo) : ICommand;

public sealed record DefinirPlanCommand(Guid Id, IReadOnlyList<AccionPlanRequest> Acciones) : ICommand;

internal static class Evaluaciones
{
    public static async Task<Result<Evaluacion>> DelUsuarioAsync(IEvaluaciones evaluaciones, IUsuarioActual usuario, Guid id, CancellationToken ct)
    {
        var e = await evaluaciones.PorIdAsync(usuario.EmpresaId, id, ct);
        return e is null || !usuario.Ve(e.EmpresaUsuariaId) ? EvaluacionErrors.NoExiste : e;
    }
}

internal sealed class CalificarValidator : AbstractValidator<CalificarEvaluacionCommand>
{
    public CalificarValidator()
    {
        RuleFor(c => c.Potencial).Must(p => Enum.TryParse<Potencial>(p, true, out _)).WithMessage("Marque el potencial: bajo, medio o alto.");
        RuleFor(c => c.Comentario).NotEmpty().WithMessage("Escriba su comentario: es la retroalimentación que recibe la persona.").MaximumLength(4000);
    }
}

internal sealed class DefinirObjetivosValidator : AbstractValidator<DefinirObjetivosCommand>
{
    public DefinirObjetivosValidator() => RuleForEach(c => c.Objetivos).ChildRules(x =>
    {
        x.RuleFor(y => y.Descripcion).NotEmpty().WithMessage("Cada objetivo necesita descripción.").MaximumLength(300);
        x.RuleFor(y => y.Meta).MaximumLength(200);
        x.RuleFor(y => y.Peso).InclusiveBetween(1, 100);
    });
}

internal sealed class RegistrarAvanceValidator : AbstractValidator<RegistrarAvanceCommand>
{
    public RegistrarAvanceValidator()
    {
        RuleFor(c => c.Avance).InclusiveBetween(0, 100).WithMessage("El avance va de 0 a 100 %.");
        RuleFor(c => c.Evidencia).MaximumLength(1000);
    }
}

internal sealed class CalibrarValidator : AbstractValidator<CalibrarEvaluacionCommand>
{
    public CalibrarValidator()
    {
        RuleFor(c => c.Caja).InclusiveBetween(1, 9);
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Diga por qué el comité mueve a la persona en la matriz.").MaximumLength(500);
    }
}

internal sealed class DefinirPlanValidator : AbstractValidator<DefinirPlanCommand>
{
    public DefinirPlanValidator() => RuleForEach(c => c.Acciones).ChildRules(x =>
    {
        x.RuleFor(y => y.Accion).NotEmpty().WithMessage("Cada acción del plan necesita descripción.").MaximumLength(400);
        x.RuleFor(y => y.Competencia).MaximumLength(120);
    });
}

/* ---------- Consultas ---------- */

internal sealed class ListarEvaluacionesHandler(IUsuarioActual usuario, ILecturaDesempeno lectura)
    : IQueryHandler<ListarEvaluacionesQuery, Pagina<FilaEvaluacionDto>>
{
    public async Task<Result<Pagina<FilaEvaluacionDto>>> Handle(ListarEvaluacionesQuery q, CancellationToken ct)
    {
        var estado = Enum.TryParse<EstadoEvaluacion>(q.Estado, true, out var e) ? e.ToString() : null;
        return await lectura.EvaluacionesAsync(usuario.EmpresaId, q.CicloId, usuario.EmpresasUsuarias, new PaginaSolicitada(q.Pagina, q.Tamano), q.Buscar, estado, ct);
    }
}

internal sealed class ObtenerEvaluacionHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, ICiclos ciclos) : IQueryHandler<ObtenerEvaluacionQuery, EvaluacionDto>
{
    public async Task<Result<EvaluacionDto>> Handle(ObtenerEvaluacionQuery q, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, q.Id, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var e = r.Value;
        var ciclo = await ciclos.PorIdAsync(e.EmpresaId, e.CicloId, ct);
        var b = e.Borrador;
        return new EvaluacionDto(e.Id, e.CicloId, ciclo?.Nombre ?? "", ciclo?.Estado.ToString() ?? "", e.Alcance.ToString(), e.PesoObjetivos, e.EmpleadoId, e.Codigo,
            e.Nombre, e.Cargo, e.Area, e.Sede, e.Estado.ToString(),
            [.. e.Objetivos.Select(o => new ObjetivoDto(o.Id, o.Descripcion, o.Meta, o.Peso, o.Avance, [.. o.Evidencias.Select(x => new EvidenciaDto(x.Texto, x.Fecha, x.Por))]))],
            [.. e.Competencias.Select(c => new CalificacionDto(c.Nombre, c.Descripcion, c.Peso, c.Lider, c.Auto,
                c.Pares.Count == 0 ? null : Math.Round((decimal)c.Pares.Average(), 1), c.Pares.Count))],
            e.ComentarioLider, e.ComentarioAuto, e.AutoevaluadaEn, e.CalificadaPor, e.CalificadaEn,
            b is null ? null : new BorradorDto(b.Estado, b.Fortalezas, b.Oportunidades, b.Comentario, [.. b.Razones.Select(x => new RazonDto(x.Signo, x.Texto, x.Fuente))],
                b.Modelo, b.Error),
            e.Potencial?.ToString(), e.Puntaje, e.CalcularPuntaje(), e.Caja, e.Caja is { } caja ? Ciclos.Ciclos.NombresCaja[caja - 1] : null,
            e.Ajuste is { } a ? new AjusteDto(a.Caja, a.Motivo, a.Por, a.En) : null,
            [.. e.Plan.Select(p => new AccionPlanDto(p.Id, p.Competencia, p.Accion, p.Fecha, p.Cumplida, p.Origen))]);
    }
}

/* ---------- Comandos ---------- */

internal sealed class DefinirObjetivosHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<DefinirObjetivosCommand>
{
    public async Task<Result> Handle(DefinirObjetivosCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var hecho = r.Value.DefinirObjetivos([.. c.Objetivos.Select(o => (o.Id, o.Descripcion, o.Meta ?? "", o.Peso))], reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class RegistrarAvanceHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<RegistrarAvanceCommand>
{
    public async Task<Result> Handle(RegistrarAvanceCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var hecho = r.Value.RegistrarAvance(c.ObjetivoId, c.Avance, c.Evidencia, usuario.Nombre, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class RegistrarParHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<RegistrarParCommand>
{
    public async Task<Result> Handle(RegistrarParCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var hecho = r.Value.RegistrarPar(c.Calificaciones, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class PedirBorradorHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, IEventosDesempeno eventos, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<PedirBorradorCommand>
{
    public async Task<Result> Handle(PedirBorradorCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        if (e.Estado == EstadoEvaluacion.Calificada)
        {
            return Result.Failure(EvaluacionErrors.Cerrada);
        }

        var ahora = reloj.GetUtcNow();
        e.PedirBorrador(ahora);
        // Sin nombre, documento ni datos protegidos: el cargo, los objetivos con sus evidencias y la autoevaluación
        eventos.Publicar(new BorradorEvaluacionSolicitado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.Id, e.Cargo,
            [.. e.Objetivos.Select(o => new ObjetivoParaCopiloto(o.Descripcion, o.Meta, o.Avance, [.. o.Evidencias.Select(x => x.Texto)]))],
            [.. e.Competencias.Select(x => new CompetenciaParaCopiloto(x.Nombre, x.Descripcion, x.Auto))], e.ComentarioAuto));
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class UsarBorradorHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<UsarBorradorCommand>
{
    public async Task<Result> Handle(UsarBorradorCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var hecho = r.Value.UsarBorrador(reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(r.Value.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Usó el borrador del copiloto en la evaluación de {r.Value.Nombre}",
            r.Value.Codigo, "Lo edita antes de calificar", "Evaluacion", r.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class CalificarEvaluacionHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, ICiclos ciclos, IBitacora bitacora, IUnidadDesempeno unidad,
    TimeProvider reloj) : ICommandHandler<CalificarEvaluacionCommand>
{
    public async Task<Result> Handle(CalificarEvaluacionCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        if (await ciclos.PorIdAsync(e.EmpresaId, e.CicloId, ct) is not { Estado: EstadoCiclo.Abierto or EstadoCiclo.Calibracion })
        {
            return Result.Failure(CicloErrors.Estado);
        }

        var hecho = e.Calificar(c.Calificaciones, c.Comentario, Enum.Parse<Potencial>(c.Potencial, true), usuario.Nombre, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(e.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Calificó la evaluación de {e.Nombre}",
            $"{e.Codigo} · puntaje {e.Puntaje:0.00} · potencial {e.Potencial}", e.Borrador?.Estado == "Usado" ? "Partió del borrador del copiloto" : null, "Evaluacion", e.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class ReabrirEvaluacionHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, ICiclos ciclos, IBitacora bitacora, IUnidadDesempeno unidad,
    TimeProvider reloj) : ICommandHandler<ReabrirEvaluacionCommand>
{
    public async Task<Result> Handle(ReabrirEvaluacionCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        if (await ciclos.PorIdAsync(r.Value.EmpresaId, r.Value.CicloId, ct) is not { Estado: EstadoCiclo.Abierto or EstadoCiclo.Calibracion })
        {
            return Result.Failure(CicloErrors.Estado);
        }

        r.Value.Reabrir(reloj.GetUtcNow());
        bitacora.Registrar(r.Value.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Reabrió la evaluación de {r.Value.Nombre}", r.Value.Codigo, null, "Evaluacion", r.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class CalibrarEvaluacionHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, ICiclos ciclos, IBitacora bitacora, IUnidadDesempeno unidad,
    TimeProvider reloj) : ICommandHandler<CalibrarEvaluacionCommand>
{
    public async Task<Result> Handle(CalibrarEvaluacionCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        if (await ciclos.PorIdAsync(e.EmpresaId, e.CicloId, ct) is not { Estado: EstadoCiclo.Calibracion })
        {
            return Result.Failure(Error.Conflict("Desempeno.NoCalibracion", "La matriz se ajusta cuando el ciclo está en calibración."));
        }

        var antes = e.Caja;
        var hecho = e.Calibrar(c.Caja, c.Motivo, usuario.Nombre, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(e.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Movió a {e.Nombre} en la matriz 9-box",
            $"Caja {antes} → {c.Caja}", c.Motivo, "Evaluacion", e.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class DefinirPlanHandler(IUsuarioActual usuario, IEvaluaciones evaluaciones, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<DefinirPlanCommand>
{
    public async Task<Result> Handle(DefinirPlanCommand c, CancellationToken ct)
    {
        var r = await Evaluaciones.DelUsuarioAsync(evaluaciones, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        r.Value.DefinirPlan([.. c.Acciones.Select(a => (a.Id, a.Competencia ?? "", a.Accion, a.Fecha, a.Cumplida))], reloj.GetUtcNow());
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Llegó el borrador del copiloto: queda en violeta hasta que el líder lo use o lo ignore.</summary>
internal sealed class BorradorEvaluacionGeneradoConsumer(IEvaluaciones evaluaciones, IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<BorradorEvaluacionGenerado>
{
    public async Task Handle(BorradorEvaluacionGenerado e, CancellationToken ct)
    {
        var ev = await evaluaciones.PorIdInternoAsync(e.EvaluacionId, ct);
        if (ev?.Borrador is not { Estado: "Preparando" })
        {
            return;
        }

        ev.RegistrarBorrador(e.Fortalezas, e.Oportunidades, e.Comentario,
            [.. e.Razones.Select(x => new Domain.Ciclos.RazonPropuesta { Signo = x.Signo, Texto = x.Texto, Fuente = x.Fuente })], e.Modelo, e.CostoUsd, e.Error,
            reloj.GetUtcNow());
        bitacora.Registrar(ev.EmpresaId, TipoActor.Ia, "Copiloto del evaluador", $"Redactó el borrador de la evaluación de {ev.Nombre}",
            e.Error ?? $"{e.Fortalezas.Count} fortalezas · {e.Oportunidades.Count} oportunidades · {e.Modelo}", "La calificación la pone el líder", "Evaluacion", ev.Id);
        await unidad.SaveChangesAsync(ct);
    }
}
