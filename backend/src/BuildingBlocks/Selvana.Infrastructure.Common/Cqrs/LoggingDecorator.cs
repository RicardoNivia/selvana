using Microsoft.Extensions.Logging;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Infrastructure.Common.Cqrs;

/// <summary>Registra inicio, fin y errores de cada comando y consulta.</summary>
public static partial class LoggingDecorator
{
    public sealed class CommandHandler<TCommand>(
        ICommandHandler<TCommand> inner,
        ILogger<CommandHandler<TCommand>> logger) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            Procesando(logger, typeof(TCommand).Name);
            var result = await inner.Handle(command, cancellationToken);
            Registrar(logger, typeof(TCommand).Name, result);
            return result;
        }
    }

    public sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> inner,
        ILogger<CommandHandler<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            Procesando(logger, typeof(TCommand).Name);
            var result = await inner.Handle(command, cancellationToken);
            Registrar(logger, typeof(TCommand).Name, result);
            return result;
        }
    }

    public sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> inner,
        ILogger<QueryHandler<TQuery, TResponse>> logger) : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            Procesando(logger, typeof(TQuery).Name);
            var result = await inner.Handle(query, cancellationToken);
            Registrar(logger, typeof(TQuery).Name, result);
            return result;
        }
    }

    private static void Registrar(ILogger logger, string nombre, Result result)
    {
        if (result.IsSuccess)
        {
            Completado(logger, nombre);
        }
        else
        {
            Fallido(logger, nombre, result.Error.Code, result.Error.Description);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Procesando {Solicitud}")]
    private static partial void Procesando(ILogger logger, string solicitud);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completado {Solicitud}")]
    private static partial void Completado(ILogger logger, string solicitud);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falló {Solicitud}: {Codigo} {Descripcion}")]
    private static partial void Fallido(ILogger logger, string solicitud, string codigo, string descripcion);
}
