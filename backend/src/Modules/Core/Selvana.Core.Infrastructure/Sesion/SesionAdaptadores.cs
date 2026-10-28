using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Sesion;
using Selvana.Core.Application.Usuarios;
using Selvana.Core.Domain.Empresas;
using Selvana.Core.Domain.Usuarios;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.Infrastructure.Common.Seguridad;

namespace Selvana.Core.Infrastructure.Sesion;

internal sealed class UsuariosRepositorio(CoreDbContext db) : IUsuarios
{
    private static readonly Rol[] Administran = [.. MatrizPermisos.RolesQuePueden(Accion.GestionarUsuarios)];

    public async Task<IReadOnlyList<Usuario>> PorCorreoAsync(string correo, CancellationToken cancellationToken) =>
        await db.Set<Usuario>().Where(u => u.Correo == correo).OrderByDescending(u => u.UltimoIngreso).ToListAsync(cancellationToken);

    public Task<Usuario?> ObtenerAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken) =>
        db.Set<Usuario>().FirstOrDefaultAsync(u => u.EmpresaId == empresaId && u.Id == usuarioId, cancellationToken);

    public Task<bool> ExisteCorreoAsync(Guid empresaId, string correo, CancellationToken cancellationToken) =>
        db.Set<Usuario>().AnyAsync(u => u.EmpresaId == empresaId && u.Correo == correo, cancellationToken);

    public Task<Usuario?> PorInvitacionAsync(string huella, CancellationToken cancellationToken) =>
        db.Set<Usuario>().FirstOrDefaultAsync(u => u.InvitacionHash == huella, cancellationToken);

    public Task<int> ContarAdministradoresAsync(Guid empresaId, Guid excepto, CancellationToken cancellationToken) =>
        db.Set<Usuario>().CountAsync(u => u.EmpresaId == empresaId && u.Id != excepto && u.Estado != EstadoUsuario.Desactivado && Administran.Contains(u.Rol), cancellationToken);

    public void Agregar(Usuario usuario) => db.Add(usuario);
}

internal sealed class EmpresasRepositorio(CoreDbContext db) : IEmpresas
{
    public Task<Empresa?> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken) =>
        db.Set<Empresa>().Include(e => e.EmpresasUsuarias).FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);
}

/// <summary>32 bytes aleatorios en Base64 URL; se guarda solo la huella SHA-256 en hexadecimal.</summary>
internal sealed class GeneradorTokens : IGeneradorTokens
{
    public (string Token, string Huella) Generar()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Huella(token));
    }

    public string Huella(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
}

/// <summary>PBKDF2-SHA256 compartido con el portal de la persona (ver ContrasenasPbkdf2).</summary>
internal sealed class HasherPbkdf2 : IHasherContrasenas
{
    public string Calcular(string contrasena) => ContrasenasPbkdf2.Calcular(contrasena);

    public bool Verificar(string? hash, string contrasena) => ContrasenasPbkdf2.Verificar(hash, contrasena);
}

internal sealed class LecturaSesion(CoreDbContext db) : ILecturaSesion
{
    public async Task<SesionDto?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var u = await db.Set<Usuario>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuarioId && x.Estado != EstadoUsuario.Desactivado, cancellationToken);
        if (u is null)
        {
            return null;
        }

        var e = await db.Set<Empresa>().AsNoTracking().Include(x => x.EmpresasUsuarias).FirstOrDefaultAsync(x => x.Id == u.EmpresaId, cancellationToken);
        if (e is null)
        {
            return null;
        }

        var visibles = e.EmpresasUsuarias.Where(x => x.Activa && u.VeEmpresaUsuaria(x.Id)).OrderBy(x => x.Nombre)
            .Select(x => new EmpresaUsuariaResumenDto(x.Id, x.Nombre)).ToList();
        var acciones = MatrizPermisos.Acciones.Where(a => MatrizPermisos.Puede(u.Rol, a)).Select(a => a.ToString()).ToList();
        return new SesionDto(u.Id, u.Nombre, u.Correo, u.Rol.ToString(), e.Id, e.Nombre, e.EsTemporal, u.TodasLasEmpresasUsuarias, visibles, acciones);
    }
}
