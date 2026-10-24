using Selvana.Core.Contracts.Estructura;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application;

/// <summary>Vacantes de la empresa que el usuario puede ver; <paramref name="EmpresaUsuariaId"/> filtra por la empresa usuaria elegida en la barra lateral.</summary>
public sealed record ListarVacantesQuery(int? Pagina, int? Tamano, string? Buscar, string? Estado, Guid? EmpresaUsuariaId = null) : IQuery<Pagina<VacanteResumenDto>>;

internal sealed class ListarVacantesHandler(IUsuarioActual usuario, ILecturaReclutamiento lectura) : IQueryHandler<ListarVacantesQuery, Pagina<VacanteResumenDto>>
{
    public async Task<Result<Pagina<VacanteResumenDto>>> Handle(ListarVacantesQuery q, CancellationToken ct) =>
        await lectura.VacantesAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, q.EmpresaUsuariaId, new PaginaSolicitada(q.Pagina, q.Tamano), q.Buscar, q.Estado, ct);
}

public sealed record ObtenerVacanteQuery(Guid VacanteId) : IQuery<VacanteDto>;

internal sealed class ObtenerVacanteHandler(IUsuarioActual usuario, ILecturaReclutamiento lectura, ICatalogoCargos catalogo) : IQueryHandler<ObtenerVacanteQuery, VacanteDto>
{
    public async Task<Result<VacanteDto>> Handle(ObtenerVacanteQuery q, CancellationToken ct)
    {
        if (await lectura.VacanteAsync(usuario.EmpresaId, q.VacanteId, ct) is not { } v || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        // Puede decidir si su rol aprueba la etapa pendiente
        return v with
        {
            Aprobacion = v.Aprobacion with { PuedeDecidir = v.Aprobacion.QuienesAprueban.Contains(usuario.Rol) },
            CargoCatalogo = await CargoAsync(v, ct),
        };
    }

    /// <summary>El cargo del que parte la vacante o, si se redactó desde cero, el del catálogo con el mismo nombre (para no duplicarlo).</summary>
    private async Task<CargoCatalogoDto?> CargoAsync(VacanteDto v, CancellationToken ct)
    {
        if (v.CargoId is { } id && await catalogo.ObtenerAsync(usuario.EmpresaId, id, ct) is { } vinculado)
        {
            return new CargoCatalogoDto(vinculado.Id, vinculado.Nombre, vinculado.Area, vinculado.Empleados, vinculado.EstadoPerfil, Vinculado: true);
        }

        if (string.IsNullOrWhiteSpace(v.Cargo))
        {
            return null;
        }

        var igual = (await catalogo.BuscarAsync(usuario.EmpresaId, v.Cargo, 10, ct))
            .FirstOrDefault(c => string.Equals(c.Nombre.Trim(), v.Cargo.Trim(), StringComparison.OrdinalIgnoreCase));
        return igual is null ? null : new CargoCatalogoDto(igual.Id, igual.Nombre, igual.Area, igual.Empleados, igual.EstadoPerfil);
    }
}

/// <summary>Cargos del catálogo para elegir de cuál parte una vacante; primero los de perfil aprobado.</summary>
public sealed record BuscarCargosCatalogoQuery(string? Buscar) : IQuery<IReadOnlyList<CargoCatalogoDto>>;

internal sealed class BuscarCargosCatalogoHandler(IUsuarioActual usuario, ICatalogoCargos catalogo) : IQueryHandler<BuscarCargosCatalogoQuery, IReadOnlyList<CargoCatalogoDto>>
{
    public async Task<Result<IReadOnlyList<CargoCatalogoDto>>> Handle(BuscarCargosCatalogoQuery q, CancellationToken ct) =>
        (await catalogo.BuscarAsync(usuario.EmpresaId, q.Buscar, 20, ct)).Select(c => new CargoCatalogoDto(c.Id, c.Nombre, c.Area, c.Empleados, c.EstadoPerfil)).ToList();
}

public sealed record ListarPostulantesQuery(Guid VacanteId, int? Pagina, int? Tamano, string? Buscar, string? Canal) : IQuery<Pagina<PostulanteDto>>;

internal sealed class ListarPostulantesHandler(IUsuarioActual usuario, ILecturaReclutamiento lectura) : IQueryHandler<ListarPostulantesQuery, Pagina<PostulanteDto>>
{
    public async Task<Result<Pagina<PostulanteDto>>> Handle(ListarPostulantesQuery q, CancellationToken ct)
    {
        // Los postulantes de una vacante solo los ve quien ve su empresa usuaria
        if (await lectura.VacanteAsync(usuario.EmpresaId, q.VacanteId, ct) is not { } v || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        return await lectura.PostulantesAsync(usuario.EmpresaId, q.VacanteId, new PaginaSolicitada(q.Pagina, q.Tamano), q.Buscar, q.Canal, ct);
    }
}

/// <summary>Portal público: vacantes publicadas de todas las empresas. No requiere sesión.</summary>
public sealed record ListarVacantesPublicasQuery(int? Pagina, int? Tamano, string? Buscar, string? Ciudad, string? Jornada) : IQuery<PortalDto>;

internal sealed class ListarVacantesPublicasHandler(ILecturaReclutamiento lectura) : IQueryHandler<ListarVacantesPublicasQuery, PortalDto>
{
    public async Task<Result<PortalDto>> Handle(ListarVacantesPublicasQuery q, CancellationToken ct) =>
        await lectura.PortalAsync(new PaginaSolicitada(q.Pagina, q.Tamano), q.Buscar, q.Ciudad, q.Jornada, ct);
}

public sealed record ObtenerVacantePublicaQuery(string Codigo) : IQuery<VacantePublicaDetalleDto>;

internal sealed class ObtenerVacantePublicaHandler(ILecturaReclutamiento lectura) : IQueryHandler<ObtenerVacantePublicaQuery, VacantePublicaDetalleDto>
{
    public async Task<Result<VacantePublicaDetalleDto>> Handle(ObtenerVacantePublicaQuery q, CancellationToken ct) =>
        await lectura.VacantePublicaAsync(q.Codigo, ct) is { } v ? v : VacanteErrors.NoExiste;
}

public sealed record ObtenerBorradorPostulacionQuery(Guid BorradorId) : IQuery<BorradorPostulacionDto>;

internal sealed class ObtenerBorradorPostulacionHandler(ILecturaReclutamiento lectura) : IQueryHandler<ObtenerBorradorPostulacionQuery, BorradorPostulacionDto>
{
    public async Task<Result<BorradorPostulacionDto>> Handle(ObtenerBorradorPostulacionQuery q, CancellationToken ct) =>
        await lectura.BorradorAsync(q.BorradorId, ct) is { } b ? b : Domain.Postulaciones.BorradorErrors.NoExiste;
}
