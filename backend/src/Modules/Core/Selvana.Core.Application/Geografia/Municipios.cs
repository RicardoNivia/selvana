using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Geografia;

/// <summary>Municipio de la división político-administrativa del DANE (DIVIPOLA), con sus códigos.</summary>
public sealed record MunicipioDto(string Codigo, string Municipio, string CodigoDepartamento, string Departamento, string? Subregion);

/// <summary>Catálogo DIVIPOLA: datos de referencia de solo lectura que se cargan como semilla.</summary>
public interface ICatalogoMunicipios
{
    /// <summary>Busca por nombre de municipio, departamento o subregión, sin importar tildes ni mayúsculas, o por código DANE.</summary>
    Task<IReadOnlyList<MunicipioDto>> BuscarAsync(string? texto, int tamano, CancellationToken cancellationToken);

    Task<MunicipioDto?> ObtenerAsync(string codigo, CancellationToken cancellationToken);
}

/// <summary>Autocompletado de municipios: los primeros <paramref name="Tamano"/> que coinciden (10 por defecto, máximo 50).</summary>
public sealed record BuscarMunicipiosQuery(string? Buscar, int? Tamano) : IQuery<IReadOnlyList<MunicipioDto>>;

internal sealed class BuscarMunicipiosHandler(ICatalogoMunicipios catalogo) : IQueryHandler<BuscarMunicipiosQuery, IReadOnlyList<MunicipioDto>>
{
    public async Task<Result<IReadOnlyList<MunicipioDto>>> Handle(BuscarMunicipiosQuery query, CancellationToken cancellationToken) =>
        Result.Success(await catalogo.BuscarAsync(query.Buscar, Math.Clamp(query.Tamano ?? 10, 1, 50), cancellationToken));
}
