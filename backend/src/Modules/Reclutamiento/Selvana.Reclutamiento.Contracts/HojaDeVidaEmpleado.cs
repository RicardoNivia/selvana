namespace Selvana.Reclutamiento.Contracts;

public sealed record ExperienciaHv(string Cargo, string Empresa, string Desde, string Hasta);

public sealed record EstudioHv(string Titulo, string Institucion, string Anio);

/// <summary>
/// La hoja de vida de un empleado tal como la puede ver su empresa. <paramref name="Fuente"/>: «Postulación» (la que envió a una vacante de esa
/// empresa) o «Portal» (su perfil del portal, solo si la persona decidió compartirlo con esa empresa).
/// </summary>
public sealed record HojaDeVidaParaEmpresa(
    string Fuente, string Nombre, string Ciudad, string? LinkedIn, IReadOnlyList<ExperienciaHv> Experiencia, IReadOnlyList<EstudioHv> Formacion,
    IReadOnlyList<string> Cursos, IReadOnlyList<string> Habilidades, Guid? PostulacionId, string? Vacante, string? NombreArchivo, DateTimeOffset? Fecha);

public interface IHojasDeVidaParaEmpresa
{
    /// <summary>Por cédula (y correo, para el perfil del portal). Null si la empresa no tiene una hoja de vida de esa persona.</summary>
    Task<HojaDeVidaParaEmpresa?> ObtenerAsync(Guid empresaId, string documento, string correo, CancellationToken cancellationToken);

    /// <summary>El archivo del perfil del portal, solo si la persona lo compartió con la empresa.</summary>
    Task<(string Nombre, string TipoContenido, byte[] Contenido)?> AbrirCompartidaAsync(Guid empresaId, string documento, string correo,
        CancellationToken cancellationToken);
}
