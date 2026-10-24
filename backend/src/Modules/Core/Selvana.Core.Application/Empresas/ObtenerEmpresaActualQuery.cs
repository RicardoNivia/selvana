using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Contracts.Empresas;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Empresas;

/// <summary>La empresa del usuario con sus empresas usuarias (para elegir a cuál pertenece una vacante).</summary>
public sealed record ObtenerEmpresaActualQuery : IQuery<EmpresaResumen>;

internal sealed class ObtenerEmpresaActualHandler(IContextoUsuario usuario, IDirectorioEmpresas directorio)
    : IQueryHandler<ObtenerEmpresaActualQuery, EmpresaResumen>
{
    public async Task<Result<EmpresaResumen>> Handle(ObtenerEmpresaActualQuery query, CancellationToken cancellationToken) =>
        await directorio.ObtenerAsync(usuario.EmpresaId, cancellationToken) is { } empresa
            ? empresa
            : Error.NotFound("Empresa.NoExiste", "La empresa no existe.");
}
