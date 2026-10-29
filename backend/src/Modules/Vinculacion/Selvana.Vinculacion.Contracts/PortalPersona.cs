using Selvana.SharedKernel.Results;

namespace Selvana.Vinculacion.Contracts;

/// <summary>
/// Un proceso de contratación tal como lo ve la persona en su portal (ADR 0013): sin salarios negociados internos ni notas de RRHH.
/// <paramref name="Pendiente"/> dice qué debe hacer ella (aceptar y firmar, subir documentos); null si no le toca nada.
/// </summary>
public sealed record ProcesoDeLaPersona(
    Guid Id, Guid PostulacionId, string Empresa, string Cargo, string Etapa, bool Vinculado, bool Cancelado, string? Pendiente, DateTimeOffset ActualizadoEn);

/// <summary>
/// Un documento de ingreso que un proceso le pide a la persona (P4). <paramref name="Tipo"/> es la clave del documento
/// (Cedula, CertificadoBancario…); <paramref name="Estado"/>: Pendiente, Rechazado, Leyendo, PorRevisar o Aprobado.
/// </summary>
public sealed record DocumentoPedidoALaPersona(
    Guid ProcesoId, string Empresa, string Cargo, string Tipo, string Nombre, bool Obligatorio, string Estado, string? MotivoRechazo);

/// <summary>Un archivo que la persona entrega desde «Mis documentos» de su portal.</summary>
public sealed record EntregaDesdePortal(Guid ProcesoId, string Tipo, string NombreArchivo, string TipoContenido, byte[] Contenido);

/// <summary>Procesos de contratación de la persona, buscados por su cédula y su correo (los dos deben coincidir).</summary>
public interface IProcesosDeLaPersona
{
    /// <summary>Documentos de ingreso que le piden sus procesos abiertos (los que carga la empresa no aparecen).</summary>
    Task<IReadOnlyList<DocumentoPedidoALaPersona>> DocumentosPedidosAsync(string documento, string correo, CancellationToken cancellationToken);

    /// <summary>
    /// Entrega un documento que la persona ya tenía en su portal: llega al proceso como si lo hubiera subido por su enlace
    /// (la IA lo lee y RRHH lo aprueba). Solo si el proceso es de esa persona.
    /// </summary>
    Task<Result> EntregarDocumentoAsync(string documento, string correo, EntregaDesdePortal entrega, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcesoDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken);

    /// <summary>
    /// Enlace para responder desde el portal (oferta, firma o documentos). Renueva el enlace solo si el proceso es de esa persona
    /// y tiene algo pendiente; null si no.
    /// </summary>
    Task<string?> AbrirAsync(Guid procesoId, string documento, string correo, CancellationToken cancellationToken);
}
