using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Domain.Usuarios;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Sesion;

/// <summary>
/// Quién está en la consola, en qué empresa trabaja, qué empresas usuarias ve y qué acciones puede hacer
/// (las de la matriz de permisos con cualquier alcance).
/// </summary>
public sealed record SesionDto(
    Guid UsuarioId, string Nombre, string Correo, string Rol, Guid EmpresaId, string EmpresaNombre, bool EmpresaTemporal,
    bool TodasLasEmpresasUsuarias, IReadOnlyList<Usuarios.EmpresaUsuariaResumenDto> EmpresasUsuarias, IReadOnlyList<string> Acciones);

public enum MotivoRechazo
{
    CredencialesInvalidas,
    Bloqueado,
    Desactivado,
}

/// <summary>Un intento de ingreso siempre queda registrado; si no entra, dice por qué (sin revelar si el correo existe).</summary>
public sealed record ResultadoIngreso(SesionDto? Sesion, MotivoRechazo? Motivo, string? Mensaje)
{
    internal static ResultadoIngreso Rechazado(RechazoIngreso rechazo, DateTimeOffset? bloqueadoHasta, DateTimeOffset ahora) => rechazo switch
    {
        RechazoIngreso.Bloqueado => new(null, MotivoRechazo.Bloqueado,
            $"Por seguridad bloqueamos el ingreso tras {Usuario.MaximoIntentos} intentos fallidos. Intente de nuevo en " +
            $"{Math.Max(1, (int)Math.Ceiling(((bloqueadoHasta ?? ahora) - ahora).TotalMinutes))} minutos."),
        RechazoIngreso.Desactivado => new(null, MotivoRechazo.Desactivado, UsuarioErrors.Desactivado.Description),
        _ => new(null, MotivoRechazo.CredencialesInvalidas, UsuarioErrors.CredencialesInvalidas.Description),
    };
}

/// <summary>Ingreso con correo y contraseña. Devuelve éxito aunque se rechace, para que el intento fallido se guarde.</summary>
public sealed record IngresarCommand(string Correo, string Contrasena) : ICommand<ResultadoIngreso>
{
    // La contraseña nunca aparece en logs ni trazas
    public override string ToString() => $"IngresarCommand {{ Correo = {Correo} }}";
}

internal sealed class IngresarValidator : AbstractValidator<IngresarCommand>
{
    public IngresarValidator()
    {
        RuleFor(c => c.Correo).NotEmpty().WithMessage("Escriba su correo de trabajo.").MaximumLength(254);
        RuleFor(c => c.Contrasena).NotEmpty().WithMessage("Escriba su contraseña.").MaximumLength(200);
    }
}

internal sealed class IngresarHandler(IUsuarios usuarios, IHasherContrasenas hasher, ILecturaSesion lectura, IBitacora bitacora, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<IngresarCommand, ResultadoIngreso>
{
    public async Task<Result<ResultadoIngreso>> Handle(IngresarCommand command, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var candidatos = await usuarios.PorCorreoAsync(Usuario.NormalizarCorreo(command.Correo), cancellationToken);
        if (candidatos.Count == 0)
        {
            // Mismo costo que una verificación real: el tiempo de respuesta no revela si el correo existe
            hasher.Verificar(null, command.Contrasena);
            return ResultadoIngreso.Rechazado(RechazoIngreso.CredencialesInvalidas, null, ahora);
        }

        var rechazo = RechazoIngreso.CredencialesInvalidas;
        DateTimeOffset? bloqueadoHasta = null;
        foreach (var usuario in candidatos)
        {
            var impedimento = usuario.PuedeIntentar(ahora);
            if (impedimento is not null)
            {
                hasher.Verificar(null, command.Contrasena);
                if (impedimento != RechazoIngreso.CredencialesInvalidas)
                {
                    rechazo = impedimento.Value;
                    bloqueadoHasta = usuario.BloqueadoHasta;
                }

                continue;
            }

            if (hasher.Verificar(usuario.ContrasenaHash, command.Contrasena))
            {
                usuario.RegistrarIngreso(ahora);
                await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, "Ingresó a la consola", usuario.Correo, null, "Usuario", usuario.Id,
                    cancellationToken: cancellationToken);
                await unidad.SaveChangesAsync(cancellationToken);
                return new ResultadoIngreso(await lectura.ObtenerAsync(usuario.Id, cancellationToken), null, null);
            }

            usuario.RegistrarIntentoFallido(ahora);
            if (usuario.EstaBloqueado(ahora))
            {
                await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Sistema, "Selvana", $"Bloqueó el ingreso de {usuario.Nombre} por {Usuario.MaximoIntentos} intentos fallidos",
                    $"{usuario.Correo} · bloqueado 15 minutos", "Si no fue esa persona, revise quién intenta entrar", "Usuario", usuario.Id, cancellationToken: cancellationToken);
                rechazo = RechazoIngreso.Bloqueado;
                bloqueadoHasta = usuario.BloqueadoHasta;
            }
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return ResultadoIngreso.Rechazado(rechazo, bloqueadoHasta, ahora);
    }
}

/// <summary>La sesión de quien hace la petición.</summary>
public sealed record ObtenerSesionQuery : IQuery<SesionDto>;

internal sealed class ObtenerSesionHandler(IContextoUsuario usuario, ILecturaSesion lectura) : IQueryHandler<ObtenerSesionQuery, SesionDto>
{
    public async Task<Result<SesionDto>> Handle(ObtenerSesionQuery query, CancellationToken cancellationToken) =>
        usuario.UsuarioId is { } id && await lectura.ObtenerAsync(id, cancellationToken) is { } sesion
            ? sesion
            : UsuarioErrors.NoExiste;
}
