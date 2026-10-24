using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Infrastructure.Common.Cqrs;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.SharedKernel.Tests.Cqrs;

public class CqrsPipelineTests
{
    [Fact]
    public async Task ComandoValidoLlegaAlHandler()
    {
        using var provider = CrearProveedor();
        var handler = provider.GetRequiredService<ICommandHandler<CrearPruebaCommand, Guid>>();

        var result = await handler.Handle(new CrearPruebaCommand("Auxiliar de bodega"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComandoInvalidoDevuelveErrorDeValidacionSinLlegarAlHandler()
    {
        using var provider = CrearProveedor();
        var handler = provider.GetRequiredService<ICommandHandler<CrearPruebaCommand, Guid>>();

        var result = await handler.Handle(new CrearPruebaCommand(""), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>()
            .Which.Errors.Should().ContainSingle(e => e.Code == nameof(CrearPruebaCommand.Nombre));
    }

    [Fact]
    public async Task ConsultaPasaPorLosDecoradores()
    {
        using var provider = CrearProveedor();
        var handler = provider.GetRequiredService<IQueryHandler<ObtenerPruebaQuery, string>>();

        var result = await handler.Handle(new ObtenerPruebaQuery(), CancellationToken.None);

        result.Value.Should().Be("ok");
        handler.GetType().Should().Be<LoggingDecorator.QueryHandler<ObtenerPruebaQuery, string>>();
    }

    [Fact]
    public void LosDecoradoresSeAplicanEnOrden()
    {
        using var provider = CrearProveedor();

        var handler = provider.GetRequiredService<ICommandHandler<CrearPruebaCommand, Guid>>();

        handler.GetType().GetGenericTypeDefinition().Should().Be(typeof(LoggingDecorator.CommandHandler<,>));
    }

    private static ServiceProvider CrearProveedor() => new ServiceCollection()
        .AddLogging()
        .AddCqrs(typeof(CqrsPipelineTests).Assembly)
        .BuildServiceProvider();
}

public sealed record CrearPruebaCommand(string Nombre) : ICommand<Guid>;

internal sealed class CrearPruebaValidator : AbstractValidator<CrearPruebaCommand>
{
    public CrearPruebaValidator() => RuleFor(c => c.Nombre).NotEmpty();
}

internal sealed class CrearPruebaHandler : ICommandHandler<CrearPruebaCommand, Guid>
{
    public Task<Result<Guid>> Handle(CrearPruebaCommand command, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Guid>>(Guid.NewGuid());
}

public sealed record ObtenerPruebaQuery : IQuery<string>;

internal sealed class ObtenerPruebaHandler : IQueryHandler<ObtenerPruebaQuery, string>
{
    public Task<Result<string>> Handle(ObtenerPruebaQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<string>>("ok");
}
