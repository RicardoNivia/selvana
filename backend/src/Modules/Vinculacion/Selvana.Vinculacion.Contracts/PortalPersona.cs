namespace Selvana.Vinculacion.Contracts;

/// <summary>
/// Un proceso de contratación tal como lo ve la persona en su portal (ADR 0013): sin salarios negociados internos ni notas de RRHH.
/// <paramref name="Pendiente"/> dice qué debe hacer ella (aceptar y firmar, subir documentos); null si no le toca nada.
/// </summary>
public sealed record ProcesoDeLaPersona(
    Guid Id, Guid PostulacionId, string Empresa, string Cargo, string Etapa, bool Vinculado, bool Cancelado, string? Pendiente, DateTimeOffset ActualizadoEn);

/// <summary>Procesos de contratación de la persona, buscados por su cédula y su correo (los dos deben coincidir).</summary>
public interface IProcesosDeLaPersona
{
    Task<IReadOnlyList<ProcesoDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken);

    /// <summary>
    /// Enlace para responder desde el portal (oferta, firma o documentos). Renueva el enlace solo si el proceso es de esa persona
    /// y tiene algo pendiente; null si no.
    /// </summary>
    Task<string?> AbrirAsync(Guid procesoId, string documento, string correo, CancellationToken cancellationToken);
}
