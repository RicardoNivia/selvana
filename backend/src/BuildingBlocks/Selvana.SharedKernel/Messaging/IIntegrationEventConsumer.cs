namespace Selvana.SharedKernel.Messaging;

/// <summary>Reacciona a un evento de integración recibido por el inbox del módulo (o del AI.Worker).</summary>
public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}
