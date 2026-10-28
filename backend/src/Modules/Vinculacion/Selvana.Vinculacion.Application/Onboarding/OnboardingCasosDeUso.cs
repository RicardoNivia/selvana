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

public sealed record TareaInduccionRequest(Guid? Id, string Titulo, string Detalle, string Responsable, string Momento, int Dia);

/// <summary>Pedir (o volver a pedir) el plan de inducción a la IA.</summary>
public sealed record PedirPlanInduccionCommand(Guid ProcesoId) : ICommand;

/// <summary>Plan por reglas, sin IA.</summary>
public sealed record UsarPlanBaseCommand(Guid ProcesoId) : ICommand;

public sealed record EditarPlanInduccionCommand(Guid ProcesoId, IReadOnlyList<TareaInduccionRequest> Tareas) : ICommand;

/// <summary>Decisión humana: aprobar el plan y arrancar el seguimiento.</summary>
public sealed record AprobarPlanInduccionCommand(Guid ProcesoId) : ICommand;

public sealed record MarcarTareaInduccionCommand(Guid ProcesoId, Guid TareaId, bool Hecha) : ICommand;

/// <summary>Todas las tareas hechas: el proceso queda «Vinculado».</summary>
public sealed record CerrarOnboardingCommand(Guid ProcesoId) : ICommand;

internal static class Onboardings
{
    public const string TipoBandeja = "PlanInduccion";

    public static OnboardingSolicitado Solicitud(ProcesoVinculacion p, DateTimeOffset ahora) =>
        new(Guid.CreateVersion7(), ahora, p.EmpresaId, p.Id, p.Empresa, p.EmpresaUsuaria, p.Vacante.Cargo, p.Vacante.Descripcion, p.Vacante.Requisitos,
            ReglasContrato.Nombre(p.Condiciones), p.Condiciones.TipoContratoDescripcion, p.Condiciones.FechaIngreso, p.Condiciones.PeriodoPruebaDias,
            p.Condiciones.Jornada, p.Condiciones.Ciudad);

    public static Result<List<TareaPropuesta>> Tareas(IEnumerable<TareaInduccionRequest> tareas)
    {
        var lista = new List<TareaPropuesta>();
        foreach (var t in tareas)
        {
            if (!Enum.TryParse<MomentoInduccion>(t.Momento, ignoreCase: true, out var momento) || !Enum.IsDefined(momento))
            {
                return Error.Validation("Vinculacion.MomentoInvalido", $"Elija cuándo va la tarea «{t.Titulo}».");
            }

            lista.Add(new TareaPropuesta(t.Titulo, t.Detalle ?? "", t.Responsable, momento, t.Dia, t.Id));
        }

        return lista;
    }
}

internal sealed class TareaInduccionValidator : AbstractValidator<TareaInduccionRequest>
{
    public TareaInduccionValidator()
    {
        RuleFor(t => t.Titulo).NotEmpty().WithMessage("Cada tarea necesita un título.").MaximumLength(200);
        RuleFor(t => t.Detalle).MaximumLength(1000);
        RuleFor(t => t.Responsable).NotEmpty().WithMessage("Diga quién hace cada tarea.").MaximumLength(100);
        RuleFor(t => t.Dia).InclusiveBetween(-30, 180).WithMessage("El día va entre 30 días antes y 180 días después del ingreso.");
    }
}

internal sealed class EditarPlanInduccionValidator : AbstractValidator<EditarPlanInduccionCommand>
{
    public EditarPlanInduccionValidator()
    {
        RuleFor(c => c.Tareas).NotEmpty().WithMessage("El plan necesita al menos una tarea.")
            .Must(t => t.Count <= ProcesoVinculacion.TareasMaximas).WithMessage($"El plan admite hasta {ProcesoVinculacion.TareasMaximas} tareas.");
        RuleForEach(c => c.Tareas).SetValidator(new TareaInduccionValidator());
    }
}

internal sealed class PedirPlanInduccionHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<PedirPlanInduccionCommand>
{
    public async Task<Result> Handle(PedirPlanInduccionCommand c, CancellationToken cancellationToken)
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

        eventos.Publicar(Onboardings.Solicitud(r.Value, ahora));
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, r.Value.EmpresaId, Onboardings.TipoBandeja, r.Value.Id, "Se pidió otro plan", usuario.Nombre));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class UsarPlanBaseHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<UsarPlanBaseCommand>
{
    public async Task<Result> Handle(UsarPlanBaseCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        var usado = p.UsarPlanBase(ahora);
        if (usado.IsFailure)
        {
            return usado;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Onboardings.TipoBandeja, p.Id, "Se usó el plan base", usuario.Nombre));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Usó el plan base de inducción", $"{p.Codigo} · {p.Induccion.Tareas.Count} tareas",
            "Plan por reglas, sin IA", "ProcesoVinculacion", p.Id);
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

        var tareas = Onboardings.Tareas(c.Tareas);
        if (tareas.IsFailure)
        {
            return Result.Failure(tareas.Error);
        }

        var p = r.Value;
        var editado = p.EditarPlanInduccion(tareas.Value, reloj.GetUtcNow());
        if (editado.IsFailure)
        {
            return editado;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Ajustó el plan de inducción", $"{p.Codigo} · {p.Induccion.Tareas.Count} tareas",
            "Editar lo que propuso la IA cuenta como confirmarlo", "ProcesoVinculacion", p.Id);
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

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Onboardings.TipoBandeja, p.Id, "Plan aprobado", usuario.Nombre));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó el plan de inducción de {p.Persona.Nombre}",
            $"{p.Codigo} · {p.Induccion.Tareas.Count} tareas · origen {p.Induccion.Origen}", "Decisión humana; arranca el seguimiento", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class MarcarTareaInduccionHandler(IUsuarioActual usuario, IProcesos procesos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<MarcarTareaInduccionCommand>
{
    public async Task<Result> Handle(MarcarTareaInduccionCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var tarea = p.MarcarTareaInduccion(c.TareaId, c.Hecha, usuario.Nombre, reloj.GetUtcNow());
        if (tarea.IsFailure)
        {
            return Result.Failure(tarea.Error);
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, c.Hecha ? $"Marcó hecha «{tarea.Value.Titulo}»" : $"Reabrió «{tarea.Value.Titulo}»",
            $"{p.Codigo} · avance {p.Induccion.Avance} %", null, "ProcesoVinculacion", p.Id);
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

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Cerró el onboarding de {p.Persona.Nombre}", $"{p.Codigo} · {p.Induccion.Tareas.Count} tareas hechas",
            "El proceso de vinculación queda terminado", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>La IA propuso el plan: queda por aprobar en la bandeja. Si falló, RRHH usa el plan base o lo pide de nuevo.</summary>
internal sealed class PlanInduccionGeneradoConsumer(IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<PlanInduccionGenerado>
{
    public async Task Handle(PlanInduccionGenerado e, CancellationToken cancellationToken)
    {
        var p = await procesos.PorIdAsync(e.ProcesoId, cancellationToken);
        if (p is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        var tareas = e.Tareas
            .Select(t => Enum.TryParse<MomentoInduccion>(t.Momento, ignoreCase: true, out var m) && Enum.IsDefined(m)
                ? new TareaPropuesta(t.Titulo, t.Detalle, t.Responsable, m, t.Dia) : null)
            .OfType<TareaPropuesta>().ToList();
        if (e.Error is not null || tareas.Count == 0)
        {
            if (p.FallaPlanInduccion(e.Error ?? "La IA no devolvió tareas.", ahora))
            {
                await unidad.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (!p.RegistrarPlanInduccion(tareas, e.Razones.Select(r => new RazonOferta { Signo = r.Signo, Texto = r.Texto, Fuente = r.Fuente }), e.Alertas, e.Modelo,
                e.CostoUsd, ahora))
        {
            return;
        }

        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Vinculacion", Onboardings.TipoBandeja, p.Id,
            $"Plan de inducción por aprobar · {p.Persona.Nombre}", $"La IA preparó {tareas.Count} tareas de inducción para «{p.Vacante.Cargo}». Revíselas, ajústelas y apruébelas.",
            "Planificador de inducción", e.Modelo, [.. e.Razones.Select(r => new RazonPropuesta(r.Signo, r.Texto, r.Fuente))],
            [new CampoPropuesta("Cargo", p.Vacante.Cargo), new CampoPropuesta("Tareas", tareas.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
             new CampoPropuesta("Ingreso", p.Condiciones.FechaIngreso.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture))],
            null, $"/consola/vinculacion/{p.Id}?paso=Onboarding", true, p.Condiciones.FechaIngreso));
        bitacora.Registrar(p.EmpresaId, TipoActor.Ia, "Planificador de inducción", "Preparó el plan de inducción", $"{p.Codigo} · {tareas.Count} tareas",
            "Espera aprobación de RRHH", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}
