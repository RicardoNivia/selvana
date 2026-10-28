using Selvana.SharedKernel.Messaging;

namespace Selvana.Expediente.Contracts.Ia;

/// <summary>
/// RRHH subió un documento a la carpeta del empleado y pidió leerlo: el worker lo descarga con la clave interna y la IA extrae sus datos.
/// Nunca se envían exámenes médicos (no están en los tipos de la carpeta).
/// </summary>
public sealed record DocumentoEmpleadoRecibido(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid EmpleadoId, Guid DocumentoId, Guid ArchivoId, string TipoEsperado, string TipoContenido)
    : IIntegrationEvent;
