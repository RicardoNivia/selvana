namespace Selvana.Infrastructure.Common.Outbox;

/// <summary>Evento de integración pendiente de publicar; vive en la tabla outbox_messages de cada esquema.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset OccurredOn { get; init; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public string? Error { get; set; }
}
