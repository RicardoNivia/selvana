using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Preseleccion;

public enum EstadoConversacion
{
    /// <summary>Se envió el enlace; el candidato aún no escribe.</summary>
    Invitada,
    Abierta,

    /// <summary>El agente recogió todo lo que necesitaba.</summary>
    Completada,

    /// <summary>El candidato pidió hablar con una persona o la IA no estuvo segura: responde una persona.</summary>
    Escalada,
}

public enum AutorMensaje
{
    Agente,
    Candidato,
    Persona,
}

public sealed class MensajeConversacion
{
    public AutorMensaje Autor { get; init; }
    public string Texto { get; init; } = "";

    /// <summary>Quién escribió, si es una persona de la empresa.</summary>
    public string? Por { get; init; }
    public DateTimeOffset En { get; init; }
}

public sealed class RespuestaTecnica
{
    public string Pregunta { get; init; } = "";
    public string Respuesta { get; init; } = "";
}

/// <summary>Lo que el agente entendió de la conversación. Nunca incluye edad, estado civil, hijos ni otras variables protegidas.</summary>
public sealed class ResumenPreseleccion
{
    public bool? Interesado { get; set; }
    public decimal? AspiracionSalarialCop { get; set; }
    public string Disponibilidad { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public List<RespuestaTecnica> Respuestas { get; set; } = [];
    public List<string> Dudas { get; set; } = [];
    public string Texto { get; set; } = "";
}

public static class ConversacionErrors
{
    public static readonly Error NoExiste = Error.NotFound("Preseleccion.NoExiste", "Este enlace no existe o ya no está activo. Pídele uno nuevo a la empresa.");
    public static readonly Error MensajeVacio = Error.Validation("Preseleccion.MensajeVacio", "Escribe tu mensaje.");
    public static readonly Error Pensando = Error.Conflict("Preseleccion.Pensando", "Dame un momento: estoy respondiendo tu mensaje anterior.");
    public static readonly Error Cerrada = Error.Conflict("Preseleccion.Cerrada", "Esta conversación ya terminó. Si tienes otra duda, escríbele a la empresa.");
}

/// <summary>
/// Conversación de preselección de una postulación: el agente confirma interés, aspiración, disponibilidad y ubicación,
/// hace las preguntas técnicas y responde dudas. Si el candidato lo pide o la IA no está segura, responde una persona.
/// </summary>
public sealed class ConversacionPreseleccion : AggregateRoot<Guid>
{
    public const int MaximoMensajes = 60;

    private ConversacionPreseleccion(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public Guid? EmpresaUsuariaId { get; private init; }
    public Guid VacanteId { get; private init; }
    public Guid PostulacionId { get; private init; }
    public Guid CandidatoId { get; private init; }

    /// <summary>SHA-256 del enlace: el enlace en claro solo lo tiene el candidato.</summary>
    public string TokenHash { get; private set; } = "";
    public EstadoConversacion Estado { get; private set; }

    /// <summary>El candidato escribió y el agente está respondiendo.</summary>
    public bool Pensando { get; private set; }
    public List<MensajeConversacion> Mensajes { get; private set; } = [];
    public ResumenPreseleccion Resumen { get; private set; } = new();
    public string? MotivoEscalamiento { get; private set; }
    public string? Modelo { get; private set; }

    /// <summary>Lo que costó la IA en la conversación (todos los turnos), en dólares.</summary>
    public decimal CostoIaUsd { get; private set; }

    public void SumarCostoIa(decimal costoUsd) => CostoIaUsd += Math.Max(0, costoUsd);
    public string InvitadaPor { get; private init; } = "";
    public DateTimeOffset CreadaEn { get; private init; }
    public DateTimeOffset ActualizadaEn { get; private set; }

    public bool Activa => Estado is EstadoConversacion.Invitada or EstadoConversacion.Abierta or EstadoConversacion.Escalada;

    public static ConversacionPreseleccion Invitar(Guid empresaId, Guid? empresaUsuariaId, Guid vacanteId, Guid postulacionId, Guid candidatoId,
        string tokenHash, string saludo, string por, DateTimeOffset ahora) => new(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            EmpresaUsuariaId = empresaUsuariaId,
            VacanteId = vacanteId,
            PostulacionId = postulacionId,
            CandidatoId = candidatoId,
            TokenHash = tokenHash,
            Estado = EstadoConversacion.Invitada,
            Mensajes = [new MensajeConversacion { Autor = AutorMensaje.Agente, Texto = saludo, En = ahora }],
            InvitadaPor = por,
            CreadaEn = ahora,
            ActualizadaEn = ahora,
        };

    /// <summary>Un enlace nuevo invalida el anterior (por ejemplo, si el candidato lo perdió).</summary>
    public void RenovarEnlace(string tokenHash, DateTimeOffset ahora)
    {
        TokenHash = tokenHash;
        ActualizadaEn = ahora;
    }

    public Result EscribeCandidato(string texto, DateTimeOffset ahora)
    {
        if (!Activa)
        {
            return Result.Failure(ConversacionErrors.Cerrada);
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return Result.Failure(ConversacionErrors.MensajeVacio);
        }

        if (Pensando)
        {
            return Result.Failure(ConversacionErrors.Pensando);
        }

        if (Mensajes.Count >= MaximoMensajes)
        {
            return Result.Failure(ConversacionErrors.Cerrada);
        }

        Mensajes.Add(new MensajeConversacion { Autor = AutorMensaje.Candidato, Texto = Recortar(texto, 1000), En = ahora });
        if (Estado == EstadoConversacion.Invitada)
        {
            Estado = EstadoConversacion.Abierta;
        }

        // Escalada: responde una persona, el agente no interviene
        Pensando = Estado != EstadoConversacion.Escalada;
        ActualizadaEn = ahora;
        return Result.Success();
    }

    /// <summary>El agente respondió: su mensaje y lo que entendió. Si no está seguro o el candidato pide una persona, se escala.</summary>
    public void RespondeAgente(string texto, ResumenPreseleccion resumen, bool completa, string? escalarPorque, string modelo, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(resumen);
        Pensando = false;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            Mensajes.Add(new MensajeConversacion { Autor = AutorMensaje.Agente, Texto = Recortar(texto, 1500), En = ahora });
        }

        Resumen = resumen;
        Modelo = modelo;
        ActualizadaEn = ahora;
        if (!string.IsNullOrWhiteSpace(escalarPorque))
        {
            Escalar(escalarPorque, ahora);
        }
        else if (completa && Estado == EstadoConversacion.Abierta)
        {
            Estado = EstadoConversacion.Completada;
        }
    }

    /// <summary>El agente no pudo responder: la conversación pasa a una persona para no dejar al candidato esperando.</summary>
    public void FallaAgente(string motivo, DateTimeOffset ahora)
    {
        Pensando = false;
        Mensajes.Add(new MensajeConversacion { Autor = AutorMensaje.Agente, Texto = "Ahora no puedo responderte. Le pasé tu mensaje a una persona de la empresa: te responde aquí mismo.", En = ahora });
        Escalar(motivo, ahora);
    }

    public void Escalar(string motivo, DateTimeOffset ahora)
    {
        Estado = EstadoConversacion.Escalada;
        MotivoEscalamiento = Recortar(motivo.Trim(), 300);
        ActualizadaEn = ahora;
    }

    /// <summary>Una persona de la empresa responde en la conversación (sobre todo cuando está escalada).</summary>
    public Result EscribePersona(string texto, string por, bool cerrar, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return Result.Failure(ConversacionErrors.MensajeVacio);
        }

        Mensajes.Add(new MensajeConversacion { Autor = AutorMensaje.Persona, Texto = Recortar(texto, 1500), Por = por, En = ahora });
        Pensando = false;
        if (cerrar)
        {
            Estado = EstadoConversacion.Completada;
        }

        ActualizadaEn = ahora;
        return Result.Success();
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto.Trim() : texto[..largo].Trim();
}
