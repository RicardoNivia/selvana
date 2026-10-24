using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Infrastructure.Common.Cqrs;

public static class CqrsServiceCollectionExtensions
{
    /// <summary>
    /// Registra handlers y validadores de los ensamblados de Application y aplica los decoradores.
    /// El último decorador registrado es el más externo: Logging → Validation → Transaction → handler.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime());

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        services.TryDecorate(typeof(ICommandHandler<>), typeof(TransactionDecorator.CommandHandler<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(TransactionDecorator.CommandHandler<,>));

        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationDecorator.CommandHandler<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.QueryHandler<,>));

        services.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandHandler<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));

        return services;
    }
}
