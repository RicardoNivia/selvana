namespace Selvana.Infrastructure.Common.Messaging;

/// <summary>
/// Mensajería entre la API y el AI.Worker por HTTP (ADR 0008). Cada evento del outbox se envía a los
/// destinos cuyo prefijo coincide con el nombre completo del tipo del evento.
/// </summary>
public sealed class MensajeriaOptions
{
    public const string Section = "Mensajeria";
    public const string EncabezadoClave = "X-Selvana-Clave";

    public List<DestinoEventos> Destinos { get; init; } = [];

    /// <summary>Clave compartida entre la API y el worker; se envía en <see cref="EncabezadoClave"/>.</summary>
    public string ClaveInterna { get; init; } = "";
}

public sealed class DestinoEventos
{
    public string Url { get; init; } = "";

    /// <summary>Prefijos de tipo que recibe este destino, por ejemplo "Selvana.Core.Contracts.".</summary>
    public List<string> Prefijos { get; init; } = [];
}

/// <summary>Cuerpo del POST con el que viaja un evento de integración.</summary>
public sealed record EventoEntrante(string Tipo, string Contenido);
