using Selvana.SharedKernel.Messaging;

namespace Selvana.Vinculacion.Contracts.Eventos;

/// <summary>Una persona aprobó contratar al candidato de la terna: Reclutamiento marca la postulación.</summary>
public sealed record ContratacionAprobada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid PostulacionId, Guid ProcesoId, string Por) : IIntegrationEvent;

/// <summary>El proceso de vinculación se canceló (el candidato no aceptó o la empresa desistió): la postulación vuelve a la terna.</summary>
public sealed record ContratacionCancelada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid PostulacionId, Guid ProcesoId, string Motivo, string Por) : IIntegrationEvent;
