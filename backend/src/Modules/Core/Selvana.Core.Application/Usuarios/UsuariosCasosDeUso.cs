using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Domain.Usuarios;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Usuarios;

/// <summary>Una política de autorización por acción de la matriz («accion:VerHojasDeVida»), con los roles que la pueden hacer.</summary>
public static class PoliticasCore
{
    public const string GestionarUsuarios = "accion:GestionarUsuarios";

    public static IReadOnlyList<(string Politica, IReadOnlyList<string> Roles)> Todas() =>
        [.. MatrizPermisos.Acciones.Select(a => ($"accion:{a}", (IReadOnlyList<string>)[.. MatrizPermisos.RolesQuePueden(a).Select(r => r.ToString())]))];
}

public static class UsuariosErrors
{
    public static readonly Error RolInvalido = Error.Validation("Usuario.RolInvalido", "Elija uno de los roles de la lista.");
    public static readonly Error NoPuedeDesactivarse = Error.Conflict("Usuario.NoPuedeDesactivarse", "No puede desactivar su propio acceso.");
    public static readonly Error EmpresaUsuariaAjena = Error.Validation("Usuario.EmpresaUsuariaAjena", "Una de las empresas usuarias elegidas no existe o está inactiva.");
    public static readonly Error CargoNoExiste = Error.NotFound("Usuario.CargoNoExiste", "Ese cargo no existe en la estructura.");
}

// ---------- Consultas ----------

public sealed record ListarUsuariosQuery(int? Pagina, int? Tamano, string? Buscar, string? Rol) : IQuery<Pagina<UsuarioDto>>;

internal sealed class ListarUsuariosHandler(IContextoUsuario usuario, ILecturaUsuarios lectura) : IQueryHandler<ListarUsuariosQuery, Pagina<UsuarioDto>>
{
    public async Task<Result<Pagina<UsuarioDto>>> Handle(ListarUsuariosQuery query, CancellationToken cancellationToken) =>
        await lectura.UsuariosAsync(usuario.EmpresaId, new PaginaSolicitada(query.Pagina, query.Tamano), query.Buscar, query.Rol, cancellationToken);
}

/// <summary>Rol sugerido para el cargo que la persona tiene en la nómina, con la razón y la evidencia.</summary>
public sealed record SugerirRolQuery(Guid CargoId) : IQuery<SugerenciaRolDto>;

internal sealed class SugerirRolHandler(IContextoUsuario usuario, ILecturaUsuarios lectura) : IQueryHandler<SugerirRolQuery, SugerenciaRolDto>
{
    public async Task<Result<SugerenciaRolDto>> Handle(SugerirRolQuery query, CancellationToken cancellationToken)
    {
        if (await lectura.CargoAsync(usuario.EmpresaId, query.CargoId, cancellationToken) is not { } cargo)
        {
            return UsuariosErrors.CargoNoExiste;
        }

        var s = SugeridorRol.Sugerir(cargo.Nombre, cargo.Area, cargo.Empleados);
        return new SugerenciaRolDto(s.Rol.ToString(), s.Razon, s.Evidencia);
    }
}

public sealed record ObtenerMatrizPermisosQuery : IQuery<MatrizPermisosDto>;

internal sealed class ObtenerMatrizPermisosHandler : IQueryHandler<ObtenerMatrizPermisosQuery, MatrizPermisosDto>
{
    private static readonly Dictionary<Accion, (string Texto, string? Nota)> Textos = new()
    {
        [Accion.AprobarBandeja] = ("Aprobar ternas y decisiones de la bandeja", null),
        [Accion.CrearVacantes] = ("Crear y publicar vacantes", null),
        [Accion.VerHojasDeVida] = ("Ver hojas de vida", null),
        [Accion.VerDatosSensibles] = ("Ver datos sensibles (salud, pruebas psicotécnicas)", "Dato sensible · cada acceso queda en la bitácora"),
        [Accion.ConfigurarEstructura] = ("Configurar estructura y conexión de nómina", null),
        [Accion.GestionarUsuarios] = ("Invitar personas y asignar roles", null),
        [Accion.ConfigurarAgentes] = ("Configurar agentes y autonomía", null),
        [Accion.VerBitacora] = ("Ver bitácora de auditoría", null),
        [Accion.GestionarEmpleados] = ("Gestionar el expediente de los empleados", null),
        [Accion.ConsultarSusDatos] = ("Consultar sus vacaciones, certificados y documentos", null),
    };

    public Task<Result<MatrizPermisosDto>> Handle(ObtenerMatrizPermisosQuery query, CancellationToken cancellationToken)
    {
        var permisos = MatrizPermisos.Acciones.Select(a => new PermisoDto(
            a.ToString(), Textos[a].Texto, Textos[a].Nota, a == Accion.VerDatosSensibles,
            [.. MatrizPermisos.Roles.Select(r => new CeldaPermisoDto(r.ToString(), MatrizPermisos.Para(r, a).ToString()))])).ToList();
        return Task.FromResult(Result.Success(new MatrizPermisosDto([.. MatrizPermisos.Roles.Select(r => r.ToString())], permisos)));
    }
}

/// <summary>Datos de una invitación para la pantalla de crear contraseña (sin sesión).</summary>
public sealed record ObtenerInvitacionQuery(string Token) : IQuery<InvitacionDto>;

internal sealed class ObtenerInvitacionHandler(ILecturaUsuarios lectura, IGeneradorTokens tokens, TimeProvider reloj) : IQueryHandler<ObtenerInvitacionQuery, InvitacionDto>
{
    public async Task<Result<InvitacionDto>> Handle(ObtenerInvitacionQuery query, CancellationToken cancellationToken) =>
        await lectura.InvitacionAsync(tokens.Huella(query.Token), reloj.GetUtcNow(), cancellationToken) is { Vigente: true } invitacion
            ? invitacion
            : UsuarioErrors.InvitacionVencida;
}

// ---------- Comandos ----------

/// <summary>Invita a una persona. Si la IA sugirió el rol y la persona eligió otro, queda registrado. Devuelve el enlace una sola vez.</summary>
public sealed record InvitarUsuarioCommand(
    string Correo, string Nombre, string Rol, Guid? CargoId, bool TodasLasEmpresasUsuarias, IReadOnlyList<Guid> EmpresasUsuarias,
    string? RolSugerido, string? RazonSugerencia) : ICommand<InvitacionEmitidaDto>;

internal sealed class InvitarUsuarioValidator : AbstractValidator<InvitarUsuarioCommand>
{
    public InvitarUsuarioValidator()
    {
        RuleFor(c => c.Correo).NotEmpty().WithMessage("Escriba el correo de trabajo.").MaximumLength(254);
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre de la persona.").MaximumLength(200);
        RuleFor(c => c.Rol).NotEmpty().WithMessage("Elija un rol.");
        RuleFor(c => c.RazonSugerencia).MaximumLength(500);
    }
}

internal sealed class InvitarUsuarioHandler(
    IContextoUsuario actual, IUsuarios usuarios, IEmpresas empresas, ILecturaUsuarios lectura, IGeneradorTokens tokens, IBitacora bitacora, IUnitOfWork unidad,
    TimeProvider reloj) : ICommandHandler<InvitarUsuarioCommand, InvitacionEmitidaDto>
{
    public async Task<Result<InvitacionEmitidaDto>> Handle(InvitarUsuarioCommand c, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Rol>(c.Rol, ignoreCase: false, out var rol))
        {
            return UsuariosErrors.RolInvalido;
        }

        if (await usuarios.ExisteCorreoAsync(actual.EmpresaId, Usuario.NormalizarCorreo(c.Correo), cancellationToken))
        {
            return UsuarioErrors.CorreoRepetido;
        }

        var validas = await EmpresasUsuariasValidas.RevisarAsync(empresas, actual.EmpresaId, c.TodasLasEmpresasUsuarias, c.EmpresasUsuarias, cancellationToken);
        if (validas.IsFailure)
        {
            return validas.Error;
        }

        string? cargoNombre = null;
        if (c.CargoId is { } cargoId)
        {
            cargoNombre = (await lectura.CargoAsync(actual.EmpresaId, cargoId, cancellationToken))?.Nombre;
            if (cargoNombre is null)
            {
                return UsuariosErrors.CargoNoExiste;
            }
        }

        Rol? sugerido = Enum.TryParse<Rol>(c.RolSugerido, ignoreCase: false, out var s) ? s : null;
        var ahora = reloj.GetUtcNow();
        var invitado = Usuario.Invitar(actual.EmpresaId, c.Correo, c.Nombre, rol, c.CargoId, cargoNombre, c.TodasLasEmpresasUsuarias,
            c.EmpresasUsuarias, sugerido, sugerido is null ? null : c.RazonSugerencia, actual.Nombre, ahora);
        if (invitado.IsFailure)
        {
            return invitado.Error;
        }

        var (token, huella) = tokens.Generar();
        invitado.Value.EmitirInvitacion(huella, ahora);
        usuarios.Agregar(invitado.Value);
        await bitacora.RegistrarAsync(actual.EmpresaId, TipoActor.Persona, actual.Nombre, $"Invitó a {invitado.Value.Nombre} como {rol}",
            $"{invitado.Value.Correo}{(cargoNombre is null ? "" : $" · {cargoNombre}")} · {(c.TodasLasEmpresasUsuarias ? "todas las empresas usuarias" : $"{c.EmpresasUsuarias.Count} empresas usuarias")}",
            sugerido is null ? null : sugerido == rol ? $"Aceptó el rol que sugirió Selvana ({sugerido})" : $"Cambió el rol que sugirió Selvana ({sugerido} → {rol})",
            "Usuario", invitado.Value.Id, cancellationToken: cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return new InvitacionEmitidaDto(invitado.Value.Id, token, invitado.Value.InvitacionVence!.Value);
    }
}

/// <summary>Cambia rol, empresas usuarias o activa/desactiva. Nunca deja a la empresa sin quien administre usuarios.</summary>
public sealed record CambiarUsuarioCommand(
    Guid UsuarioId, string Nombre, string Rol, bool TodasLasEmpresasUsuarias, IReadOnlyList<Guid> EmpresasUsuarias, bool Activo) : ICommand;

internal sealed class CambiarUsuarioHandler(IContextoUsuario actual, IUsuarios usuarios, IEmpresas empresas, IBitacora bitacora, IUnitOfWork unidad)
    : ICommandHandler<CambiarUsuarioCommand>
{
    public async Task<Result> Handle(CambiarUsuarioCommand c, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Rol>(c.Rol, ignoreCase: false, out var rol))
        {
            return Result.Failure(UsuariosErrors.RolInvalido);
        }

        var usuario = await usuarios.ObtenerAsync(actual.EmpresaId, c.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return Result.Failure(UsuarioErrors.NoExiste);
        }

        if (!c.Activo && usuario.Id == actual.UsuarioId)
        {
            return Result.Failure(UsuariosErrors.NoPuedeDesactivarse);
        }

        var validas = await EmpresasUsuariasValidas.RevisarAsync(empresas, actual.EmpresaId, c.TodasLasEmpresasUsuarias, c.EmpresasUsuarias, cancellationToken);
        if (validas.IsFailure)
        {
            return validas;
        }

        var administrabaAntes = usuario.AdministraLaEmpresa;
        var rolAntes = usuario.Rol;
        var estadoAntes = usuario.Estado;
        var cambio = usuario.Cambiar(c.Nombre, rol, c.TodasLasEmpresasUsuarias, c.EmpresasUsuarias, c.Activo);
        if (cambio.IsFailure)
        {
            return cambio;
        }

        if (administrabaAntes && !usuario.AdministraLaEmpresa
            && await usuarios.ContarAdministradoresAsync(actual.EmpresaId, excepto: usuario.Id, cancellationToken) == 0)
        {
            return Result.Failure(UsuarioErrors.UltimoAdministrador);
        }

        var cambios = new List<string>();
        if (rolAntes != usuario.Rol) cambios.Add($"rol {rolAntes} → {usuario.Rol}");
        if (estadoAntes != usuario.Estado) cambios.Add(usuario.Estado == EstadoUsuario.Desactivado ? "desactivó el acceso" : "activó el acceso");
        cambios.Add(c.TodasLasEmpresasUsuarias ? "todas las empresas usuarias" : $"{c.EmpresasUsuarias.Count} empresas usuarias");
        await bitacora.RegistrarAsync(actual.EmpresaId, TipoActor.Persona, actual.Nombre, $"Cambió el acceso de {usuario.Nombre}", string.Join(" · ", cambios),
            usuario.RolCorregido && rolAntes != usuario.Rol ? $"Distinto del rol que sugirió Selvana ({usuario.RolSugeridoIa})" : null,
            "Usuario", usuario.Id, cancellationToken: cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Nuevo enlace de invitación (el anterior deja de servir). Para quien perdió el enlace o lo dejó vencer.</summary>
public sealed record RenovarInvitacionCommand(Guid UsuarioId) : ICommand<InvitacionEmitidaDto>;

internal sealed class RenovarInvitacionHandler(IContextoUsuario actual, IUsuarios usuarios, IGeneradorTokens tokens, IBitacora bitacora, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<RenovarInvitacionCommand, InvitacionEmitidaDto>
{
    public async Task<Result<InvitacionEmitidaDto>> Handle(RenovarInvitacionCommand c, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObtenerAsync(actual.EmpresaId, c.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return UsuarioErrors.NoExiste;
        }

        var (token, huella) = tokens.Generar();
        var emitida = usuario.EmitirInvitacion(huella, reloj.GetUtcNow());
        if (emitida.IsFailure)
        {
            return emitida.Error;
        }

        await bitacora.RegistrarAsync(actual.EmpresaId, TipoActor.Persona, actual.Nombre, $"Generó un nuevo enlace de invitación para {usuario.Nombre}",
            "El enlace anterior dejó de servir", null, "Usuario", usuario.Id, cancellationToken: cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return new InvitacionEmitidaDto(usuario.Id, token, usuario.InvitacionVence!.Value);
    }
}

/// <summary>Quien recibió la invitación crea su contraseña (sin sesión). El enlace deja de servir.</summary>
public sealed record ActivarCuentaCommand(string Token, string Contrasena) : ICommand
{
    public override string ToString() => "ActivarCuentaCommand { }";
}

internal sealed class ActivarCuentaValidator : AbstractValidator<ActivarCuentaCommand>
{
    public ActivarCuentaValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Contrasena).Must(Usuario.ContrasenaAceptable).WithMessage(UsuarioErrors.ContrasenaDebil.Description);
    }
}

internal sealed class ActivarCuentaHandler(IUsuarios usuarios, IGeneradorTokens tokens, IHasherContrasenas hasher, IBitacora bitacora, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<ActivarCuentaCommand>
{
    public async Task<Result> Handle(ActivarCuentaCommand c, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.PorInvitacionAsync(tokens.Huella(c.Token), cancellationToken);
        if (usuario is null)
        {
            return Result.Failure(UsuarioErrors.InvitacionVencida);
        }

        var activada = usuario.ActivarCuenta(hasher.Calcular(c.Contrasena), reloj.GetUtcNow());
        if (activada.IsSuccess)
        {
            await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, "Aceptó la invitación y creó su contraseña", usuario.Correo, null,
                "Usuario", usuario.Id, cancellationToken: cancellationToken);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return activada;
    }
}

internal static class EmpresasUsuariasValidas
{
    /// <summary>Las empresas usuarias asignadas existen, están activas y son de la empresa del usuario.</summary>
    public static async Task<Result> RevisarAsync(IEmpresas empresas, Guid empresaId, bool todas, IReadOnlyList<Guid> elegidas, CancellationToken cancellationToken)
    {
        if (todas || elegidas.Count == 0)
        {
            return Result.Success();
        }

        var empresa = await empresas.ObtenerAsync(empresaId, cancellationToken);
        var activas = empresa?.EmpresasUsuarias.Where(e => e.Activa).Select(e => e.Id).ToHashSet() ?? [];
        return elegidas.All(activas.Contains) ? Result.Success() : Result.Failure(UsuariosErrors.EmpresaUsuariaAjena);
    }
}
