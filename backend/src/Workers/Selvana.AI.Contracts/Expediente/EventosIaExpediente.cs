using Selvana.AI.Contracts.Vinculacion;
using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Contracts.Expediente;

/// <summary>Lo que leyó la IA de un documento de la carpeta del empleado. Si falló, trae <see cref="Error"/> y RRHH lo revisa a mano.</summary>
public sealed record DocumentoEmpleadoLeido(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid EmpleadoId, Guid DocumentoId, Guid ArchivoId, string TipoDetectado, bool Legible, bool TipoCorrecto,
    IReadOnlyList<CampoDocumento> Campos, IReadOnlyList<string> Observaciones, IReadOnlyList<RazonRedaccion> Razones, string Modelo, decimal CostoUsd, string? Error)
    : IIntegrationEvent;
