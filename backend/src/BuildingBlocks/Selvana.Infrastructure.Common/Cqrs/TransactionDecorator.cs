using System.Transactions;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Infrastructure.Common.Cqrs;

/// <summary>Envuelve cada comando en una transacción; se confirma solo si el resultado es exitoso.</summary>
public static class TransactionDecorator
{
    public sealed class CommandHandler<TCommand>(ICommandHandler<TCommand> inner) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            using var scope = NuevaTransaccion();
            var result = await inner.Handle(command, cancellationToken);
            if (result.IsSuccess)
            {
                scope.Complete();
            }

            return result;
        }
    }

    public sealed class CommandHandler<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> inner)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            using var scope = NuevaTransaccion();
            var result = await inner.Handle(command, cancellationToken);
            if (result.IsSuccess)
            {
                scope.Complete();
            }

            return result;
        }
    }

    private static TransactionScope NuevaTransaccion() => new(
        TransactionScopeOption.Required,
        new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
        TransactionScopeAsyncFlowOption.Enabled);
}
