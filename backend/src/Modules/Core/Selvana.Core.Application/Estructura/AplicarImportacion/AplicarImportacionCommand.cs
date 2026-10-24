using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Bandeja;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Estructura;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura.AplicarImportacion;

/// <summary>Aplica la importación aprobada y pide a la IA el perfil de los cargos que no lo tienen.</summary>
public sealed record AplicarImportacionCommand(Guid ImportacionId) : ICommand;

internal sealed class AplicarImportacionHandler(
    IContextoUsuario usuario, IEstructuras estructuras, IEventosSalientes eventos, IBitacora bitacora, IBandeja bandeja, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<AplicarImportacionCommand>
{
    public async Task<Result> Handle(AplicarImportacionCommand command, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            return Result.Failure(EstructuraErrors.ImportacionNoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var resultado = estructura.AplicarImportacion(command.ImportacionId, usuario.Nombre, ahora);
        if (resultado.IsFailure)
        {
            return Result.Failure(resultado.Error);
        }

        if (resultado.Value.Count > 0)
        {
            eventos.Publicar(new PerfilesCargoSolicitados(Guid.CreateVersion7(), ahora, usuario.EmpresaId,
                [.. resultado.Value.Select(c => new CargoParaPerfil(c.Id, c.Nombre, c.Area, c.Empleados, c.NombresNomina))]));
        }

        await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, "Aplicó la importación de la nómina",
            $"{estructura.Cargos.Count} cargos en la estructura · {resultado.Value.Count} perfiles pedidos a la IA", "Revisó y aprobó lo que propuso la IA antes de aplicar",
            "ImportacionNomina", command.ImportacionId, cancellationToken: cancellationToken);
        await bandeja.ResolverAsync(usuario.EmpresaId, "UnionesCargos", command.ImportacionId, "Importación aplicada", usuario.Nombre, cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
