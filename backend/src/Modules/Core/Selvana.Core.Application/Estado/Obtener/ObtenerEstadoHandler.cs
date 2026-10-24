using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estado.Obtener;

internal sealed class ObtenerEstadoHandler(TimeProvider timeProvider) : IQueryHandler<ObtenerEstadoQuery, EstadoDto>
{
    public Task<Result<EstadoDto>> Handle(ObtenerEstadoQuery query, CancellationToken cancellationToken)
    {
        var version = typeof(ObtenerEstadoHandler).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        Result<EstadoDto> result = new EstadoDto("Core", version, timeProvider.GetUtcNow());
        return Task.FromResult(result);
    }
}
