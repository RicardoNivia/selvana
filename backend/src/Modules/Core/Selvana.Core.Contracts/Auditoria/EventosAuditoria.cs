using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Contracts.Auditoria;

/// <summary>Quién hizo una acción: una persona de la consola, un agente de IA, un proceso del sistema o el titular de los datos.</summary>
public static class TipoActor
{
    public const string Persona = "Persona";
    public const string Ia = "Ia";
    public const string Sistema = "Sistema";
    public const string Titular = "Titular";
}

/// <summary>
/// Una acción que debe quedar en la bitácora de auditoría de la empresa. Los módulos la publican en su outbox
/// (misma transacción que el cambio) y Core la agrega a la bitácora encadenada.
/// </summary>
/// <param name="Nota">Para la IA: quién lo aprobó o si espera aprobación. Para datos sensibles: el motivo.</param>
public sealed record AccionAuditada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, string TipoActor, string Actor, string Accion, string Detalle, string? Nota,
    string Modulo, string Entidad, Guid? EntidadId, bool DatoSensible) : IIntegrationEvent;
