using System.Text.RegularExpressions;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Domain.Usuarios;

public enum EstadoUsuario
{
    Invitado,
    Activo,
    Desactivado,
}

public static class UsuarioErrors
{
    public static readonly Error CorreoInvalido = Error.Validation("Usuario.CorreoInvalido", "Escriba un correo válido, por ejemplo nombre@empresa.co.");
    public static readonly Error NombreVacio = Error.Validation("Usuario.NombreVacio", "Escriba el nombre de la persona.");
    public static readonly Error CandidatoNoSeInvita = Error.Validation("Usuario.Candidato", "Los candidatos no se invitan: se registran solos en el portal de empleo.");
    public static readonly Error SinEmpresasUsuarias = Error.Validation("Usuario.SinEmpresasUsuarias", "Asigne al menos una empresa usuaria o marque «Todas».");
    public static readonly Error CorreoRepetido = Error.Conflict("Usuario.CorreoRepetido", "Ya hay una persona con ese correo en la empresa.");
    public static readonly Error NoExiste = Error.NotFound("Usuario.NoExiste", "Esa persona no existe.");
    public static readonly Error UltimoAdministrador = Error.Conflict("Usuario.UltimoAdministrador", "No puede quitarse el último acceso de administración de la empresa.");
    public static readonly Error CredencialesInvalidas = Error.Validation("Sesion.CredencialesInvalidas", "El correo o la contraseña no coinciden. Revíselos e intente de nuevo.");
    public static readonly Error InvitacionVencida = Error.Validation("Usuario.InvitacionVencida", "Este enlace de invitación ya no sirve. Pida a quien lo invitó que le envíe uno nuevo.");
    public static readonly Error YaActivo = Error.Conflict("Usuario.YaActivo", "Esta persona ya creó su contraseña; puede ingresar con su correo.");
    public static readonly Error ContrasenaDebil = Error.Validation("Usuario.ContrasenaDebil", "Use al menos 10 caracteres, con letras y números.");
    public static readonly Error Desactivado = Error.Conflict("Sesion.Desactivado", "Su acceso a Selvana está desactivado. Pida al administrador de su empresa que lo active.");
}

/// <summary>Por qué no se dejó entrar a alguien.</summary>
public enum RechazoIngreso
{
    CredencialesInvalidas,
    Bloqueado,
    Desactivado,
}

/// <summary>Una persona con acceso a la consola de una empresa. Si la IA sugirió su rol, queda registrado si se aceptó o se corrigió.</summary>
public sealed partial class Usuario : AggregateRoot<Guid>
{
    private Usuario(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string Correo { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public Rol Rol { get; private set; }
    public Guid? CargoId { get; private set; }
    public string? CargoNombre { get; private set; }

    /// <summary>Ve todas las empresas usuarias (también las que se creen después).</summary>
    public bool TodasLasEmpresasUsuarias { get; private set; }
    public List<Guid> EmpresasUsuarias { get; private set; } = [];
    public EstadoUsuario Estado { get; private set; }
    public DateTimeOffset InvitadoEn { get; private init; }
    public string InvitadoPor { get; private init; } = "";
    public DateTimeOffset? UltimoIngreso { get; private set; }
    public Rol? RolSugeridoIa { get; private init; }

    /// <summary>Contraseña derivada con PBKDF2 (nunca el texto). Vacía si la persona entra solo con Microsoft o Google.</summary>
    public string? ContrasenaHash { get; private set; }
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }

    /// <summary>Huella SHA-256 del enlace de invitación (el enlace solo lo ve quien invita, una vez).</summary>
    public string? InvitacionHash { get; private set; }
    public DateTimeOffset? InvitacionVence { get; private set; }

    public const int MaximoIntentos = 5;
    public static readonly TimeSpan VigenciaInvitacion = TimeSpan.FromDays(7);
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);
    public string? RazonRolIa { get; private init; }

    /// <summary>La persona que invitó cambió el rol que sugirió la IA.</summary>
    public bool RolCorregido => RolSugeridoIa is not null && RolSugeridoIa != Rol;

    public static Result<Usuario> Invitar(
        Guid empresaId, string correo, string nombre, Rol rol, Guid? cargoId, string? cargoNombre,
        bool todasLasEmpresasUsuarias, IEnumerable<Guid> empresasUsuarias, Rol? rolSugeridoIa, string? razonRolIa,
        string invitadoPor, DateTimeOffset ahora)
    {
        var validacion = Validar(correo, nombre, rol, todasLasEmpresasUsuarias, empresasUsuarias);
        if (validacion.IsFailure)
        {
            return validacion.Error;
        }

        return new Usuario(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            Correo = NormalizarCorreo(correo),
            Nombre = nombre.Trim(),
            Rol = rol,
            CargoId = cargoId,
            CargoNombre = cargoNombre,
            TodasLasEmpresasUsuarias = todasLasEmpresasUsuarias,
            EmpresasUsuarias = todasLasEmpresasUsuarias ? [] : [.. empresasUsuarias.Distinct()],
            Estado = EstadoUsuario.Invitado,
            InvitadoEn = ahora,
            InvitadoPor = invitadoPor,
            RolSugeridoIa = rolSugeridoIa,
            RazonRolIa = razonRolIa,
        };
    }

    public Result Cambiar(string nombre, Rol rol, bool todasLasEmpresasUsuarias, IEnumerable<Guid> empresasUsuarias, bool activo)
    {
        var validacion = Validar(Correo, nombre, rol, todasLasEmpresasUsuarias, empresasUsuarias);
        if (validacion.IsFailure)
        {
            return validacion;
        }

        Nombre = nombre.Trim();
        Rol = rol;
        TodasLasEmpresasUsuarias = todasLasEmpresasUsuarias;
        EmpresasUsuarias = todasLasEmpresasUsuarias ? [] : [.. empresasUsuarias.Distinct()];
        Estado = !activo ? EstadoUsuario.Desactivado : Estado == EstadoUsuario.Desactivado ? EstadoUsuario.Activo : Estado;
        return Result.Success();
    }

    /// <summary>Nuevo enlace de invitación; anula el anterior. Solo para quien aún no creó su contraseña.</summary>
    public Result EmitirInvitacion(string huella, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        if (ContrasenaHash is not null)
        {
            return Result.Failure(UsuarioErrors.YaActivo);
        }

        InvitacionHash = huella;
        InvitacionVence = ahora.Add(VigenciaInvitacion);
        return Result.Success();
    }

    public bool InvitacionVigente(DateTimeOffset ahora) =>
        InvitacionHash is not null && InvitacionVence is { } vence && vence > ahora && Estado != EstadoUsuario.Desactivado;

    /// <summary>Quien registra la empresa (autoservicio) queda como administrador activo, con la contraseña que eligió.</summary>
    public static Result<Usuario> RegistrarDueno(Guid empresaId, string correo, string nombre, string contrasenaHash, DateTimeOffset ahora)
    {
        var r = Invitar(empresaId, correo, nombre, Rol.Administrador, null, null, true, [], null, null, "Registro en línea", ahora);
        if (r.IsFailure)
        {
            return r;
        }

        r.Value.EstablecerContrasena(contrasenaHash);
        r.Value.Estado = EstadoUsuario.Activo;
        return r;
    }

    /// <summary>La persona abrió su enlace y creó la contraseña: el enlace deja de servir.</summary>
    public Result ActivarCuenta(string contrasenaHash, DateTimeOffset ahora)
    {
        if (!InvitacionVigente(ahora))
        {
            return Result.Failure(UsuarioErrors.InvitacionVencida);
        }

        EstablecerContrasena(contrasenaHash);
        InvitacionHash = null;
        InvitacionVence = null;
        Estado = EstadoUsuario.Activo;
        return Result.Success();
    }

    /// <summary>Al menos 10 caracteres, con letras y números.</summary>
    public static bool ContrasenaAceptable(string? contrasena) =>
        contrasena is { Length: >= 10 and <= 200 } && contrasena.Any(char.IsLetter) && contrasena.Any(char.IsDigit);

    public void EstablecerContrasena(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ContrasenaHash = hash;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public bool EstaBloqueado(DateTimeOffset ahora) => BloqueadoHasta is { } hasta && hasta > ahora;

    /// <summary>Antes de comparar la contraseña: ¿puede intentar entrar ahora?</summary>
    public RechazoIngreso? PuedeIntentar(DateTimeOffset ahora) =>
        Estado == EstadoUsuario.Desactivado ? RechazoIngreso.Desactivado
        : EstaBloqueado(ahora) ? RechazoIngreso.Bloqueado
        : ContrasenaHash is null ? RechazoIngreso.CredencialesInvalidas
        : null;

    /// <summary>Contraseña equivocada: al quinto intento seguido se bloquea 15 minutos.</summary>
    public void RegistrarIntentoFallido(DateTimeOffset ahora)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= MaximoIntentos)
        {
            BloqueadoHasta = ahora.Add(DuracionBloqueo);
            IntentosFallidos = 0;
        }
    }

    /// <summary>Primer ingreso: una invitación pasa a activa.</summary>
    public void RegistrarIngreso(DateTimeOffset ahora)
    {
        UltimoIngreso = ahora;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        if (Estado == EstadoUsuario.Invitado)
        {
            Estado = EstadoUsuario.Activo;
        }
    }

    public bool VeEmpresaUsuaria(Guid empresaUsuariaId) => TodasLasEmpresasUsuarias || EmpresasUsuarias.Contains(empresaUsuariaId);

    public bool AdministraLaEmpresa => Estado != EstadoUsuario.Desactivado && MatrizPermisos.Puede(Rol, Accion.GestionarUsuarios);

    public static string NormalizarCorreo(string correo) => (correo ?? "").Trim().ToLowerInvariant();

    private static Result Validar(string correo, string nombre, Rol rol, bool todas, IEnumerable<Guid> empresas)
    {
        if (!FormatoCorreo().IsMatch(NormalizarCorreo(correo)))
        {
            return Result.Failure(UsuarioErrors.CorreoInvalido);
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Result.Failure(UsuarioErrors.NombreVacio);
        }

        if (rol == Rol.Candidato)
        {
            return Result.Failure(UsuarioErrors.CandidatoNoSeInvita);
        }

        return !todas && !empresas.Any() ? Result.Failure(UsuarioErrors.SinEmpresasUsuarias) : Result.Success();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[a-z]{2,}$")]
    private static partial Regex FormatoCorreo();
}
