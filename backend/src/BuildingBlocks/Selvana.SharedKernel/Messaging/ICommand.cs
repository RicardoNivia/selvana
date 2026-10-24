using Selvana.SharedKernel.Results;

namespace Selvana.SharedKernel.Messaging;

public interface IBaseCommand;

/// <summary>Comando sin valor de retorno.</summary>
public interface ICommand : IBaseCommand;

/// <summary>Comando que devuelve un valor, por ejemplo el Id creado.</summary>
public interface ICommand<TResponse> : IBaseCommand;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
