namespace Selvana.Core.Contracts.Empresas;

public sealed record EmpresaUsuariaResumen(Guid Id, string Nombre, string Ciudad, bool Activa);

public sealed record EmpresaResumen(Guid Id, string Nombre, bool EsTemporal, IReadOnlyList<EmpresaUsuariaResumen> EmpresasUsuarias);

/// <summary>Lo único que otros módulos pueden saber de una empresa. Se consulta por este contrato, nunca por las tablas de Core.</summary>
public interface IDirectorioEmpresas
{
    Task<EmpresaResumen?> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken);
}
