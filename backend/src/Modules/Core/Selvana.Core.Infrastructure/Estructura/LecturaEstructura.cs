using Microsoft.EntityFrameworkCore;
using Selvana.Core.Application.Estructura;
using Selvana.Core.Domain.Estructura;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Infrastructure.Estructura;

/// <summary>Lado de lectura: consultas sin tracking y paginadas en la base de datos.</summary>
internal sealed class LecturaEstructura(CoreDbContext db, TimeProvider reloj) : ILecturaEstructura
{
    /// <summary>Un perfil pedido a la IA hace menos de esto se considera "en redacción"; después se puede pedir otra vez.</summary>
    private static readonly TimeSpan VentanaRedaccion = TimeSpan.FromMinutes(10);

    public async Task<ImportacionDto?> ImportacionActualAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var importacion = await db.Set<ImportacionNomina>().AsNoTracking()
            .Include(i => i.Propuestas)
            .Where(i => EF.Property<Guid>(i, "EstructuraId") == empresaId && i.Estado != EstadoImportacion.Descartada)
            .OrderByDescending(i => i.LeidaEn)
            .FirstOrDefaultAsync(cancellationToken);

        return importacion is null ? null : Mapear(importacion);
    }

    public async Task<ResumenEstructuraDto> ResumenAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var desde = reloj.GetUtcNow() - VentanaRedaccion;
        var cargos = De<Cargo>(empresaId);
        var aplicada = await db.Set<ImportacionNomina>().AsNoTracking()
            .Where(i => EF.Property<Guid>(i, "EstructuraId") == empresaId && i.Estado == EstadoImportacion.Aplicada)
            .OrderByDescending(i => i.AplicadaEn)
            .Select(i => new { i.AplicadaEn, i.Fuente })
            .FirstOrDefaultAsync(cancellationToken);

        return new ResumenEstructuraDto(
            await De<Sede>(empresaId).CountAsync(cancellationToken),
            await De<Area>(empresaId).CountAsync(cancellationToken),
            await De<CentroCosto>(empresaId).CountAsync(cancellationToken),
            await cargos.CountAsync(cancellationToken),
            await cargos.CountAsync(c => c.EstadoPerfil == EstadoPerfil.BorradorIa, cancellationToken),
            await cargos.CountAsync(c => c.EstadoPerfil == EstadoPerfil.SinPerfil && c.PerfilSolicitadoEn > desde, cancellationToken),
            await cargos.CountAsync(c => c.EstadoPerfil == EstadoPerfil.SinPerfil && c.Activo && (c.PerfilSolicitadoEn == null || c.PerfilSolicitadoEn <= desde), cancellationToken),
            aplicada?.AplicadaEn,
            aplicada?.Fuente,
            await De<Sede>(empresaId).Where(s => s.Activo).OrderBy(s => s.Nombre).Select(s => s.Nombre).ToListAsync(cancellationToken),
            await De<Area>(empresaId).Where(a => a.Activo).OrderBy(a => a.Nombre).Select(a => a.Nombre).ToListAsync(cancellationToken));
    }

    public async Task<Pagina<CargoDto>> CargosAsync(Guid empresaId, FiltroListado filtro, string? estadoPerfil, CancellationToken cancellationToken)
    {
        var consulta = De<Cargo>(empresaId);
        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = $"%{filtro.Buscar.Trim()}%";
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nombre, patron) || EF.Functions.ILike(c.Area, patron));
        }

        if (Enum.TryParse<EstadoPerfil>(estadoPerfil, ignoreCase: true, out var estado))
        {
            consulta = consulta.Where(c => c.EstadoPerfil == estado);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var pagina = await consulta.OrderByDescending(c => c.Activo).ThenByDescending(c => c.Empleados).ThenBy(c => c.Nombre)
            .Skip(filtro.Pagina.Omitir).Take(filtro.Pagina.Tamano).ToListAsync(cancellationToken);

        var desde = reloj.GetUtcNow() - VentanaRedaccion;
        return new Pagina<CargoDto>([.. pagina.Select(c => Mapear(c, desde))], filtro.Pagina.Numero, filtro.Pagina.Tamano, total);
    }

    public Task<Pagina<MaestroDto>> SedesAsync(Guid empresaId, FiltroListado filtro, CancellationToken cancellationToken) =>
        Maestros<Sede>(empresaId, filtro, cancellationToken);

    public Task<Pagina<MaestroDto>> AreasAsync(Guid empresaId, FiltroListado filtro, CancellationToken cancellationToken) =>
        Maestros<Area>(empresaId, filtro, cancellationToken);

    public async Task<Pagina<CentroCostoDto>> CentrosCostoAsync(Guid empresaId, FiltroListado filtro, CancellationToken cancellationToken)
    {
        var consulta = De<CentroCosto>(empresaId);
        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = $"%{filtro.Buscar.Trim()}%";
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nombre, patron) || EF.Functions.ILike(c.Codigo, patron) || EF.Functions.ILike(c.Sede, patron));
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta.OrderByDescending(c => c.Activo).ThenBy(c => c.Codigo)
            .Skip(filtro.Pagina.Omitir).Take(filtro.Pagina.Tamano)
            .Select(c => new CentroCostoDto(c.Id, c.Codigo, c.Nombre, c.Sede, c.Empleados, c.Activo, c.EditadoManualmente))
            .ToListAsync(cancellationToken);
        return new Pagina<CentroCostoDto>(items, filtro.Pagina.Numero, filtro.Pagina.Tamano, total);
    }

    private async Task<Pagina<MaestroDto>> Maestros<T>(Guid empresaId, FiltroListado filtro, CancellationToken cancellationToken)
        where T : Maestro
    {
        var consulta = De<T>(empresaId);
        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = $"%{filtro.Buscar.Trim()}%";
            consulta = consulta.Where(m => EF.Functions.ILike(m.Nombre, patron) || EF.Functions.ILike(m.NombreNomina, patron));
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta.OrderByDescending(m => m.Activo).ThenByDescending(m => m.Empleados).ThenBy(m => m.Nombre)
            .Skip(filtro.Pagina.Omitir).Take(filtro.Pagina.Tamano)
            .Select(m => new MaestroDto(m.Id, m.Nombre, m.NombreNomina, m.Empleados, m.Activo, m.EditadoManualmente))
            .ToListAsync(cancellationToken);
        return new Pagina<MaestroDto>(items, filtro.Pagina.Numero, filtro.Pagina.Tamano, total);
    }

    private IQueryable<T> De<T>(Guid empresaId)
        where T : class => db.Set<T>().AsNoTracking().Where(x => EF.Property<Guid>(x, "EstructuraId") == empresaId);

    private static CargoDto Mapear(Cargo c, DateTimeOffset redaccionDesde) => new(
        c.Id, c.Nombre, c.Area, c.Empleados, c.CodigosNomina, c.NombresNomina, c.EstadoPerfil.ToString(),
        c.Perfil is null ? null : new PerfilDto(c.Perfil.Mision, c.Perfil.Funciones, c.Perfil.Requisitos, c.Perfil.RiesgoArl),
        [.. c.Razones.Select(r => new RazonDto(r.Signo, r.Texto, r.Fuente))],
        c.ModeloIa, c.AprobadoPor, c.AprobadoEn, c.Activo, c.EditadoManualmente,
        c.EstadoPerfil == EstadoPerfil.SinPerfil && c.PerfilSolicitadoEn > redaccionDesde);

    private static ImportacionDto Mapear(ImportacionNomina i)
    {
        var unionesIncluidas = i.Propuestas.Where(p => p.Incluida && p.Tipo == TipoPropuesta.UnirCargos).ToList();
        var cargosTrasUnir = i.Cargos.Count - unionesIncluidas.Sum(p => p.Codigos.Count) + unionesIncluidas.Count;
        return new ImportacionDto(
            i.Id, i.Fuente, i.LeidaEn, i.Estado.ToString(),
            new ConteosImportacionDto(i.Sedes.Count, i.Areas.Count, i.CentrosCosto.Count, i.Cargos.Count, cargosTrasUnir, i.TotalEmpleados,
                [.. i.Sedes.Select(s => s.Nombre)]),
            [.. i.Propuestas.OrderBy(p => p.Origen).ThenByDescending(p => p.Cantidad)
                .Select(p => new PropuestaDto(p.Id, p.Tipo.ToString(), p.Origen.ToString(), p.Titulo, p.Porque, p.Evidencia, p.Incluida))],
            i.Errores, i.MensajeIa, i.ModeloIa, i.AplicadaEn, i.AplicadaPor);
    }
}
