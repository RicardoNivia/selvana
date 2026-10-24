namespace Selvana.SharedKernel.Messaging;

/// <summary>Evento entre módulos o hacia el AI.Worker; se publica a través del outbox.</summary>
public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTimeOffset OccurredOn { get; }
}
