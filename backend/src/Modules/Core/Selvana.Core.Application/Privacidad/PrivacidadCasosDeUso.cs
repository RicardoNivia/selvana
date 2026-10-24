using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Bandeja;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Contracts.Empresas;
using Selvana.Core.Contracts.Privacidad;
using Selvana.Core.Domain.Auditoria;
using Selvana.Core.Domain.Privacidad;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Privacidad;

public sealed record EntradaBitacoraDto(
    Guid Id, DateTimeOffset Fecha, string TipoActor, string Actor, string Accion, string Detalle, string? Nota, string Modulo, bool DatoSensible, string Huella);

public sealed record VerificacionDto(bool Integra, int Revisadas, DateTimeOffset? FechaAlterada, string Mensaje);

public sealed record ResumenPrivacidadDto(int SolicitudesAbiertas, int VencenEnTresDias, int Vencidas, int AccesosSensiblesSieteDias, int EntradasHoy);

public sealed record SolicitudDto(
    Guid Id, string Tipo, string Titular, string Documento, string Contacto, string Canal, string Descripcion, DateTimeOffset RecibidaEn,
    DateOnly Vence, int DiasHabilesRestantes, int PlazoDiasHabiles, string BorradorRespuesta, string Estado, string? Respuesta, string? RespondidaPor,
    DateTimeOffset? RespondidaEn, bool EsSupresion, bool SupresionAprobada);

/// <summary>Lado de lectura de privacidad y auditoría.</summary>
public interface ILecturaPrivacidad
{
    Task<Pagina<EntradaBitacoraDto>> BitacoraAsync(Guid empresaId, PaginaSolicitada pagina, string? tipo, string? buscar, bool soloSensibles, CancellationToken cancellationToken);

    /// <summary>Toda la cadena de la empresa en orden (para verificarla).</summary>
    Task<IReadOnlyList<EntradaBitacora>> CadenaAsync(Guid empresaId, CancellationToken cancellationToken);

    Task<Pagina<SolicitudDto>> SolicitudesAsync(Guid empresaId, PaginaSolicitada pagina, string? estado, DateOnly hoy, CancellationToken cancellationToken);

    Task<ResumenPrivacidadDto> ResumenAsync(Guid empresaId, DateTimeOffset ahora, DateOnly hoy, CancellationToken cancellationToken);
}

public interface ISolicitudesTitular
{
    Task<SolicitudTitular?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    void Agregar(SolicitudTitular solicitud);
}

/// <summary>Colombia no tiene horario de verano: los plazos se cuentan en UTC−5.</summary>
internal static class HoraColombia
{
    public static readonly TimeSpan Zona = TimeSpan.FromHours(-5);

    public static DateOnly Hoy(DateTimeOffset ahora) => DateOnly.FromDateTime(ahora.ToOffset(Zona).DateTime);
}

// ---------- Bitácora ----------

/// <summary>Los demás módulos dejan sus acciones por su outbox; aquí entran a la cadena de la empresa.</summary>
internal sealed class AccionAuditadaConsumer(IBitacora bitacora, IUnitOfWork unidad) : IIntegrationEventConsumer<AccionAuditada>
{
    public async Task Handle(AccionAuditada e, CancellationToken cancellationToken)
    {
        await bitacora.RegistrarAsync(e.EmpresaId, e.TipoActor, e.Actor, e.Accion, e.Detalle, e.Nota, e.Entidad, e.EntidadId, e.DatoSensible,
            e.Modulo, e.OccurredOn, e.Id, cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}

/// <summary><paramref name="Tipo"/>: Ia, Persona, Sistema o Titular; vacío = todo.</summary>
public sealed record ListarBitacoraQuery(int? Pagina, int? Tamano, string? Tipo, string? Buscar, bool SoloSensibles) : IQuery<Pagina<EntradaBitacoraDto>>;

internal sealed class ListarBitacoraHandler(IContextoUsuario usuario, ILecturaPrivacidad lectura) : IQueryHandler<ListarBitacoraQuery, Pagina<EntradaBitacoraDto>>
{
    public async Task<Result<Pagina<EntradaBitacoraDto>>> Handle(ListarBitacoraQuery q, CancellationToken cancellationToken) =>
        await lectura.BitacoraAsync(usuario.EmpresaId, new PaginaSolicitada(q.Pagina, q.Tamano), q.Tipo, q.Buscar, q.SoloSensibles, cancellationToken);
}

/// <summary>Recorre toda la cadena: cada línea debe apuntar a la anterior y su huella debe corresponder a su contenido.</summary>
public sealed record VerificarBitacoraQuery : IQuery<VerificacionDto>;

internal sealed class VerificarBitacoraHandler(IContextoUsuario usuario, ILecturaPrivacidad lectura) : IQueryHandler<VerificarBitacoraQuery, VerificacionDto>
{
    public async Task<Result<VerificacionDto>> Handle(VerificarBitacoraQuery q, CancellationToken cancellationToken)
    {
        var v = CadenaBitacora.Verificar(await lectura.CadenaAsync(usuario.EmpresaId, cancellationToken));
        return new VerificacionDto(v.Integra, v.Revisadas, v.FechaAlterada, v.Integra
            ? $"Cadena íntegra: se revisaron {v.Revisadas} líneas y ninguna fue alterada."
            : $"La cadena se rompe en la línea {v.Revisadas}: alguien la cambió o borró una anterior. Avise a su oficial de protección de datos.");
    }
}

public sealed record ObtenerResumenPrivacidadQuery : IQuery<ResumenPrivacidadDto>;

internal sealed class ObtenerResumenPrivacidadHandler(IContextoUsuario usuario, ILecturaPrivacidad lectura, TimeProvider reloj)
    : IQueryHandler<ObtenerResumenPrivacidadQuery, ResumenPrivacidadDto>
{
    public async Task<Result<ResumenPrivacidadDto>> Handle(ObtenerResumenPrivacidadQuery q, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        return await lectura.ResumenAsync(usuario.EmpresaId, ahora, HoraColombia.Hoy(ahora), cancellationToken);
    }
}

// ---------- Solicitudes de titulares ----------

public sealed record ListarSolicitudesQuery(int? Pagina, int? Tamano, string? Estado) : IQuery<Pagina<SolicitudDto>>;

internal sealed class ListarSolicitudesHandler(IContextoUsuario usuario, ILecturaPrivacidad lectura, TimeProvider reloj) : IQueryHandler<ListarSolicitudesQuery, Pagina<SolicitudDto>>
{
    public async Task<Result<Pagina<SolicitudDto>>> Handle(ListarSolicitudesQuery q, CancellationToken cancellationToken) =>
        await lectura.SolicitudesAsync(usuario.EmpresaId, new PaginaSolicitada(q.Pagina, q.Tamano), q.Estado, HoraColombia.Hoy(reloj.GetUtcNow()), cancellationToken);
}

/// <summary>Una persona de la empresa registra una solicitud que llegó por correo, teléfono o en persona.</summary>
public sealed record RegistrarSolicitudCommand(string Tipo, string Titular, string Documento, string Contacto, string Canal, string Descripcion) : ICommand<Guid>;

internal sealed class RegistrarSolicitudValidator : AbstractValidator<RegistrarSolicitudCommand>
{
    public RegistrarSolicitudValidator()
    {
        RuleFor(c => c.Tipo).Must(t => Enum.TryParse<TipoSolicitud>(t, out _)).WithMessage("Elija qué pide el titular.");
        RuleFor(c => c.Titular).NotEmpty().WithMessage("Escriba el nombre del titular.").MaximumLength(200);
        RuleFor(c => c.Contacto).MaximumLength(200);
        RuleFor(c => c.Canal).NotEmpty().MaximumLength(60);
        RuleFor(c => c.Descripcion).MaximumLength(2000);
    }
}

internal sealed class RegistrarSolicitudHandler(
    IContextoUsuario usuario, ISolicitudesTitular solicitudes, IDatosTitulares titulares, IDirectorioEmpresas empresas, IBitacora bitacora, IBandeja bandeja,
    IUnitOfWork unidad, TimeProvider reloj) : ICommandHandler<RegistrarSolicitudCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegistrarSolicitudCommand c, CancellationToken cancellationToken)
    {
        var tipo = Enum.Parse<TipoSolicitud>(c.Tipo);
        var empresa = (await empresas.ObtenerAsync(usuario.EmpresaId, cancellationToken))?.Nombre ?? "la empresa";
        var datos = await titulares.ObtenerAsync(usuario.EmpresaId, c.Documento, cancellationToken);
        var registrada = SolicitudTitular.Registrar(usuario.EmpresaId, tipo, c.Titular, c.Documento, c.Contacto, c.Canal, c.Descripcion,
            RedactorRespuestas.Redactar(tipo, c.Titular, empresa, datos), usuario.Nombre, reloj.GetUtcNow(), HoraColombia.Zona);
        if (registrada.IsFailure)
        {
            return registrada.Error;
        }

        var s = registrada.Value;
        solicitudes.Agregar(s);
        await bitacora.RegistrarAsync(s.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Registró una solicitud de {Nombres.Tipo(tipo).ToLowerInvariant()} de {s.Titular}",
            $"Recibida por {s.Canal} · vence el {s.Vence:dd/MM/yyyy} ({SolicitudTitular.PlazoDiasHabiles(tipo)} días hábiles)", null, "SolicitudTitular", s.Id,
            cancellationToken: cancellationToken);
        await bandeja.PublicarAsync(PropuestaDeSolicitud.Crear(s, reloj.GetUtcNow()), cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return s.Id;
    }
}

/// <summary>
/// El titular pide desde el portal. Se crea una solicitud en cada empresa que tenga sus datos. La respuesta al titular es
/// siempre la misma, tenga o no datos: así nadie puede averiguar en qué empresas aparece un documento.
/// </summary>
public sealed record RegistrarSolicitudPortalCommand(string Tipo, string Titular, string Documento, string Contacto, string Descripcion) : ICommand;

internal sealed class RegistrarSolicitudPortalValidator : AbstractValidator<RegistrarSolicitudPortalCommand>
{
    public RegistrarSolicitudPortalValidator()
    {
        RuleFor(c => c.Tipo).Must(t => Enum.TryParse<TipoSolicitud>(t, out _)).WithMessage("Elige qué quieres pedir.");
        RuleFor(c => c.Titular).NotEmpty().WithMessage("Escribe tu nombre completo.").MaximumLength(200);
        RuleFor(c => c.Documento).Must(d => (d ?? "").Count(char.IsDigit) is >= 5 and <= 12).WithMessage("Escribe tu número de cédula sin puntos.");
        RuleFor(c => c.Contacto).NotEmpty().WithMessage("Déjanos un correo o un celular para responderte.").MaximumLength(200);
        RuleFor(c => c.Descripcion).MaximumLength(2000);
    }
}

internal sealed class RegistrarSolicitudPortalHandler(
    ISolicitudesTitular solicitudes, IDatosTitulares titulares, IDirectorioEmpresas empresas, IBitacora bitacora, IBandeja bandeja, IUnitOfWork unidad,
    TimeProvider reloj) : ICommandHandler<RegistrarSolicitudPortalCommand>
{
    public async Task<Result> Handle(RegistrarSolicitudPortalCommand c, CancellationToken cancellationToken)
    {
        var tipo = Enum.Parse<TipoSolicitud>(c.Tipo);
        var ahora = reloj.GetUtcNow();
        foreach (var empresaId in await titulares.EmpresasConDatosAsync(c.Documento, cancellationToken))
        {
            var empresa = (await empresas.ObtenerAsync(empresaId, cancellationToken))?.Nombre ?? "la empresa";
            var datos = await titulares.ObtenerAsync(empresaId, c.Documento, cancellationToken);
            var registrada = SolicitudTitular.Registrar(empresaId, tipo, c.Titular, c.Documento, c.Contacto, "Portal de empleo", c.Descripcion,
                RedactorRespuestas.Redactar(tipo, c.Titular, empresa, datos), "El titular", ahora, HoraColombia.Zona);
            if (registrada.IsFailure)
            {
                return registrada;
            }

            solicitudes.Agregar(registrada.Value);
            await bitacora.RegistrarAsync(empresaId, TipoActor.Titular, c.Titular.Trim(), $"Pidió {Nombres.Tipo(tipo).ToLowerInvariant()} de sus datos desde el portal",
                $"Vence el {registrada.Value.Vence:dd/MM/yyyy} · {SolicitudTitular.PlazoDiasHabiles(tipo)} días hábiles", "Verifique la identidad antes de responder",
                "SolicitudTitular", registrada.Value.Id, cancellationToken: cancellationToken);
            await bandeja.PublicarAsync(PropuestaDeSolicitud.Crear(registrada.Value, ahora), cancellationToken);
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Responder al titular. En supresión o revocatoria, aprobar ordena borrar sus datos en todos los módulos.</summary>
public sealed record ResponderSolicitudCommand(Guid SolicitudId, string Respuesta, bool AprobarSupresion) : ICommand;

internal sealed class ResponderSolicitudValidator : AbstractValidator<ResponderSolicitudCommand>
{
    public ResponderSolicitudValidator() =>
        RuleFor(c => c.Respuesta).NotEmpty().WithMessage("Escriba la respuesta para el titular.").MaximumLength(4000);
}

internal sealed class ResponderSolicitudHandler(
    IContextoUsuario usuario, ISolicitudesTitular solicitudes, IEventosSalientes eventos, IBitacora bitacora, IBandeja bandeja, IUnitOfWork unidad,
    TimeProvider reloj) : ICommandHandler<ResponderSolicitudCommand>
{
    public async Task<Result> Handle(ResponderSolicitudCommand c, CancellationToken cancellationToken)
    {
        var s = await solicitudes.ObtenerAsync(usuario.EmpresaId, c.SolicitudId, cancellationToken);
        if (s is null)
        {
            return Result.Failure(SolicitudErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var respondida = s.Responder(c.Respuesta, c.AprobarSupresion, usuario.Nombre, ahora);
        if (respondida.IsFailure)
        {
            return respondida;
        }

        var restantes = CalendarioColombia.DiasHabilesRestantes(HoraColombia.Hoy(ahora), s.Vence);
        await bitacora.RegistrarAsync(s.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Respondió la solicitud de {Nombres.Tipo(s.Tipo).ToLowerInvariant()} de {s.Titular}",
            restantes >= 0 ? $"Dentro del plazo legal (quedaban {restantes} días hábiles)" : $"Fuera del plazo legal ({-restantes} días hábiles tarde)",
            s.SupresionAprobada ? "Aprobó suprimir sus datos en todos los módulos" : s.EsSupresion ? "No aprobó la supresión" : null,
            "SolicitudTitular", s.Id, cancellationToken: cancellationToken);
        await bandeja.ResolverAsync(s.EmpresaId, "SolicitudTitular", s.Id, s.SupresionAprobada ? "Respondida y datos suprimidos" : "Respondida", usuario.Nombre,
            cancellationToken);
        if (s.SupresionAprobada)
        {
            eventos.Publicar(new SupresionDeTitularAprobada(Guid.CreateVersion7(), ahora, s.EmpresaId, s.Id, s.Documento, usuario.Nombre));
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>La solicitud de un titular como decisión en la bandeja: lleva el plazo legal y afecta a una persona.</summary>
internal static class PropuestaDeSolicitud
{
    public static PropuestaPublicada Crear(SolicitudTitular s, DateTimeOffset ahora) => new(
        Guid.CreateVersion7(), ahora, s.EmpresaId, null, "Core", "SolicitudTitular", s.Id,
        $"{Nombres.Tipo(s.Tipo)} de datos · {s.Titular}", $"Recibida por {s.Canal} · plazo legal de {SolicitudTitular.PlazoDiasHabiles(s.Tipo)} días hábiles",
        null, null,
        [new RazonPropuesta("+", "La respuesta ya está preparada con los datos que la empresa tiene del titular.", "Privacidad")],
        [new CampoPropuesta("Pide", string.IsNullOrWhiteSpace(s.Descripcion) ? Nombres.Tipo(s.Tipo) : s.Descripcion),
         new CampoPropuesta("Vence", s.Vence.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture))],
        null, "/consola/empresa/privacidad", true, s.Vence);
}

internal static class Nombres
{
    public static string Tipo(TipoSolicitud tipo) => tipo switch
    {
        TipoSolicitud.Consulta => "Consulta",
        TipoSolicitud.Actualizacion => "Actualización",
        TipoSolicitud.Supresion => "Supresión",
        _ => "Revocatoria",
    };
}

/// <summary>
/// Prepara la respuesta con los datos que la empresa tiene de verdad del titular. La persona la revisa y la ajusta
/// antes de responder: nada se envía solo.
/// </summary>
internal static class RedactorRespuestas
{
    public static string Redactar(TipoSolicitud tipo, string titular, string empresa, DatosTitularReclutamiento? d)
    {
        var nombre = (titular ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var saludo = $"Hola, {nombre}.";
        if (d is null)
        {
            return $"{saludo} Revisamos nuestros registros y {empresa} no tiene datos personales tuyos en sus procesos de selección. " +
                "Si crees que hay un error, respóndenos con más información (por ejemplo, a qué vacante te postulaste).";
        }

        var tiene = new List<string> { "nombre", "cédula" };
        if (!string.IsNullOrWhiteSpace(d.Celular)) tiene.Add("celular");
        if (!string.IsNullOrWhiteSpace(d.Correo)) tiene.Add("correo");
        if (!string.IsNullOrWhiteSpace(d.Ciudad)) tiene.Add("ciudad");
        if (d.TieneHojaDeVida) tiene.Add("hoja de vida");
        var postulaciones = d.Postulaciones == 1 ? "1 postulación" : $"{d.Postulaciones} postulaciones";
        var vacantes = d.Vacantes.Count > 0 ? $" ({string.Join(", ", d.Vacantes.Take(5))})" : "";
        var autorizacion = d.VersionAutorizacion is null ? "" : $" Aceptaste la autorización de tratamiento de datos {d.VersionAutorizacion}{(d.UltimaPostulacion is { } f ? $" el {f.ToOffset(HoraColombia.Zona):dd/MM/yyyy}" : "")}.";

        return tipo switch
        {
            TipoSolicitud.Consulta =>
                $"{saludo} {empresa} tiene estos datos tuyos: {string.Join(", ", tiene)}, y {postulaciones}{vacantes} con tus respuestas de preselección.{autorizacion} " +
                "Los usamos solo para procesos de selección. Puedes pedir que los actualicemos o los eliminemos cuando quieras.",
            TipoSolicitud.Actualizacion =>
                $"{saludo} Actualizamos tus datos como nos pediste. Ahora tenemos: {string.Join(", ", tiene)}. Si algo más no está bien, respóndenos y lo corregimos.",
            _ =>
                $"{saludo} Eliminamos tus datos de los procesos de selección de {empresa}: {string.Join(", ", tiene)} y {postulaciones}{vacantes}. " +
                "Conservamos solo el registro de que revocaste la autorización, como exige la Ley 1581 de 2012. No volveremos a contactarte para vacantes.",
        };
    }
}
