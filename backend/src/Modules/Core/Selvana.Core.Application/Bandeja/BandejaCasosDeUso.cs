using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Privacidad;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Domain.Bandeja;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Bandeja;

public sealed record PropuestaDto(
    Guid Id, string Modulo, string Tipo, Guid EntidadId, Guid? EmpresaUsuariaId, string Titulo, string Descripcion, string? Agente, string? Modelo,
    IReadOnlyList<RazonPropuesta> Razones, IReadOnlyList<CampoPropuesta> Campos, AccionPropuesta? Accion, string Revisar, bool AfectaPersona,
    DateOnly? VenceEl, bool Urgente, DateTimeOffset CreadaEn, string Estado, string? Decision, string? DecididaPor, DateTimeOffset? DecididaEn);

public sealed record ResumenBandejaDto(int Pendientes, int Urgentes, int AfectanPersonas);

/// <summary>Lo que hizo hoy un agente de IA, según la bitácora.</summary>
public sealed record ActividadAgenteDto(string Agente, int AccionesHoy, string UltimaAccion, DateTimeOffset UltimaVez, int EsperanAprobacion);

public interface IPropuestas
{
    Task<PropuestaBandeja?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    Task<PropuestaBandeja?> ObtenerPorEntidadAsync(Guid empresaId, string tipo, Guid entidadId, CancellationToken cancellationToken);

    void Agregar(PropuestaBandeja propuesta);
}

public interface ILecturaBandeja
{
    Task<Pagina<PropuestaDto>> PropuestasAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, string rol, string estado, PaginaSolicitada pagina, DateOnly hoy, CancellationToken cancellationToken);

    Task<ResumenBandejaDto> ResumenAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, string rol, DateOnly hoy, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActividadAgenteDto>> ActividadAgentesAsync(Guid empresaId, DateTimeOffset desde, CancellationToken cancellationToken);
}

/// <summary>Publicar y resolver propuestas desde Core (los demás módulos lo hacen con los eventos de Core.Contracts.Bandeja).</summary>
public interface IBandeja
{
    Task PublicarAsync(PropuestaPublicada propuesta, CancellationToken cancellationToken);

    Task ResolverAsync(Guid empresaId, string tipo, Guid entidadId, string decision, string por, CancellationToken cancellationToken);
}

/// <summary>Implementación del puerto: reemplaza la propuesta pendiente de la misma entidad o crea una nueva.</summary>
public sealed class BandejaAprobaciones(IPropuestas propuestas, TimeProvider reloj) : IBandeja
{
    public async Task PublicarAsync(PropuestaPublicada propuesta, CancellationToken cancellationToken)
    {
        var e = propuesta;
        var ahora = reloj.GetUtcNow();
        var p = await propuestas.ObtenerPorEntidadAsync(e.EmpresaId, e.Tipo, e.EntidadId, cancellationToken);
        if (p is null)
        {
            p = PropuestaBandeja.Crear(e.EmpresaId, e.Modulo, e.Tipo, e.EntidadId, ahora);
            propuestas.Agregar(p);
        }

        p.Actualizar(e.EmpresaUsuariaId, e.Titulo, e.Descripcion, e.Agente, e.Modelo,
            e.Razones.Select(r => new RazonBandeja { Signo = r.Signo, Texto = r.Texto, Fuente = r.Fuente }),
            e.Campos.Select(c => new CampoBandeja { Etiqueta = c.Etiqueta, Valor = c.Valor }),
            e.Accion is null ? null : new AccionBandeja { Etiqueta = e.Accion.Etiqueta, Metodo = e.Accion.Metodo, Ruta = e.Accion.Ruta },
            e.Revisar, e.AfectaPersona, e.VenceEl, e.ParaRoles, ahora);
    }

    public async Task ResolverAsync(Guid empresaId, string tipo, Guid entidadId, string decision, string por, CancellationToken cancellationToken) =>
        (await propuestas.ObtenerPorEntidadAsync(empresaId, tipo, entidadId, cancellationToken))?.Resolver(decision, por, reloj.GetUtcNow());
}

internal sealed class PropuestaPublicadaConsumer(IBandeja bandeja, IUnitOfWork unidad) : IIntegrationEventConsumer<PropuestaPublicada>
{
    public async Task Handle(PropuestaPublicada e, CancellationToken cancellationToken)
    {
        await bandeja.PublicarAsync(e, cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class PropuestaResueltaConsumer(IBandeja bandeja, IUnitOfWork unidad) : IIntegrationEventConsumer<PropuestaResuelta>
{
    public async Task Handle(PropuestaResuelta e, CancellationToken cancellationToken)
    {
        await bandeja.ResolverAsync(e.EmpresaId, e.Tipo, e.EntidadId, e.Decision, e.Por, cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}

/// <summary><paramref name="Estado"/>: Pendiente (por defecto), Resuelta o Descartada.</summary>
public sealed record ListarBandejaQuery(int? Pagina, int? Tamano, string? Estado) : IQuery<Pagina<PropuestaDto>>;

internal sealed class ListarBandejaHandler(IContextoUsuario usuario, ILecturaBandeja lectura, TimeProvider reloj) : IQueryHandler<ListarBandejaQuery, Pagina<PropuestaDto>>
{
    public async Task<Result<Pagina<PropuestaDto>>> Handle(ListarBandejaQuery q, CancellationToken cancellationToken) =>
        await lectura.PropuestasAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, usuario.Rol, q.Estado ?? nameof(EstadoPropuesta.Pendiente),
            new PaginaSolicitada(q.Pagina, q.Tamano), HoraColombia.Hoy(reloj.GetUtcNow()), cancellationToken);
}

public sealed record ObtenerResumenBandejaQuery : IQuery<ResumenBandejaDto>;

internal sealed class ObtenerResumenBandejaHandler(IContextoUsuario usuario, ILecturaBandeja lectura, TimeProvider reloj) : IQueryHandler<ObtenerResumenBandejaQuery, ResumenBandejaDto>
{
    public async Task<Result<ResumenBandejaDto>> Handle(ObtenerResumenBandejaQuery q, CancellationToken cancellationToken) =>
        await lectura.ResumenAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, usuario.Rol, HoraColombia.Hoy(reloj.GetUtcNow()), cancellationToken);
}

/// <summary>Qué hicieron hoy los agentes de IA (desde la medianoche de Colombia), según la bitácora.</summary>
public sealed record ObtenerActividadAgentesQuery : IQuery<IReadOnlyList<ActividadAgenteDto>>;

internal sealed class ObtenerActividadAgentesHandler(IContextoUsuario usuario, ILecturaBandeja lectura, TimeProvider reloj)
    : IQueryHandler<ObtenerActividadAgentesQuery, IReadOnlyList<ActividadAgenteDto>>
{
    public async Task<Result<IReadOnlyList<ActividadAgenteDto>>> Handle(ObtenerActividadAgentesQuery q, CancellationToken cancellationToken)
    {
        var hoy = HoraColombia.Hoy(reloj.GetUtcNow());
        // PostgreSQL (timestamptz) solo acepta UTC: la medianoche de Colombia se convierte antes de consultar
        var desde = new DateTimeOffset(hoy.ToDateTime(TimeOnly.MinValue), HoraColombia.Zona).ToUniversalTime();
        return Result.Success(await lectura.ActividadAgentesAsync(usuario.EmpresaId, desde, cancellationToken));
    }
}

/// <summary>Una persona descarta una sugerencia: queda registrada con su motivo (sirve para medir y mejorar al agente).</summary>
public sealed record DescartarPropuestaCommand(Guid PropuestaId, string Motivo) : ICommand;

internal sealed class DescartarPropuestaValidator : AbstractValidator<DescartarPropuestaCommand>
{
    public DescartarPropuestaValidator() => RuleFor(c => c.Motivo).NotEmpty().WithMessage(PropuestaErrors.SinMotivo.Description).MaximumLength(300);
}

internal sealed class DescartarPropuestaHandler(IContextoUsuario usuario, IPropuestas propuestas, IBitacora bitacora, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<DescartarPropuestaCommand>
{
    public async Task<Result> Handle(DescartarPropuestaCommand c, CancellationToken cancellationToken)
    {
        var p = await propuestas.ObtenerAsync(usuario.EmpresaId, c.PropuestaId, cancellationToken);
        if (p is null || (p.EmpresaUsuariaId is { } eu && usuario.EmpresasUsuarias is { } alcance && !alcance.Contains(eu)))
        {
            return Result.Failure(PropuestaErrors.NoExiste);
        }

        var descartada = p.Descartar(c.Motivo, usuario.Nombre, reloj.GetUtcNow());
        if (descartada.IsFailure)
        {
            return descartada;
        }

        await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Descartó la sugerencia «{p.Titulo}»", $"Motivo: {c.Motivo.Trim()}",
            p.Agente is null ? null : $"Propuesta de {p.Agente}", "Propuesta", p.Id, cancellationToken: cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
