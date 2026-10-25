using FluentValidation;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Application.Procesos;
using Selvana.Vinculacion.Contracts.Ia;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Onboarding;

public sealed record TareaRequest(Guid? Id, string Titulo, string? Descripcion, string Responsable, string? Asignado, int Dia);

/// <summary>Pedir (o volver a pedir) a la IA las tareas de inducción del cargo.</summary>
public sealed record RedactarPlanInduccionCommand(Guid ProcesoId) : ICommand;

/// <summary>RRHH ajusta las tareas del plan; lo que cambia deja de ser sugerencia de la IA.</summary>
public sealed record EditarPlanInduccionCommand(Guid ProcesoId, IReadOnlyList<TareaRequest> Tareas) : ICommand;

/// <summary>Decisión humana: aprobar el plan de inducción.</summary>
public sealed record AprobarPlanInduccionCommand(Guid ProcesoId) : ICommand;

/// <summary>Seguimiento: marcar una tarea como hecha, no aplica (con motivo) o pendiente.</summary>
public sealed record ActualizarTareaInduccionCommand(Guid ProcesoId, Guid TareaId, string Estado, string? Nota) : ICommand;

/// <summary>Decisión humana: cerrar el onboarding; el proceso termina y la persona queda vinculada.</summary>
public sealed record CerrarOnboardingCommand(Guid ProcesoId) : ICommand;

internal static class Induccion
{
    public const string TipoBandeja = "PlanInduccion";

    /// <summary>Lo que viaja al planificador: el cargo y sus condiciones, sin nombre, documento ni contacto de la persona.</summary>
    public static PlanInduccionSolicitado Solicitud(ProcesoVinculacion p, DateTimeOffset ahora)
    {
        var c = p.Condiciones;
        var clase = p.Afiliaciones.FirstOrDefault(a => a.Tipo == TipoAfiliacion.Arl)?.ClaseRiesgo ?? ReglasAlta.ClaseSugerida(p.Vacante.Cargo).Clase;
        return new PlanInduccionSolicitado(Guid.CreateVersion7(), ahora, p.EmpresaId, p.Id,
            new DatosInduccion(p.Empresa, p.EmpresaUsuaria, p.Vacante.Cargo, p.Alta.Area, p.Alta.Sede, ReglasContrato.Nombre(c.Tipo), c.Jornada, c.FechaIngreso,
                c.PeriodoPruebaDias, clase, p.Vacante.Descripcion, p.Vacante.Requisitos,
                [.. (p.Induccion?.Tareas ?? []).Select(t => new TareaExistente(t.Titulo, t.Responsable, t.Dia))]));
    }
}

internal sealed class EditarPlanInduccionValidator : AbstractValidator<EditarPlanInduccionCommand>
{
    public EditarPlanInduccionValidator()
    {
        RuleFor(c => c.Tareas).NotEmpty().WithMessage("El plan necesita al menos una tarea.").Must(t => t is null || t.Count <= 40)
            .WithMessage("El plan tiene demasiadas tareas: use máximo 40.");
        RuleForEach(c => c.Tareas).ChildRules(t =>
        {
            t.RuleFor(x => x.Titulo).NotEmpty().WithMessage("Cada tarea necesita su título.").MaximumLength(200);
            t.RuleFor(x => x.Descripcion).MaximumLength(1000);
            t.RuleFor(x => x.Responsable).NotEmpty().WithMessage("Elija quién hace cada tarea.").MaximumLength(80);
            t.RuleFor(x => x.Asignado).MaximumLength(120);
            t.RuleFor(x => x.Dia).InclusiveBetween(ReglasInduccion.DiaMinimo, ReglasInduccion.DiaMaximo)
                .WithMessage("El día va de 5 días antes a 180 días después del ingreso.");
        });
    }
}

internal sealed class ActualizarTareaInduccionValidator : AbstractValidator<ActualizarTareaInduccionCommand>
{
    public ActualizarTareaInduccionValidator() => RuleFor(c => c.Nota).MaximumLength(500).WithMessage("Resuma la nota en menos de 500 caracteres.");
}

/// <summary>El planificador entregó las tareas del cargo: el plan queda por aprobar en la bandeja.</summary>
internal sealed class PlanInduccionRedactadoConsumer(IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<PlanInduccionRedactado>
{
    public async Task Handle(PlanInduccionRedactado e, CancellationToken cancellationToken)
    {
        var p = await procesos.PorIdAsync(e.ProcesoId, cancellationToken);
        if (p is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        if (e.Error is not null)
        {
            if (p.FallaPlanInduccion(e.Error, ahora))
            {
                bitacora.Registrar(p.EmpresaId, TipoActor.Ia, "Planificador de inducción", "No pudo proponer el plan de inducción", $"{p.Codigo} · {e.Error}",
                    "Quedan las tareas legales; RRHH puede completar el plan a mano", "ProcesoVinculacion", p.Id);
                Avisar(p, eventos, ahora, null);
                await unidad.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (!p.RegistrarPlanInduccion(e.Tareas.Select(t => new TareaPropuesta(t.Titulo, t.Descripcion, t.Responsable, t.Dia, t.Fuente)),
                e.Razones.Select(r => new RazonOferta { Signo = r.Signo, Texto = r.Texto, Fuente = r.Fuente }), e.Alertas, e.Modelo, e.CostoUsd, ahora))
        {
            return;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Ia, "Planificador de inducción", "Propuso el plan de inducción",
            $"{p.Codigo} · {p.Induccion!.Tareas.Count(t => t.DeIa)} tareas del cargo y {p.Induccion.Tareas.Count(t => t.Obligatoria)} legales · {e.Modelo}",
            "Espera aprobación de RRHH", "ProcesoVinculacion", p.Id);
        Avisar(p, eventos, ahora, e.Modelo);
        await unidad.SaveChangesAsync(cancellationToken);
    }

    /// <summary>El plan (de la IA o solo con las tareas legales) llega a la bandeja para que una persona lo apruebe.</summary>
    private static void Avisar(ProcesoVinculacion p, IEventosVinculacion eventos, DateTimeOffset ahora, string? modelo)
    {
        var plan = p.Induccion!;
        var razones = plan.Razones.Count > 0
            ? plan.Razones.Select(r => new RazonPropuesta(r.Signo, r.Texto, r.Fuente)).ToList()
            : [new RazonPropuesta("−", "La IA no respondió: el plan solo trae las tareas legales", "Planificador")];
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Vinculacion", Induccion.TipoBandeja, p.Id,
            $"Plan de inducción por aprobar · {p.Persona.Nombre}",
            $"{plan.Tareas.Count} tareas para «{p.Vacante.Cargo}» desde el {p.Condiciones.FechaIngreso.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}: " +
            "las legales por reglas y las del cargo que propone la IA. Revíselas y apruebe el plan.",
            modelo is null ? null : "Planificador de inducción", modelo, [.. razones.Take(5)],
            [new CampoPropuesta("Tareas legales", plan.Tareas.Count(t => t.Obligatoria).ToString(System.Globalization.CultureInfo.InvariantCulture)),
             new CampoPropuesta("Tareas del cargo", plan.Tareas.Count(t => !t.Obligatoria).ToString(System.Globalization.CultureInfo.InvariantCulture)),
             new CampoPropuesta("Primera semana", string.Join(" · ", plan.Tareas.Where(t => t.Dia <= 5).Take(3).Select(t => t.Titulo)))],
            new AccionPropuesta("Aprobar el plan", "POST", $"/api/v1/vinculacion/procesos/{p.Id}/induccion/aprobar"),
            $"/consola/vinculacion/{p.Id}?paso=Onboarding", false, p.Condiciones.FechaIngreso));
    }
}

internal sealed class RedactarPlanInduccionHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<RedactarPlanInduccionCommand>
{
    public async Task<Result> Handle(RedactarPlanInduccionCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var ahora = reloj.GetUtcNow();
        var pedido = r.Value.PedirPlanInduccion(ahora);
        if (pedido.IsFailure)
        {
            return pedido;
        }

        eventos.Publicar(Induccion.Solicitud(r.Value, ahora));
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, r.Value.EmpresaId, Induccion.TipoBandeja, r.Value.Id, "Se pidió otra versión del plan", usuario.Nombre));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class EditarPlanInduccionHandler(IUsuarioActual usuario, IProcesos procesos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<EditarPlanInduccionCommand>
{
    public async Task<Result> Handle(EditarPlanInduccionCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var editado = p.EditarPlanInduccion([.. c.Tareas.Select(t => new TareaEditada(t.Id, t.Titulo, t.Descripcion ?? "", t.Responsable, t.Asignado, t.Dia))],
            usuario.Nombre, reloj.GetUtcNow());
        if (editado.IsFailure)
        {
            return editado;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Ajustó el plan de inducción", $"{p.Codigo} · {p.Induccion!.Tareas.Count} tareas",
            "Lo que cambió deja de ser sugerencia de la IA", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class AprobarPlanInduccionHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<AprobarPlanInduccionCommand>
{
    public async Task<Result> Handle(AprobarPlanInduccionCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        var aprobado = p.AprobarPlanInduccion(usuario.Nombre, ahora);
        if (aprobado.IsFailure)
        {
            return aprobado;
        }

        var plan = p.Induccion!;
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Induccion.TipoBandeja, p.Id, "Plan de inducción aprobado", usuario.Nombre));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Aprobó el plan de inducción", $"{p.Codigo} · {plan.Tareas.Count} tareas",
            plan.Tareas.Any(t => t.DeIa) ? $"{plan.Tareas.Count(t => t.DeIa)} tareas propuestas por la IA aprobadas sin cambios" : "Todas las tareas revisadas por una persona",
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ActualizarTareaInduccionHandler(IUsuarioActual usuario, IProcesos procesos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<ActualizarTareaInduccionCommand>
{
    public async Task<Result> Handle(ActualizarTareaInduccionCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        if (!Enum.TryParse<EstadoTarea>(c.Estado, ignoreCase: true, out var estado) || !Enum.IsDefined(estado))
        {
            return Result.Failure(Error.Validation("Induccion.EstadoInvalido", "Elija Hecha, No aplica o Pendiente."));
        }

        var p = r.Value;
        var t = p.ActualizarTarea(c.TareaId, estado, c.Nota, usuario.Nombre, reloj.GetUtcNow());
        if (t.IsFailure)
        {
            return Result.Failure(t.Error);
        }

        var accion = estado switch
        {
            EstadoTarea.Hecha => "Marcó como hecha una tarea de inducción",
            EstadoTarea.NoAplica => "Marcó como «no aplica» una tarea de inducción",
            _ => "Reabrió una tarea de inducción",
        };
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, accion, $"{p.Codigo} · {t.Value.Titulo}", t.Value.Nota, "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class CerrarOnboardingHandler(IUsuarioActual usuario, IProcesos procesos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<CerrarOnboardingCommand>
{
    public async Task<Result> Handle(CerrarOnboardingCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var cerrado = p.CerrarOnboarding(usuario.Nombre, reloj.GetUtcNow());
        if (cerrado.IsFailure)
        {
            return cerrado;
        }

        var plan = p.Induccion!;
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Cerró el onboarding de {p.Persona.Nombre}",
            $"{p.Codigo} · {plan.Tareas.Count(t => t.Estado == EstadoTarea.Hecha)} tareas hechas · {plan.Tareas.Count(t => t.Estado == EstadoTarea.NoAplica)} no aplican",
            "El proceso de vinculación terminó", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
