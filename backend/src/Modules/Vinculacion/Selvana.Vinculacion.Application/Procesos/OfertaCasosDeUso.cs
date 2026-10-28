using FluentValidation;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Contracts.Eventos;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Procesos;

public sealed record CambiarCondicionesCommand(Guid ProcesoId, CondicionesRequest Condiciones) : ICommand;

/// <summary>Volver a pedir el borrador a la IA (si falló o si RRHH quiere otra versión).</summary>
public sealed record RedactarOfertaCommand(Guid ProcesoId) : ICommand;

public sealed record EditarOfertaCommand(Guid ProcesoId, string Carta, IReadOnlyList<ClausulaDto> Clausulas) : ICommand;

/// <summary>Decisión humana: aprobar la oferta. Devuelve el enlace del candidato.</summary>
public sealed record AprobarOfertaCommand(Guid ProcesoId) : ICommand<EnlaceOfertaDto>;

public sealed record RenovarEnlaceOfertaCommand(Guid ProcesoId) : ICommand<EnlaceOfertaDto>;

public sealed record CancelarProcesoCommand(Guid ProcesoId, string Motivo) : ICommand;

public sealed record ObtenerOfertaPublicaQuery(string Token) : IQuery<OfertaPublicaDto>;

public sealed record ResponderOfertaCommand(string Token, bool Acepta, string? Nombre, string? Documento, string? Motivo, string Ip, string Navegador) : ICommand;

internal static class Ofertas
{
    public const string TipoBandeja = "OfertaLaboral";

    public static async Task<Result<ProcesoVinculacion>> DelUsuarioAsync(IProcesos procesos, IUsuarioActual usuario, Guid id, CancellationToken ct)
    {
        var p = await procesos.PorIdAsync(id, ct);
        return p is null || p.EmpresaId != usuario.EmpresaId || !usuario.Ve(p.EmpresaUsuariaId) ? ProcesoErrors.NoExiste : p;
    }
}

internal sealed class EditarOfertaValidator : AbstractValidator<EditarOfertaCommand>
{
    public EditarOfertaValidator()
    {
        RuleFor(c => c.Carta).NotEmpty().WithMessage("La carta oferta no puede quedar vacía.").MaximumLength(6000);
        RuleFor(c => c.Clausulas).NotEmpty().WithMessage("El contrato necesita al menos una cláusula.").Must(c => c is null || c.Count <= 30)
            .WithMessage("El contrato tiene demasiadas cláusulas.");
        RuleForEach(c => c.Clausulas).ChildRules(x =>
        {
            x.RuleFor(c => c.Titulo).NotEmpty().WithMessage("Cada cláusula necesita su título.").MaximumLength(200);
            x.RuleFor(c => c.Texto).NotEmpty().WithMessage("Cada cláusula necesita su texto.").MaximumLength(4000);
        });
    }
}

internal sealed class CambiarCondicionesValidator : AbstractValidator<CambiarCondicionesCommand>
{
    public CambiarCondicionesValidator() => RuleFor(c => c.Condiciones).NotNull().SetValidator(new CondicionesRequestValidator());
}

internal sealed class CancelarProcesoValidator : AbstractValidator<CancelarProcesoCommand>
{
    public CancelarProcesoValidator() => RuleFor(c => c.Motivo).NotEmpty().WithMessage("Escriba por qué se cancela.").MaximumLength(500);
}

internal sealed class ResponderOfertaValidator : AbstractValidator<ResponderOfertaCommand>
{
    public ResponderOfertaValidator()
    {
        RuleFor(c => c.Nombre).MaximumLength(200);
        RuleFor(c => c.Documento).MaximumLength(20);
        RuleFor(c => c.Motivo).MaximumLength(500).WithMessage("Resume el motivo en menos de 500 caracteres.");
    }
}

/// <summary>La IA entregó el borrador: queda por aprobar en la bandeja (afecta a una persona).</summary>
internal sealed class OfertaRedactadaConsumer(IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<OfertaRedactada>
{
    public async Task Handle(OfertaRedactada e, CancellationToken cancellationToken)
    {
        var p = await procesos.PorIdAsync(e.ProcesoId, cancellationToken);
        if (p is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        if (e.Error is not null || e.Clausulas.Count == 0)
        {
            if (p.FallaRedaccion(e.Error ?? "La IA no devolvió cláusulas.", ahora))
            {
                await unidad.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (!p.RegistrarBorrador(e.Carta, e.Clausulas.Select(c => new Clausula { Titulo = c.Titulo, Texto = c.Texto }),
                e.Razones.Select(r => new RazonOferta { Signo = r.Signo, Texto = r.Texto, Fuente = r.Fuente }), e.Alertas, e.Modelo, e.CostoUsd, ahora))
        {
            return;
        }

        var c = p.Condiciones;
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Vinculacion", Ofertas.TipoBandeja, p.Id,
            $"Oferta por aprobar · {p.Persona.Nombre}", $"La IA redactó la carta oferta y el contrato de {ReglasContrato.Nombre(c).ToLowerInvariant()} para «{p.Vacante.Cargo}». Revíselos y apruébelos.",
            "Redactor de ofertas", e.Modelo, [.. e.Razones.Select(r => new RazonPropuesta(r.Signo, r.Texto, r.Fuente))],
            [new CampoPropuesta("Contrato", ReglasContrato.Nombre(c)), new CampoPropuesta("Salario", ReglasContrato.Pesos(c.SalarioCop)),
             new CampoPropuesta("Ingreso", c.FechaIngreso.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture))],
            null, $"/consola/vinculacion/{p.Id}", true, c.FechaIngreso));
        bitacora.Registrar(p.EmpresaId, TipoActor.Ia, "Redactor de ofertas", "Redactó la carta oferta y el contrato", $"{p.Codigo} · {e.Clausulas.Count} cláusulas",
            "Espera aprobación de RRHH", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class CambiarCondicionesHandler(IUsuarioActual usuario, IProcesos procesos, IParametrosLegales parametros, IEventosVinculacion eventos,
    ITiposContrato tipos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<CambiarCondicionesCommand>
{
    public async Task<Result> Handle(CambiarCondicionesCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var condiciones = await Contratacion.CondicionesAsync(c.Condiciones, p.Condiciones.Jornada, p.Condiciones.Ciudad, p.EmpresaId, tipos, cancellationToken);
        if (condiciones.IsFailure)
        {
            return condiciones;
        }

        var ahora = reloj.GetUtcNow();
        var cambio = p.CambiarCondiciones(condiciones.Value, ahora);
        if (cambio.IsFailure)
        {
            return cambio;
        }

        eventos.Publicar(Contratacion.Solicitud(p, parametros.Actuales, ahora));
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Ofertas.TipoBandeja, p.Id, "Cambiaron las condiciones", usuario.Nombre));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Cambió las condiciones de la contratación",
            $"{p.Codigo} · {ReglasContrato.Nombre(p.Condiciones)} · {ReglasContrato.Pesos(p.Condiciones.SalarioCop)}", "La IA redacta de nuevo la oferta",
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class RedactarOfertaHandler(IUsuarioActual usuario, IProcesos procesos, IParametrosLegales parametros, IEventosVinculacion eventos,
    IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<RedactarOfertaCommand>
{
    public async Task<Result> Handle(RedactarOfertaCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var ahora = reloj.GetUtcNow();
        var pedido = r.Value.PedirRedaccion(ahora);
        if (pedido.IsFailure)
        {
            return pedido;
        }

        eventos.Publicar(Contratacion.Solicitud(r.Value, parametros.Actuales, ahora));
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, r.Value.EmpresaId, Ofertas.TipoBandeja, r.Value.Id, "Se pidió otra redacción", usuario.Nombre));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class EditarOfertaHandler(IUsuarioActual usuario, IProcesos procesos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<EditarOfertaCommand>
{
    public async Task<Result> Handle(EditarOfertaCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var editada = p.EditarOferta(c.Carta, [.. c.Clausulas.Select(x => new Clausula { Titulo = x.Titulo, Texto = x.Texto })], usuario.Nombre, reloj.GetUtcNow());
        if (editada.IsFailure)
        {
            return editada;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Editó la carta oferta y el contrato", p.Codigo, "El texto deja de ser sugerencia de la IA",
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class AprobarOfertaHandler(IUsuarioActual usuario, IProcesos procesos, IParametrosLegales parametros, ITokensEnlace tokens, IEventosVinculacion eventos,
    IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<AprobarOfertaCommand, EnlaceOfertaDto>
{
    public async Task<Result<EnlaceOfertaDto>> Handle(AprobarOfertaCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        var alertas = ReglasContrato.Revisar(p.Condiciones, parametros.Actuales, DateOnly.FromDateTime(ahora.UtcDateTime), p.EmpresaUsuaria,
            p.Vacante.SalarioMinimo, p.Vacante.SalarioMaximo);
        var (token, huella) = tokens.Generar();
        var aprobada = p.AprobarOferta(alertas, huella, usuario.Nombre, ahora);
        if (aprobada.IsFailure)
        {
            return aprobada.Error;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Ofertas.TipoBandeja, p.Id, "Oferta aprobada y enviada", usuario.Nombre));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Aprobó la oferta laboral y la envió al candidato",
            $"{p.Codigo} · {p.Persona.Nombre}", p.Oferta.EditadaPor is null ? "Texto redactado por la IA, aprobado sin cambios" : $"Texto editado por {p.Oferta.EditadaPor}",
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return new EnlaceOfertaDto(p.Id, token);
    }
}

internal sealed class RenovarEnlaceOfertaHandler(IUsuarioActual usuario, IProcesos procesos, ITokensEnlace tokens, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<RenovarEnlaceOfertaCommand, EnlaceOfertaDto>
{
    public async Task<Result<EnlaceOfertaDto>> Handle(RenovarEnlaceOfertaCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var (token, huella) = tokens.Generar();
        var renovado = r.Value.RenovarEnlace(huella, reloj.GetUtcNow());
        if (renovado.IsFailure)
        {
            return renovado.Error;
        }

        bitacora.Registrar(r.Value.EmpresaId, TipoActor.Persona, usuario.Nombre, "Generó un enlace nuevo de la oferta", r.Value.Codigo, "El enlace anterior dejó de servir",
            "ProcesoVinculacion", r.Value.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return new EnlaceOfertaDto(r.Value.Id, token);
    }
}

internal sealed class CancelarProcesoHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<CancelarProcesoCommand>
{
    public async Task<Result> Handle(CancelarProcesoCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        var cancelado = p.Cancelar(c.Motivo, ahora);
        if (cancelado.IsFailure)
        {
            return cancelado;
        }

        eventos.Publicar(new ContratacionCancelada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.Vacante.PostulacionId, p.Id, c.Motivo.Trim(), usuario.Nombre));
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Ofertas.TipoBandeja, p.Id, "Proceso cancelado", usuario.Nombre));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Canceló el proceso de vinculación", $"{p.Codigo} · {p.Persona.Nombre}", c.Motivo.Trim(),
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ObtenerOfertaPublicaHandler(IProcesos procesos, ITokensEnlace tokens, IParametrosLegales parametros) : IQueryHandler<ObtenerOfertaPublicaQuery, OfertaPublicaDto>
{
    public async Task<Result<OfertaPublicaDto>> Handle(ObtenerOfertaPublicaQuery q, CancellationToken ct)
    {
        var p = await procesos.PorTokenAsync(tokens.Huella(q.Token), ct);
        if (p is null || p.Etapa == EtapaVinculacion.Cancelado || p.Oferta.Estado is not (EstadoOferta.Enviada or EstadoOferta.Aceptada or EstadoOferta.Rechazada))
        {
            return ProcesoErrors.EnlaceNoExiste;
        }

        var r = p.Oferta.Respuesta;
        return new OfertaPublicaDto(p.Empresa, p.EmpresaUsuaria, p.Persona.Nombre, p.Vacante.Cargo, p.Oferta.Estado.ToString(), p.Personalizar(p.Oferta.Carta),
            Mapeo.Clausulas(p), Mapeo.Dto(p.Condiciones, parametros.Actuales), r?.En, r?.Acepta, r?.Huella, p.Etapa.ToString(),
            [.. Mapeo.DocumentosDe(p).Where(d => !d.LoCargaEmpresa).Select(d => new DocumentoPublicoDto(d.Tipo.ToString(), RequisitosDocumentales.Nombre(d.Tipo), d.Obligatorio,
                d.Estado.ToString(), d.NombreArchivo, d.MotivoRechazo))]);
    }
}

internal sealed class ResponderOfertaHandler(IProcesos procesos, ITokensEnlace tokens, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<ResponderOfertaCommand>
{
    public async Task<Result> Handle(ResponderOfertaCommand c, CancellationToken cancellationToken)
    {
        var p = await procesos.PorTokenAsync(tokens.Huella(c.Token), cancellationToken);
        if (p is null || p.Etapa == EtapaVinculacion.Cancelado)
        {
            return Result.Failure(ProcesoErrors.EnlaceNoExiste);
        }

        var respuesta = p.ResponderOferta(c.Acepta, c.Nombre, c.Documento, c.Motivo, c.Ip, c.Navegador, reloj.GetUtcNow());
        if (respuesta.IsFailure)
        {
            return respuesta;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Titular, p.Persona.Nombre, c.Acepta ? "Aceptó la oferta laboral" : "No aceptó la oferta laboral",
            $"{p.Codigo} · huella {p.Oferta.Respuesta!.Huella[..12]}…", c.Acepta ? "Aceptación electrónica con fecha, IP, navegador y documento" : p.Oferta.Respuesta.Motivo,
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
