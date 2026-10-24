using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Contracts.Privacidad;

/// <summary>
/// Una persona de la empresa aprobó la supresión (o la revocatoria) que pidió un titular: los módulos que guardan
/// sus datos los eliminan y conservan solo la constancia de que revocó la autorización.
/// </summary>
public sealed record SupresionDeTitularAprobada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid SolicitudId, string Documento, string AprobadaPor) : IIntegrationEvent;
