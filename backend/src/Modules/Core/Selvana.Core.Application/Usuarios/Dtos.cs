using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Usuarios;

public sealed record EmpresaUsuariaResumenDto(Guid Id, string Nombre);

public sealed record UsuarioDto(
    Guid Id, string Nombre, string Correo, string Rol, string Estado, bool TodasLasEmpresasUsuarias,
    IReadOnlyList<EmpresaUsuariaResumenDto> EmpresasUsuarias, string? CargoNombre, DateTimeOffset? UltimoIngreso,
    string? RolSugeridoIa, bool RolCorregido, bool InvitacionVigente, DateTimeOffset InvitadoEn, string InvitadoPor);

public sealed record SugerenciaRolDto(string Rol, string Razon, string Evidencia);

/// <summary>El enlace solo se devuelve al crear o renovar la invitación; después no se puede volver a leer.</summary>
public sealed record InvitacionEmitidaDto(Guid UsuarioId, string Token, DateTimeOffset Vence);

public sealed record InvitacionDto(string Nombre, string Correo, string Empresa, string Rol, bool Vigente);

public sealed record CeldaPermisoDto(string Rol, string Alcance);

public sealed record PermisoDto(string Accion, string Texto, string? Nota, bool Sensible, IReadOnlyList<CeldaPermisoDto> Celdas);

public sealed record MatrizPermisosDto(IReadOnlyList<string> Roles, IReadOnlyList<PermisoDto> Permisos);

public sealed record EmpresaUsuariaDto(Guid Id, string Nombre, string Nit, string Sector, string Ciudad, bool Activa, int Usuarios, DateTimeOffset CreadaEn);

/// <summary>Lado de lectura de usuarios y empresas usuarias (proyecciones sin tracking).</summary>
public interface ILecturaUsuarios
{
    Task<Pagina<UsuarioDto>> UsuariosAsync(Guid empresaId, PaginaSolicitada pagina, string? buscar, string? rol, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmpresaUsuariaDto>> EmpresasUsuariasAsync(Guid empresaId, CancellationToken cancellationToken);

    Task<InvitacionDto?> InvitacionAsync(string huella, DateTimeOffset ahora, CancellationToken cancellationToken);

    /// <summary>Cargo de la estructura para sugerir el rol: nombre, área y cuántas personas lo tienen.</summary>
    Task<(string Nombre, string Area, int Empleados)?> CargoAsync(Guid empresaId, Guid cargoId, CancellationToken cancellationToken);
}
