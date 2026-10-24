namespace Selvana.Infrastructure.Common.Messaging;

/// <summary>Publica eventos de integración hacia otros módulos y hacia el AI.Worker.</summary>
public interface IEventBus
{
    Task PublishAsync(string type, string content, CancellationToken cancellationToken);
}
