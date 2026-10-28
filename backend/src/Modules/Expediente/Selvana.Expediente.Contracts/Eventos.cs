using Selvana.SharedKernel.Messaging;

namespace Selvana.Expediente.Contracts.Eventos;

/// <summary>Un empleado se retiró: lo usan los módulos que dependen de la planta activa (clima, desempeño, portal del empleado).</summary>
public sealed record EmpleadoRetirado(Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid EmpleadoId, DateOnly FechaRetiro, string Motivo) : IIntegrationEvent;
