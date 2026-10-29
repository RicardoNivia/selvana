using FluentValidation;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Preseleccion;

public interface IConversaciones
{
    Task<ConversacionPreseleccion?> PorTokenAsync(string tokenHash, CancellationToken cancellationToken);

    Task<ConversacionPreseleccion?> PorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ConversacionPreseleccion?> PorPostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken);

    /// <summary>Las conversaciones de la vacante, sin tracking (para leer el proceso).</summary>
    Task<IReadOnlyList<ConversacionPreseleccion>> DeVacanteAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken);

    void Agregar(ConversacionPreseleccion conversacion);
}

public sealed record MensajeDto(string Autor, string Texto, string? Por, DateTimeOffset En);

/// <summary>Lo que ve el candidato: la vacante, los mensajes y si el agente está respondiendo. Sin el resumen interno.</summary>
public sealed record ConversacionPublicaDto(string Vacante, string Empresa, string Ciudad, string Estado, bool Pensando, IReadOnlyList<MensajeDto> Mensajes);

public sealed record ResumenDto(bool? Interesado, decimal? AspiracionSalarialCop, string Disponibilidad, string Ubicacion,
    IReadOnlyList<RespuestaTecnicaDto> Respuestas, IReadOnlyList<string> Dudas, string Texto);

public sealed record RespuestaTecnicaDto(string Pregunta, string Respuesta);

/// <summary>Lo que ve el reclutador: la conversación completa, el resumen de la IA y si espera a una persona.</summary>
public sealed record ConversacionDto(Guid Id, string Estado, bool Pensando, IReadOnlyList<MensajeDto> Mensajes, ResumenDto Resumen, string? MotivoEscalamiento,
    string? Modelo, string InvitadaPor, DateTimeOffset CreadaEn, DateTimeOffset ActualizadaEn);

public sealed record EnlacePreseleccionDto(Guid ConversacionId, string Token);

/// <summary>El reclutador invita al candidato a conversar con el agente; devuelve el enlace (se comparte por WhatsApp o correo).</summary>
public sealed record InvitarPreseleccionCommand(Guid PostulacionId) : ICommand<EnlacePreseleccionDto>;

public sealed record ObtenerConversacionPublicaQuery(string Token) : IQuery<ConversacionPublicaDto>;

public sealed record EscribirCandidatoCommand(string Token, string Texto) : ICommand;

public sealed record ObtenerConversacionQuery(Guid PostulacionId) : IQuery<ConversacionDto>;

/// <summary>Una persona de la empresa responde en la conversación; «cerrar» la da por terminada.</summary>
public sealed record ResponderPreseleccionCommand(Guid ConversacionId, string Texto, bool Cerrar) : ICommand;

internal static class Preseleccion
{
    public const string TipoBandeja = "Preseleccion";

    public static MensajeDto Dto(MensajeConversacion m) => new(m.Autor.ToString(), m.Texto, m.Por, m.En);

    public static string PrimerNombre(string nombre)
    {
        var p = (nombre ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return p.Length == 0 ? "" : char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant();
    }

    public static MensajePreseleccionRecibido Evento(ConversacionPreseleccion c, Vacante v, string nombre, DateTimeOffset ahora) =>
        new(Guid.CreateVersion7(), ahora, c.EmpresaId, c.Id, nombre,
            new VacantePreseleccion(v.Titulo, v.EmpresaVisible, v.Ciudad, v.Jornada, v.Cupos, v.SalarioMinimo, v.SalarioMaximo, v.Descripcion, v.Requisitos, v.PreguntasFiltro),
            [.. c.Mensajes.Select(m => new MensajePrevio(m.Autor.ToString(), m.Texto))]);
}

internal sealed class EscribirCandidatoValidator : AbstractValidator<EscribirCandidatoCommand>
{
    public EscribirCandidatoValidator() => RuleFor(c => c.Texto).NotEmpty().WithMessage("Escribe tu mensaje.").MaximumLength(1000).WithMessage("Tu mensaje es muy largo: resúmelo un poco.");
}

internal sealed class ResponderPreseleccionValidator : AbstractValidator<ResponderPreseleccionCommand>
{
    public ResponderPreseleccionValidator() => RuleFor(c => c.Texto).NotEmpty().WithMessage("Escriba la respuesta.").MaximumLength(1500);
}

internal sealed class InvitarPreseleccionHandler(
    IUsuarioActual usuario, ICandidatos candidatos, IVacantes vacantes, IConversaciones conversaciones, ITokensPruebas tokens, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<InvitarPreseleccionCommand, EnlacePreseleccionDto>
{
    public async Task<Result<EnlacePreseleccionDto>> Handle(InvitarPreseleccionCommand c, CancellationToken cancellationToken)
    {
        var candidato = await candidatos.ObtenerPorPostulacionAsync(usuario.EmpresaId, c.PostulacionId, cancellationToken);
        var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == c.PostulacionId);
        var vacante = postulacion is null ? null : await vacantes.ObtenerAsync(postulacion.VacanteId, cancellationToken);
        if (candidato is null || candidato.Suprimido || vacante is null || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return AfinidadErrors.NoExiste;
        }

        var ahora = reloj.GetUtcNow();
        var (token, huella) = tokens.Generar();
        var conversacion = await conversaciones.PorPostulacionAsync(usuario.EmpresaId, postulacion!.Id, cancellationToken);
        if (conversacion is null)
        {
            var saludo = $"Hola{(Preseleccion.PrimerNombre(candidato.Datos.Nombre) is { Length: > 0 } n ? $", {n}" : "")}. Soy el asistente de selección de {vacante.EmpresaVisible}. " +
                $"Vi tu postulación a «{vacante.Titulo}» en {vacante.Ciudad}. ¿Te puedo hacer unas preguntas rápidas? Son unos 3 minutos.";
            conversacion = ConversacionPreseleccion.Invitar(vacante.EmpresaId, vacante.EmpresaUsuariaId, vacante.Id, postulacion.Id, candidato.Id, huella, saludo, usuario.Nombre, ahora);
            conversaciones.Agregar(conversacion);
        }
        else
        {
            conversacion.RenovarEnlace(huella, ahora);
        }

        if (postulacion.Etapa == EtapaPostulacion.Recibida)
        {
            candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Preseleccion, "Invitada al chat de preselección", usuario.Nombre, ahora);
        }

        bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Invitó a la preselección la postulación {postulacion.Numero}", vacante.Codigo,
            "Conversación con el agente por enlace de un solo uso", "Postulacion", postulacion.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return new EnlacePreseleccionDto(conversacion.Id, token);
    }
}

internal sealed class ObtenerConversacionPublicaHandler(IConversaciones conversaciones, IVacantes vacantes, ITokensPruebas tokens)
    : IQueryHandler<ObtenerConversacionPublicaQuery, ConversacionPublicaDto>
{
    public async Task<Result<ConversacionPublicaDto>> Handle(ObtenerConversacionPublicaQuery q, CancellationToken ct)
    {
        var c = await conversaciones.PorTokenAsync(tokens.Huella(q.Token), ct);
        var v = c is null ? null : await vacantes.ObtenerAsync(c.VacanteId, ct);
        return c is null || v is null
            ? ConversacionErrors.NoExiste
            : new ConversacionPublicaDto(v.Titulo, v.EmpresaVisible, v.Ciudad, c.Estado.ToString(), c.Pensando, [.. c.Mensajes.Select(Preseleccion.Dto)]);
    }
}

internal sealed class EscribirCandidatoHandler(
    IConversaciones conversaciones, IVacantes vacantes, ICandidatos candidatos, ITokensPruebas tokens, IEventosReclutamiento eventos, IUnidadReclutamiento unidad,
    TimeProvider reloj) : ICommandHandler<EscribirCandidatoCommand>
{
    public async Task<Result> Handle(EscribirCandidatoCommand c, CancellationToken cancellationToken)
    {
        var conversacion = await conversaciones.PorTokenAsync(tokens.Huella(c.Token), cancellationToken);
        var vacante = conversacion is null ? null : await vacantes.ObtenerAsync(conversacion.VacanteId, cancellationToken);
        if (conversacion is null || vacante is null)
        {
            return Result.Failure(ConversacionErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var r = conversacion.EscribeCandidato(c.Texto, ahora);
        if (r.IsFailure)
        {
            return r;
        }

        if (conversacion.Pensando)
        {
            var candidato = await candidatos.ObtenerPorPostulacionAsync(conversacion.EmpresaId, conversacion.PostulacionId, cancellationToken);
            eventos.Publicar(Preseleccion.Evento(conversacion, vacante, candidato?.Datos.Nombre ?? "", ahora));
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>El agente respondió: su mensaje, el resumen y, si hace falta, la conversación pasa a una persona (queda en la bandeja).</summary>
internal sealed class RespuestaPreseleccionConsumer(IConversaciones conversaciones, IVacantes vacantes, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : IIntegrationEventConsumer<RespuestaPreseleccion>
{
    public async Task Handle(RespuestaPreseleccion e, CancellationToken cancellationToken)
    {
        var c = await conversaciones.PorIdAsync(e.ConversacionId, cancellationToken);
        var v = c is null ? null : await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (c is null || v is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        var estabaEscalada = c.Estado == EstadoConversacion.Escalada;
        if (e.Error is not null)
        {
            c.FallaAgente($"La IA no pudo responder: {e.Error}", ahora);
        }
        else
        {
            var r = e.Resumen;
            c.RespondeAgente(e.Mensaje, new ResumenPreseleccion
            {
                Interesado = r?.Interesado,
                AspiracionSalarialCop = r?.AspiracionSalarialCop,
                Disponibilidad = r?.Disponibilidad ?? "",
                Ubicacion = r?.Ubicacion ?? "",
                Respuestas = [.. (r?.Respuestas ?? []).Select(x => new RespuestaTecnica { Pregunta = x.Pregunta, Respuesta = x.Respuesta })],
                Dudas = [.. r?.Dudas ?? []],
                Texto = r?.Texto ?? "",
            }, e.Completa, e.EscalarPorque, e.Modelo, ahora);
        }

        if (c.Estado == EstadoConversacion.Escalada && !estabaEscalada)
        {
            eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, c.EmpresaId, c.EmpresaUsuariaId, "Reclutamiento", Preseleccion.TipoBandeja, c.Id,
                $"Un candidato necesita una persona · {v.Titulo}", c.MotivoEscalamiento ?? "La conversación de preselección pasó a una persona.", "Agente de preselección", e.Modelo,
                [], [new CampoPropuesta("Último mensaje del candidato", c.Mensajes.LastOrDefault(m => m.Autor == AutorMensaje.Candidato)?.Texto ?? "")],
                null, $"/consola/vacantes/{v.Id}", false, null));
            bitacora.Registrar(c.EmpresaId, TipoActor.Ia, "Agente de preselección", "Pasó una conversación de preselección a una persona", v.Codigo,
                c.MotivoEscalamiento, "Postulacion", c.PostulacionId);
        }
        else if (c.Estado == EstadoConversacion.Completada)
        {
            bitacora.Registrar(c.EmpresaId, TipoActor.Ia, "Agente de preselección", "Completó la conversación de preselección", $"{v.Codigo} · {c.Resumen.Texto}",
                "No usó edad, género, estado civil, hijos ni dirección. Una persona decide", "Postulacion", c.PostulacionId);
        }

        await unidad.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class ObtenerConversacionHandler(IUsuarioActual usuario, IConversaciones conversaciones, IVacantes vacantes) : IQueryHandler<ObtenerConversacionQuery, ConversacionDto>
{
    public async Task<Result<ConversacionDto>> Handle(ObtenerConversacionQuery q, CancellationToken ct)
    {
        var c = await conversaciones.PorPostulacionAsync(usuario.EmpresaId, q.PostulacionId, ct);
        var v = c is null ? null : await vacantes.ObtenerAsync(c.VacanteId, ct);
        if (c is null || v is null || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return ConversacionErrors.NoExiste;
        }

        var r = c.Resumen;
        return new ConversacionDto(c.Id, c.Estado.ToString(), c.Pensando, [.. c.Mensajes.Select(Preseleccion.Dto)],
            new ResumenDto(r.Interesado, r.AspiracionSalarialCop, r.Disponibilidad, r.Ubicacion, [.. r.Respuestas.Select(x => new RespuestaTecnicaDto(x.Pregunta, x.Respuesta))], r.Dudas, r.Texto),
            c.MotivoEscalamiento, c.Modelo, c.InvitadaPor, c.CreadaEn, c.ActualizadaEn);
    }
}

internal sealed class ResponderPreseleccionHandler(IUsuarioActual usuario, IConversaciones conversaciones, IVacantes vacantes, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<ResponderPreseleccionCommand>
{
    public async Task<Result> Handle(ResponderPreseleccionCommand c, CancellationToken cancellationToken)
    {
        var conversacion = await conversaciones.PorIdAsync(c.ConversacionId, cancellationToken);
        var vacante = conversacion is null ? null : await vacantes.ObtenerAsync(conversacion.VacanteId, cancellationToken);
        if (conversacion is null || vacante is null || conversacion.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(ConversacionErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var r = conversacion.EscribePersona(c.Texto, usuario.Nombre, c.Cerrar, ahora);
        if (r.IsFailure)
        {
            return r;
        }

        bitacora.Registrar(conversacion.EmpresaId, TipoActor.Persona, usuario.Nombre, "Respondió en una conversación de preselección", vacante.Codigo,
            c.Cerrar ? "Dio la conversación por terminada" : null, "Postulacion", conversacion.PostulacionId);
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, conversacion.EmpresaId, Preseleccion.TipoBandeja, conversacion.Id, "Respondió una persona", usuario.Nombre));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
