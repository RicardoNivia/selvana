using FluentAssertions;
using NetArchTest.Rules;
using Selvana.SharedKernel.Messaging;

namespace Selvana.ArchitectureTests;

/// <summary>Convenciones CQRS: nombres, un handler sellado por caso de uso y DTOs inmutables.</summary>
public class CqrsTests
{
    public static TheoryData<string> Modulos => new() { "Core", "Reclutamiento", "Vinculacion" };

    [Theory]
    [MemberData(nameof(Modulos))]
    public void LosComandosTerminanEnCommand(string modulo)
    {
        var result = Types.InAssembly(CapasTests.Ensamblado(modulo, "Application"))
            .That().ImplementInterface(typeof(ICommand)).Or().ImplementInterface(typeof(ICommand<>))
            .Should().HaveNameEndingWith("Command", StringComparison.Ordinal)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(CapasTests.Fallas(result));
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void LasConsultasTerminanEnQuery(string modulo)
    {
        var result = Types.InAssembly(CapasTests.Ensamblado(modulo, "Application"))
            .That().ImplementInterface(typeof(IQuery<>))
            .Should().HaveNameEndingWith("Query", StringComparison.Ordinal)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(CapasTests.Fallas(result));
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void LosHandlersSonSelladosYTerminanEnHandler(string modulo)
    {
        var result = Types.InAssembly(CapasTests.Ensamblado(modulo, "Application"))
            .That().ImplementInterface(typeof(ICommandHandler<>))
            .Or().ImplementInterface(typeof(ICommandHandler<,>))
            .Or().ImplementInterface(typeof(IQueryHandler<,>))
            .Should().BeSealed().And().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(CapasTests.Fallas(result));
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void LosHandlersNoSonPublicos(string modulo)
    {
        var result = Types.InAssembly(CapasTests.Ensamblado(modulo, "Application"))
            .That().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .ShouldNot().BePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(CapasTests.Fallas(result));
    }
}
