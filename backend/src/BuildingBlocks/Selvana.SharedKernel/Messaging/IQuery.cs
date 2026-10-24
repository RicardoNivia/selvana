using Selvana.SharedKernel.Results;

namespace Selvana.SharedKernel.Messaging;

/// <summary>Consulta de solo lectura que proyecta a un DTO.</summary>
public interface IQuery<TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
