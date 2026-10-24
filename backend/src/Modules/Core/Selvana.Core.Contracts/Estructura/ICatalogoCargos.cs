namespace Selvana.Core.Contracts.Estructura;

/// <summary>Un cargo del catálogo de la empresa con su perfil (si lo tiene). EstadoPerfil: SinPerfil, BorradorIa o Aprobado.</summary>
public sealed record CargoCatalogo(
    Guid Id, string Nombre, string Area, int Empleados, string EstadoPerfil,
    string Mision, IReadOnlyList<string> Funciones, IReadOnlyList<string> Requisitos, string RiesgoArl);

/// <summary>Lo que otros módulos pueden saber del catálogo de cargos: solo los activos, por este contrato y nunca por las tablas de Core.</summary>
public interface ICatalogoCargos
{
    Task<CargoCatalogo?> ObtenerAsync(Guid empresaId, Guid cargoId, CancellationToken cancellationToken);

    /// <summary>Busca por nombre o área; primero los que tienen perfil aprobado.</summary>
    Task<IReadOnlyList<CargoCatalogo>> BuscarAsync(Guid empresaId, string? texto, int maximo, CancellationToken cancellationToken);
}
