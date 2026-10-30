namespace Selvana.Core.Contracts.Empresas;

/// <summary>Un usuario de la consola, para elegir líderes. Sin contraseña ni datos de sesión.</summary>
public sealed record UsuarioConsola(Guid Id, string Nombre, string Correo, string Rol, string? Cargo);

public interface IDirectorioUsuarios
{
    /// <summary>Usuarios activos o invitados de la empresa (sin los desactivados).</summary>
    Task<IReadOnlyList<UsuarioConsola>> ConsolaAsync(Guid empresaId, CancellationToken cancellationToken);
}
