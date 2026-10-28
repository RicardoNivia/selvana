using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Preseleccion;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Contracts;

namespace Selvana.Reclutamiento.Application.Portal;

/// <summary>
/// Una postulación tal como la ve la persona: el estado en palabras claras, sin puntajes internos, razones del ranking
/// ni motivos de descarte (ADR 0013).
/// </summary>
public sealed record MiPostulacionDto(
    Guid PostulacionId, string Numero, string VacanteCodigo, string Vacante, string Empresa, string Ciudad, DateTimeOffset RecibidaEn,
    string Estado, string Explicacion);

/// <summary>Algo que la persona debe hacer. <paramref name="Tipo"/>: Chat, Pruebas u Oferta (aceptar y firmar o subir documentos).</summary>
public sealed record PendienteDto(string Tipo, Guid Id, string Titulo, string Vacante, string Empresa, DateTimeOffset? VenceEn);

public sealed record MiPortalDto(IReadOnlyList<PendienteDto> Pendientes, IReadOnlyList<MiPostulacionDto> Postulaciones);

/// <summary>Una postulación de la persona con lo necesario para armar su estado y sus pendientes.</summary>
public sealed record PostulacionDeLaPersona(
    Guid PostulacionId, Guid EmpresaId, string Numero, string VacanteCodigo, string Vacante, string Empresa, string Ciudad, bool VacanteCerrada,
    DateTimeOffset RecibidaEn, string Etapa, bool Revisada, Guid? ConversacionId, bool ChatPorResponder, Guid? AplicacionPruebasId, DateTimeOffset? PruebasVencen);

/// <summary>Lado de lectura del portal de la persona: busca sus postulaciones en todas las empresas por cédula y correo a la vez.</summary>
public interface ILecturaPortalPersona
{
    Task<IReadOnlyList<PostulacionDeLaPersona>> PostulacionesAsync(string documento, string correo, CancellationToken cancellationToken);
}

public sealed record ObtenerMiPortalQuery : IQuery<MiPortalDto>;

/// <summary>Abre un pendiente desde el portal: devuelve la dirección de la pantalla donde se responde.</summary>
public sealed record AbrirPendienteCommand(string Tipo, Guid Id) : ICommand<string>;

public static class PortalErrors
{
    public static readonly Error PendienteNoExiste = Error.NotFound("Portal.PendienteNoExiste", "Esto ya no está pendiente. Actualiza la página.");
}

internal static class EstadoParaLaPersona
{
    /// <summary>Estado y explicación en palabras claras; nunca el puntaje ni el motivo de descarte.</summary>
    public static (string Estado, string Explicacion) De(PostulacionDeLaPersona p, ProcesoDeLaPersona? contratacion) => contratacion switch
    {
        { Vinculado: true } => ("Contratada", "¡Felicitaciones! Ya haces parte del equipo."),
        { Cancelado: true } => ("Proceso cerrado", "La empresa cerró este proceso de contratación."),
        not null => ("En contratación", contratacion.Pendiente ?? "La empresa está preparando tu ingreso."),
        _ => p.Etapa switch
        {
            "Descartada" => ("No seleccionada", "Gracias por postularte. Tu hoja de vida queda en tu perfil para otras vacantes."),
            "Terna" => ("Finalista", "Estás entre las personas finalistas. La empresa te contactará."),
            _ when p.VacanteCerrada => ("Vacante cerrada", "La empresa cerró esta vacante."),
            "Pruebas" => ("Pruebas", "Estás en la etapa de pruebas psicotécnicas."),
            "Preseleccion" => ("En preselección", "La empresa quiere conocerte mejor."),
            _ when p.Revisada => ("En revisión", "La empresa está revisando tu hoja de vida."),
            _ => ("Recibida", "Recibimos tu postulación."),
        },
    };
}

internal sealed class ObtenerMiPortalHandler(IPersonaActual persona, ICuentasCandidato cuentas, ILecturaPortalPersona lectura, IProcesosDeLaPersona procesos,
    TimeProvider reloj) : IQueryHandler<ObtenerMiPortalQuery, MiPortalDto>
{
    public async Task<Result<MiPortalDto>> Handle(ObtenerMiPortalQuery query, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return CuentaErrors.NoExiste;
        }

        var ahora = reloj.GetUtcNow();
        var postulaciones = await lectura.PostulacionesAsync(cuenta.Documento, cuenta.Correo, cancellationToken);
        var contrataciones = await procesos.ListarAsync(cuenta.Documento, cuenta.Correo, cancellationToken);
        var porPostulacion = contrataciones.GroupBy(c => c.PostulacionId).ToDictionary(g => g.Key, g => g.First());

        var pendientes = new List<PendienteDto>();
        foreach (var c in contrataciones.Where(c => c.Pendiente is not null))
        {
            pendientes.Add(new PendienteDto("Oferta", c.Id, c.Pendiente!, c.Cargo, c.Empresa, null));
        }

        foreach (var p in postulaciones)
        {
            if (p.ChatPorResponder && p.ConversacionId is not null)
            {
                pendientes.Add(new PendienteDto("Chat", p.PostulacionId, "Responder las preguntas de la preselección", p.Vacante, p.Empresa, null));
            }

            if (p.AplicacionPruebasId is not null && p.PruebasVencen is { } vence && vence >= ahora)
            {
                pendientes.Add(new PendienteDto("Pruebas", p.PostulacionId, "Presentar las pruebas psicotécnicas", p.Vacante, p.Empresa, vence));
            }
        }

        var lista = postulaciones.OrderByDescending(p => p.RecibidaEn).Select(p =>
        {
            var (estado, explicacion) = EstadoParaLaPersona.De(p, porPostulacion.GetValueOrDefault(p.PostulacionId));
            return new MiPostulacionDto(p.PostulacionId, p.Numero, p.VacanteCodigo, p.Vacante, p.Empresa, p.Ciudad, p.RecibidaEn, estado, explicacion);
        }).ToList();

        return new MiPortalDto(pendientes, lista);
    }
}

/// <summary>
/// Abre un pendiente desde el portal: comprueba que sea de la persona (cédula y correo), renueva el enlace y devuelve
/// la pantalla donde se responde (la misma del enlace que recibió). El enlace anterior deja de servir.
/// </summary>
internal sealed class AbrirPendienteHandler(
    IPersonaActual persona, ICuentasCandidato cuentas, ILecturaPortalPersona lectura, IConversaciones conversaciones, IAplicacionesPruebas aplicaciones,
    ITokensPruebas tokens, IProcesosDeLaPersona procesos, IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<AbrirPendienteCommand, string>
{
    public async Task<Result<string>> Handle(AbrirPendienteCommand c, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return CuentaErrors.NoExiste;
        }

        if (c.Tipo == "Oferta")
        {
            var tokenOferta = await procesos.AbrirAsync(c.Id, cuenta.Documento, cuenta.Correo, cancellationToken);
            return tokenOferta is null ? PortalErrors.PendienteNoExiste : $"/vinculacion/{tokenOferta}";
        }

        var postulacion = (await lectura.PostulacionesAsync(cuenta.Documento, cuenta.Correo, cancellationToken)).FirstOrDefault(p => p.PostulacionId == c.Id);
        if (postulacion is null)
        {
            return PortalErrors.PendienteNoExiste;
        }

        var ahora = reloj.GetUtcNow();
        var (token, huella) = tokens.Generar();
        if (c.Tipo == "Chat")
        {
            var conversacion = await conversaciones.PorPostulacionAsync(postulacion.EmpresaId, postulacion.PostulacionId, cancellationToken);
            if (conversacion is null || !conversacion.Activa)
            {
                return PortalErrors.PendienteNoExiste;
            }

            conversacion.RenovarEnlace(huella, ahora);
            await unidad.SaveChangesAsync(cancellationToken);
            return $"/preseleccion/{token}";
        }

        if (c.Tipo == "Pruebas")
        {
            var aplicacion = await aplicaciones.ObtenerPorPostulacionAsync(postulacion.EmpresaId, postulacion.PostulacionId, cancellationToken);
            if (aplicacion is null || !aplicacion.AbrirDesdePortal(huella, ahora))
            {
                return PortalErrors.PendienteNoExiste;
            }

            await unidad.SaveChangesAsync(cancellationToken);
            return $"/pruebas/{token}";
        }

        return PortalErrors.PendienteNoExiste;
    }
}
