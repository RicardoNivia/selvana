namespace Selvana.Reclutamiento.Contracts;

/// <summary>Resumen de lo que Reclutamiento guarda de un titular, para responder sus solicitudes (Ley 1581).</summary>
public sealed record DatosTitularReclutamiento(
    string Nombre, string Celular, string Correo, string Ciudad, int Postulaciones, IReadOnlyList<string> Vacantes,
    bool TieneHojaDeVida, DateTimeOffset? UltimaPostulacion, string? VersionAutorizacion, bool AutorizacionRevocada);

/// <summary>Lo único que otros módulos pueden consultar de Reclutamiento sobre un titular.</summary>
public interface IDatosTitulares
{
    Task<DatosTitularReclutamiento?> ObtenerAsync(Guid empresaId, string documento, CancellationToken cancellationToken);

    /// <summary>Empresas que tienen datos de ese documento (para repartir una solicitud que llegó por el portal).</summary>
    Task<IReadOnlyList<Guid>> EmpresasConDatosAsync(string documento, CancellationToken cancellationToken);
}
