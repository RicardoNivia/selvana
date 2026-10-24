using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Bandeja;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura.AprobarPerfilCargo;

/// <summary>Una persona aprueba el perfil que redactó la IA; deja de ser borrador.</summary>
public sealed record AprobarPerfilCargoCommand(Guid CargoId) : ICommand;

internal sealed class AprobarPerfilCargoHandler(IContextoUsuario usuario, IEstructuras estructuras, IBitacora bitacora, IBandeja bandeja, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<AprobarPerfilCargoCommand>
{
    public async Task<Result> Handle(AprobarPerfilCargoCommand command, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            return Result.Failure(EstructuraErrors.CargoNoExiste);
        }

        var resultado = estructura.AprobarPerfil(command.CargoId, usuario.Nombre, reloj.GetUtcNow());
        if (resultado.IsSuccess)
        {
            var cargo = estructura.Cargos.FirstOrDefault(x => x.Id == command.CargoId);
            await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó el perfil del cargo {cargo?.Nombre}",
                cargo?.ModeloIa is null ? "Perfil escrito por una persona" : $"Perfil redactado por la IA ({cargo.ModeloIa})", null, "Cargo", command.CargoId,
                cancellationToken: cancellationToken);
            await bandeja.ResolverAsync(usuario.EmpresaId, "PerfilCargo", command.CargoId, "Perfil aprobado", usuario.Nombre, cancellationToken);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }
}
