namespace Selvana.Reclutamiento.Contracts;

/// <summary>
/// Lo que Vinculación necesita de una postulación para abrir el proceso de contratación: la persona, la vacante
/// y la aspiración que dio en la preselección. Solo se entrega a quien ve esa vacante.
/// </summary>
public sealed record PostulanteContratable(
    Guid PostulacionId, string NumeroPostulacion, Guid CandidatoId, Guid VacanteId, string CodigoVacante, string Etapa, Guid? ProcesoVinculacionId,
    string Nombre, string Documento, string Correo, string Celular, string Ciudad,
    string Vacante, string Cargo, string CiudadVacante, string Jornada, string Descripcion, IReadOnlyList<string> Requisitos, IReadOnlyList<string> Competencias,
    decimal SalarioMinimo, decimal SalarioMaximo, decimal? AspiracionSalarialCop,
    string Empresa, Guid? EmpresaUsuariaId, string? EmpresaUsuaria);

/// <summary>Lo único que Vinculación puede leer de Reclutamiento: el postulante que se va a contratar.</summary>
public interface IPostulacionesContratables
{
    Task<PostulanteContratable?> ObtenerAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken);
}
