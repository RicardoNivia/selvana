using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Contracts.Eventos;
using Selvana.Vinculacion.Contracts.Ia;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Procesos;

/// <summary>Condiciones prellenadas para contratar a un candidato de la terna, con sus razones y alertas.</summary>
public sealed record ObtenerContratacionSugeridaQuery(Guid PostulacionId) : IQuery<ContratacionSugeridaDto>;

/// <summary>Revisa por reglas unas condiciones mientras RRHH las ajusta (salario mínimo, duración, periodo de prueba…).</summary>
public sealed record RevisarCondicionesQuery(Guid PostulacionId, CondicionesRequest Condiciones) : IQuery<IReadOnlyList<AlertaDto>>;

/// <summary>Decisión humana: contratar al candidato de la terna. Abre el proceso y la IA redacta la oferta.</summary>
public sealed record AprobarContratacionCommand(Guid PostulacionId, CondicionesRequest Condiciones) : ICommand<Guid>;

internal static class Contratacion
{
    public static Result<CondicionesContratacion> Condiciones(CondicionesRequest c, string jornada, string ciudad) =>
        Enum.TryParse<TipoContrato>(c.Tipo, ignoreCase: true, out var tipo) && Enum.IsDefined(tipo)
            ? new CondicionesContratacion
            {
                Tipo = tipo,
                SalarioCop = Math.Round(c.SalarioCop),
                FechaIngreso = c.FechaIngreso,
                FechaFin = tipo == TipoContrato.Indefinido ? null : c.FechaFin,
                PeriodoPruebaDias = c.PeriodoPruebaDias,
                Labor = string.IsNullOrWhiteSpace(c.Labor) ? null : c.Labor.Trim(),
                Jornada = jornada,
                Ciudad = ciudad,
            }
            : Error.Validation("Vinculacion.TipoInvalido", "Elija un tipo de contrato de la lista.");

    public static OfertaSolicitada Solicitud(ProcesoVinculacion p, ParametrosLegales parametros, DateTimeOffset ahora)
    {
        var c = p.Condiciones;
        return new OfertaSolicitada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.Id,
            new CondicionesOferta(p.Empresa, p.EmpresaUsuaria, p.Vacante.Cargo, c.Ciudad, c.Jornada, ReglasContrato.Nombre(c.Tipo), c.SalarioCop,
                ReglasContrato.TieneAuxilioTransporte(c, parametros), c.FechaIngreso, c.FechaFin, c.PeriodoPruebaDias, c.Labor, p.Vacante.Descripcion,
                p.Vacante.Requisitos));
    }
}

internal sealed class CondicionesRequestValidator : AbstractValidator<CondicionesRequest>
{
    public CondicionesRequestValidator()
    {
        RuleFor(c => c.Tipo).NotEmpty().WithMessage("Elija el tipo de contrato.");
        RuleFor(c => c.SalarioCop).GreaterThan(0).WithMessage("Escriba el salario.").LessThan(1_000_000_000).WithMessage("Revise el salario: es demasiado alto.");
        RuleFor(c => c.PeriodoPruebaDias).InclusiveBetween(0, 365).WithMessage("El periodo de prueba va en días, entre 0 y 60.");
        RuleFor(c => c.Labor).MaximumLength(500).WithMessage("Resuma la obra o labor en menos de 500 caracteres.");
    }
}

internal sealed class AprobarContratacionValidator : AbstractValidator<AprobarContratacionCommand>
{
    public AprobarContratacionValidator() => RuleFor(c => c.Condiciones).NotNull().SetValidator(new CondicionesRequestValidator());
}

internal sealed class RevisarCondicionesValidator : AbstractValidator<RevisarCondicionesQuery>
{
    public RevisarCondicionesValidator() => RuleFor(c => c.Condiciones).NotNull().SetValidator(new CondicionesRequestValidator());
}

internal sealed class ObtenerContratacionSugeridaHandler(IUsuarioActual usuario, IPostulacionesContratables postulaciones, IParametrosLegales parametros,
    TimeProvider reloj) : IQueryHandler<ObtenerContratacionSugeridaQuery, ContratacionSugeridaDto>
{
    public async Task<Result<ContratacionSugeridaDto>> Handle(ObtenerContratacionSugeridaQuery q, CancellationToken ct)
    {
        var p = await postulaciones.ObtenerAsync(usuario.EmpresaId, q.PostulacionId, ct);
        if (p is null || !usuario.Ve(p.EmpresaUsuariaId))
        {
            return ProcesoErrors.NoExiste;
        }

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var legales = parametros.Actuales;
        var s = SugeridorCondiciones.Sugerir(p.Cargo, p.EmpresaUsuaria, p.Jornada, p.CiudadVacante, p.SalarioMinimo, p.SalarioMaximo, p.AspiracionSalarialCop, legales, hoy);
        var alertas = ReglasContrato.Revisar(s.Condiciones, legales, hoy, p.EmpresaUsuaria, p.SalarioMinimo, p.SalarioMaximo);
        return new ContratacionSugeridaDto(p.PostulacionId, p.Nombre, p.Documento, p.Vacante, p.CodigoVacante, p.Cargo, p.EmpresaUsuaria, p.SalarioMinimo,
            p.SalarioMaximo, p.AspiracionSalarialCop, Mapeo.Dto(s.Condiciones, legales), [.. s.Razones.Select(Mapeo.Dto)], [.. alertas.Select(Mapeo.Dto)],
            p.ProcesoVinculacionId);
    }
}

internal sealed class RevisarCondicionesHandler(IUsuarioActual usuario, IPostulacionesContratables postulaciones, IParametrosLegales parametros, TimeProvider reloj)
    : IQueryHandler<RevisarCondicionesQuery, IReadOnlyList<AlertaDto>>
{
    public async Task<Result<IReadOnlyList<AlertaDto>>> Handle(RevisarCondicionesQuery q, CancellationToken ct)
    {
        var p = await postulaciones.ObtenerAsync(usuario.EmpresaId, q.PostulacionId, ct);
        if (p is null || !usuario.Ve(p.EmpresaUsuariaId))
        {
            return ProcesoErrors.NoExiste;
        }

        var c = Contratacion.Condiciones(q.Condiciones, p.Jornada, p.CiudadVacante);
        if (c.IsFailure)
        {
            return c.Error;
        }

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        return ReglasContrato.Revisar(c.Value, parametros.Actuales, hoy, p.EmpresaUsuaria, p.SalarioMinimo, p.SalarioMaximo).Select(Mapeo.Dto).ToList();
    }
}

internal sealed class AprobarContratacionHandler(
    IUsuarioActual usuario, IPostulacionesContratables postulaciones, IProcesos procesos, IParametrosLegales parametros, IEventosVinculacion eventos,
    IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<AprobarContratacionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AprobarContratacionCommand c, CancellationToken cancellationToken)
    {
        var p = await postulaciones.ObtenerAsync(usuario.EmpresaId, c.PostulacionId, cancellationToken);
        if (p is null || !usuario.Ve(p.EmpresaUsuariaId))
        {
            return ProcesoErrors.NoExiste;
        }

        if (p.Etapa != "Terna")
        {
            return ProcesoErrors.NoEstaEnTerna;
        }

        if (await procesos.AbiertoDePostulacionAsync(usuario.EmpresaId, p.PostulacionId, cancellationToken) is not null)
        {
            return ProcesoErrors.YaEnProceso;
        }

        var condiciones = Contratacion.Condiciones(c.Condiciones, p.Jornada, p.CiudadVacante);
        if (condiciones.IsFailure)
        {
            return condiciones.Error;
        }

        var ahora = reloj.GetUtcNow();
        var proceso = ProcesoVinculacion.Abrir(usuario.EmpresaId, p.Empresa, p.EmpresaUsuariaId, p.EmpresaUsuaria,
            new PersonaVinculada { Nombre = p.Nombre, Documento = p.Documento, Correo = p.Correo, Celular = p.Celular, Ciudad = p.Ciudad },
            new VacanteOrigen
            {
                VacanteId = p.VacanteId,
                PostulacionId = p.PostulacionId,
                CandidatoId = p.CandidatoId,
                Codigo = p.CodigoVacante,
                Titulo = p.Vacante,
                Cargo = p.Cargo,
                Descripcion = p.Descripcion,
                Requisitos = [.. p.Requisitos],
                SalarioMinimo = p.SalarioMinimo,
                SalarioMaximo = p.SalarioMaximo,
            },
            condiciones.Value, usuario.Nombre, ahora);
        procesos.Agregar(proceso);

        eventos.Publicar(new ContratacionAprobada(Guid.CreateVersion7(), ahora, usuario.EmpresaId, p.PostulacionId, proceso.Id, usuario.Nombre));
        eventos.Publicar(Contratacion.Solicitud(proceso, parametros.Actuales, ahora));
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó la contratación de la postulación {p.NumeroPostulacion}",
            $"{proceso.Codigo} · {p.Vacante} · {ReglasContrato.Nombre(proceso.Condiciones.Tipo)} · {ReglasContrato.Pesos(proceso.Condiciones.SalarioCop)}",
            "Decisión humana sobre una persona. La IA redacta la oferta y RRHH la aprueba", "ProcesoVinculacion", proceso.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return proceso.Id;
    }
}
