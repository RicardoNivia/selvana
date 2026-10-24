using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura.CambiarPropuesta;

/// <summary>Incluye o excluye una propuesta antes de aplicar la importación.</summary>
public sealed record CambiarPropuestaCommand(Guid ImportacionId, Guid PropuestaId, bool Incluida) : ICommand;

internal sealed class CambiarPropuestaHandler(IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad)
    : ICommandHandler<CambiarPropuestaCommand>
{
    public async Task<Result> Handle(CambiarPropuestaCommand command, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            return Result.Failure(EstructuraErrors.ImportacionNoExiste);
        }

        var resultado = estructura.CambiarInclusion(command.ImportacionId, command.PropuestaId, command.Incluida);
        if (resultado.IsSuccess)
        {
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }
}
