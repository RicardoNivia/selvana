using Selvana.Core.Application.Abstracciones;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura;

/// <summary>La importación más reciente que no se descartó (abierta o ya aplicada), o null si no hay.</summary>
public sealed record ObtenerImportacionActualQuery : IQuery<ImportacionDto?>;

internal sealed class ObtenerImportacionActualHandler(IContextoUsuario usuario, ILecturaEstructura lectura)
    : IQueryHandler<ObtenerImportacionActualQuery, ImportacionDto?>
{
    public async Task<Result<ImportacionDto?>> Handle(ObtenerImportacionActualQuery query, CancellationToken cancellationToken) =>
        Result.Success(await lectura.ImportacionActualAsync(usuario.EmpresaId, cancellationToken));
}

public sealed record ObtenerResumenEstructuraQuery : IQuery<ResumenEstructuraDto>;

internal sealed class ObtenerResumenEstructuraHandler(IContextoUsuario usuario, ILecturaEstructura lectura)
    : IQueryHandler<ObtenerResumenEstructuraQuery, ResumenEstructuraDto>
{
    public async Task<Result<ResumenEstructuraDto>> Handle(ObtenerResumenEstructuraQuery query, CancellationToken cancellationToken) =>
        await lectura.ResumenAsync(usuario.EmpresaId, cancellationToken);
}

/// <summary>Cargos paginados. <paramref name="EstadoPerfil"/>: SinPerfil, BorradorIa o Aprobado.</summary>
public sealed record ListarCargosQuery(int? Pagina, int? Tamano, string? Buscar, string? EstadoPerfil) : IQuery<Pagina<CargoDto>>;

internal sealed class ListarCargosHandler(IContextoUsuario usuario, ILecturaEstructura lectura)
    : IQueryHandler<ListarCargosQuery, Pagina<CargoDto>>
{
    public async Task<Result<Pagina<CargoDto>>> Handle(ListarCargosQuery query, CancellationToken cancellationToken) =>
        await lectura.CargosAsync(usuario.EmpresaId, new FiltroListado(new PaginaSolicitada(query.Pagina, query.Tamano), query.Buscar),
            query.EstadoPerfil, cancellationToken);
}

public sealed record ObtenerCargoQuery(Guid CargoId) : IQuery<CargoDto>;

internal sealed class ObtenerCargoHandler(IContextoUsuario usuario, ILecturaEstructura lectura)
    : IQueryHandler<ObtenerCargoQuery, CargoDto>
{
    public async Task<Result<CargoDto>> Handle(ObtenerCargoQuery query, CancellationToken cancellationToken) =>
        await lectura.CargoAsync(usuario.EmpresaId, query.CargoId, cancellationToken) is { } cargo
            ? cargo
            : Result.Failure<CargoDto>(Domain.Estructura.EstructuraErrors.CargoNoExiste);
}

public sealed record ListarSedesQuery(int? Pagina, int? Tamano, string? Buscar) : IQuery<Pagina<MaestroDto>>;

internal sealed class ListarSedesHandler(IContextoUsuario usuario, ILecturaEstructura lectura)
    : IQueryHandler<ListarSedesQuery, Pagina<MaestroDto>>
{
    public async Task<Result<Pagina<MaestroDto>>> Handle(ListarSedesQuery query, CancellationToken cancellationToken) =>
        await lectura.SedesAsync(usuario.EmpresaId, new FiltroListado(new PaginaSolicitada(query.Pagina, query.Tamano), query.Buscar), cancellationToken);
}

public sealed record ListarAreasQuery(int? Pagina, int? Tamano, string? Buscar) : IQuery<Pagina<MaestroDto>>;

internal sealed class ListarAreasHandler(IContextoUsuario usuario, ILecturaEstructura lectura)
    : IQueryHandler<ListarAreasQuery, Pagina<MaestroDto>>
{
    public async Task<Result<Pagina<MaestroDto>>> Handle(ListarAreasQuery query, CancellationToken cancellationToken) =>
        await lectura.AreasAsync(usuario.EmpresaId, new FiltroListado(new PaginaSolicitada(query.Pagina, query.Tamano), query.Buscar), cancellationToken);
}

public sealed record ListarCentrosCostoQuery(int? Pagina, int? Tamano, string? Buscar) : IQuery<Pagina<CentroCostoDto>>;

internal sealed class ListarCentrosCostoHandler(IContextoUsuario usuario, ILecturaEstructura lectura)
    : IQueryHandler<ListarCentrosCostoQuery, Pagina<CentroCostoDto>>
{
    public async Task<Result<Pagina<CentroCostoDto>>> Handle(ListarCentrosCostoQuery query, CancellationToken cancellationToken) =>
        await lectura.CentrosCostoAsync(usuario.EmpresaId, new FiltroListado(new PaginaSolicitada(query.Pagina, query.Tamano), query.Buscar), cancellationToken);
}
