using FluentValidation;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Infrastructure.Common.Cqrs;

/// <summary>Valida comandos y consultas con FluentValidation antes de llegar al handler.</summary>
public static class ValidationDecorator
{
    public sealed class CommandHandler<TCommand>(
        ICommandHandler<TCommand> inner,
        IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var error = await Validate(command, validators, cancellationToken);
            return error is null ? await inner.Handle(command, cancellationToken) : Result.Failure(error);
        }
    }

    public sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> inner,
        IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var error = await Validate(command, validators, cancellationToken);
            return error is null ? await inner.Handle(command, cancellationToken) : Result.Failure<TResponse>(error);
        }
    }

    public sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> inner,
        IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            var error = await Validate(query, validators, cancellationToken);
            return error is null ? await inner.Handle(query, cancellationToken) : Result.Failure<TResponse>(error);
        }
    }

    private static async Task<ValidationError?> Validate<T>(
        T request, IEnumerable<IValidator<T>> validators, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<T>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var errors = results
            .SelectMany(r => r.Errors)
            .Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))
            .ToList();

        return errors.Count == 0 ? null : new ValidationError(errors);
    }
}
