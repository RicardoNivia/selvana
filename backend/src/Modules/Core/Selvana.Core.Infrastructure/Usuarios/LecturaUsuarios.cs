using Microsoft.EntityFrameworkCore;
using Selvana.Core.Application.Usuarios;
using Selvana.Core.Domain.Empresas;
using Selvana.Core.Domain.Estructura;
using Selvana.Core.Domain.Usuarios;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Infrastructure.Usuarios;

internal sealed class LecturaUsuarios(CoreDbContext db, TimeProvider reloj) : ILecturaUsuarios
{
    public async Task<Pagina<UsuarioDto>> UsuariosAsync(Guid empresaId, PaginaSolicitada pagina, string? buscar, string? rol, CancellationToken cancellationToken)
    {
        var q = db.Set<Usuario>().AsNoTracking().Where(u => u.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(u => EF.Functions.ILike(u.Nombre, patron) || EF.Functions.ILike(u.Correo, patron) || EF.Functions.ILike(u.CargoNombre ?? "", patron));
        }

        if (Enum.TryParse<Rol>(rol, ignoreCase: true, out var filtroRol))
        {
            q = q.Where(u => u.Rol == filtroRol);
        }

        var total = await q.CountAsync(cancellationToken);
        var filas = await q.OrderBy(u => u.Estado == EstadoUsuario.Desactivado).ThenBy(u => u.Nombre)
            .Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);

        var nombres = await NombresEmpresasUsuariasAsync(empresaId, cancellationToken);
        var ahora = reloj.GetUtcNow();
        var items = filas.Select(u => new UsuarioDto(
            u.Id, u.Nombre, u.Correo, u.Rol.ToString(), u.Estado.ToString(), u.TodasLasEmpresasUsuarias,
            [.. u.EmpresasUsuarias.Where(nombres.ContainsKey).Select(id => new EmpresaUsuariaResumenDto(id, nombres[id])).OrderBy(e => e.Nombre)],
            u.CargoNombre, u.UltimoIngreso, u.RolSugeridoIa?.ToString(), u.RolCorregido, u.InvitacionVigente(ahora), u.InvitadoEn, u.InvitadoPor)).ToList();
        return new Pagina<UsuarioDto>(items, pagina.Numero, pagina.Tamano, total);
    }

    public async Task<IReadOnlyList<EmpresaUsuariaDto>> EmpresasUsuariasAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var empresa = await db.Set<Empresa>().AsNoTracking().Include(e => e.EmpresasUsuarias).FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);
        if (empresa is null)
        {
            return [];
        }

        // Pocas personas por empresa: se cuentan en memoria quién ve cada empresa usuaria
        var accesos = await db.Set<Usuario>().AsNoTracking().Where(u => u.EmpresaId == empresaId && u.Estado != EstadoUsuario.Desactivado)
            .Select(u => new { u.TodasLasEmpresasUsuarias, u.EmpresasUsuarias }).ToListAsync(cancellationToken);

        return [.. empresa.EmpresasUsuarias.OrderByDescending(e => e.Activa).ThenBy(e => e.Nombre).Select(e => new EmpresaUsuariaDto(
            e.Id, e.Nombre, e.Nit, e.Sector, e.Ciudad, e.Activa,
            accesos.Count(a => a.TodasLasEmpresasUsuarias || a.EmpresasUsuarias.Contains(e.Id)), e.CreadaEn))];
    }

    public async Task<InvitacionDto?> InvitacionAsync(string huella, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var u = await db.Set<Usuario>().AsNoTracking().FirstOrDefaultAsync(x => x.InvitacionHash == huella, cancellationToken);
        if (u is null)
        {
            return null;
        }

        var empresa = await db.Set<Empresa>().AsNoTracking().Where(e => e.Id == u.EmpresaId).Select(e => e.Nombre).FirstOrDefaultAsync(cancellationToken);
        return new InvitacionDto(u.Nombre, u.Correo, empresa ?? "", u.Rol.ToString(), u.InvitacionVigente(ahora));
    }

    public async Task<(string Nombre, string Area, int Empleados)?> CargoAsync(Guid empresaId, Guid cargoId, CancellationToken cancellationToken)
    {
        var cargo = await db.Set<Cargo>().AsNoTracking()
            .Where(c => EF.Property<Guid>(c, "EstructuraId") == empresaId && c.Id == cargoId)
            .Select(c => new { c.Nombre, c.Area, c.Empleados }).FirstOrDefaultAsync(cancellationToken);
        return cargo is null ? null : (cargo.Nombre, cargo.Area, cargo.Empleados);
    }

    private async Task<Dictionary<Guid, string>> NombresEmpresasUsuariasAsync(Guid empresaId, CancellationToken cancellationToken) =>
        await db.Set<EmpresaUsuaria>().AsNoTracking().Where(e => EF.Property<Guid>(e, "EmpresaId") == empresaId)
            .ToDictionaryAsync(e => e.Id, e => e.Nombre, cancellationToken);
}
