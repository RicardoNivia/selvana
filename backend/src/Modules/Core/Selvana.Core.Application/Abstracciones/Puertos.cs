using Selvana.Core.Application.Sesion;
using Selvana.Core.Domain.Empresas;
using Selvana.Core.Domain.Estructura;
using Selvana.Core.Domain.Usuarios;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Application.Abstracciones;

/// <summary>Quién hace la petición y en qué empresa trabaja.</summary>
public interface IContextoUsuario
{
    Guid EmpresaId { get; }
    string Nombre { get; }

    /// <summary>Rol de la matriz de permisos: la bandeja muestra a cada rol lo que le toca decidir.</summary>
    string Rol { get; }

    /// <summary>Usuario de Selvana; null cuando la identidad viene de un emisor externo sin usuario enlazado.</summary>
    Guid? UsuarioId { get; }

    /// <summary>Empresas usuarias que ve; null = todas.</summary>
    IReadOnlyCollection<Guid>? EmpresasUsuarias { get; }
}

public interface IUsuarios
{
    /// <summary>Usuarios con ese correo (uno por empresa a la que tiene acceso).</summary>
    Task<IReadOnlyList<Usuario>> PorCorreoAsync(string correo, CancellationToken cancellationToken);

    Task<Usuario?> ObtenerAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken);

    Task<bool> ExisteCorreoAsync(Guid empresaId, string correo, CancellationToken cancellationToken);

    Task<Usuario?> PorInvitacionAsync(string huella, CancellationToken cancellationToken);

    /// <summary>Usuarios activos que pueden gestionar usuarios, sin contar a <paramref name="excepto"/>.</summary>
    Task<int> ContarAdministradoresAsync(Guid empresaId, Guid excepto, CancellationToken cancellationToken);

    void Agregar(Usuario usuario);
}

public interface IEmpresas
{
    Task<Empresa?> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken);
}

/// <summary>Enlaces de un solo uso: el token va a la persona y solo su huella SHA-256 se guarda.</summary>
public interface IGeneradorTokens
{
    (string Token, string Huella) Generar();

    string Huella(string token);
}

/// <summary>Deriva y verifica contraseñas; nunca guarda el texto.</summary>
public interface IHasherContrasenas
{
    string Calcular(string contrasena);

    /// <summary>Con hash null hace el mismo trabajo y devuelve false (para no revelar si el usuario existe).</summary>
    bool Verificar(string? hash, string contrasena);
}

public interface ILecturaSesion
{
    Task<SesionDto?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken);
}

public interface IEstructuras
{
    Task<EstructuraEmpresa?> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken);

    void Agregar(EstructuraEmpresa estructura);
}

/// <summary>
/// Agrega una línea a la bitácora encadenada de la empresa. Se guarda con el siguiente SaveChanges del módulo,
/// en la misma transacción que el cambio que registra.
/// </summary>
public interface IBitacora
{
    Task RegistrarAsync(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false, string modulo = "Core", DateTimeOffset? fecha = null, Guid? id = null, CancellationToken cancellationToken = default);
}

/// <summary>Encola eventos de integración en el outbox del módulo; salen al confirmar la transacción.</summary>
public interface IEventosSalientes
{
    void Publicar(IIntegrationEvent evento);
}

public sealed record LecturaArchivoNomina(IReadOnlyList<EmpleadoLeido> Empleados, IReadOnlyList<string> Errores);

/// <summary>Lee la nómina con el conector que corresponda (archivo, API o conector nativo).</summary>
public interface ILectorNomina
{
    Task<LecturaArchivoNomina> LeerAsync(Stream contenido, CancellationToken cancellationToken);
}
