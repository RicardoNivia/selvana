using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace Selvana.ArchitectureTests;

/// <summary>Regla de dependencias de docs/arquitectura/backend.md, sección 1.</summary>
public class CapasTests
{
    public static TheoryData<string> Modulos => new() { "Core", "Reclutamiento", "Vinculacion", "Expediente", "Desempeno", "Clima" };

    [Theory]
    [MemberData(nameof(Modulos))]
    public void DomainNoDependeDeOtrasCapasNiFrameworks(string modulo)
    {
        var result = Types.InAssembly(Ensamblado(modulo, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"Selvana.{modulo}.Application",
                $"Selvana.{modulo}.Infrastructure",
                $"Selvana.{modulo}.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void ApplicationNoDependeDeInfrastructureNiApi(string modulo)
    {
        var result = Types.InAssembly(Ensamblado(modulo, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"Selvana.{modulo}.Infrastructure",
                $"Selvana.{modulo}.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void InfrastructureNoDependeDeApi(string modulo)
    {
        var result = Types.InAssembly(Ensamblado(modulo, "Infrastructure"))
            .ShouldNot()
            .HaveDependencyOn($"Selvana.{modulo}.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void ApiNoDependeDelDominioNiDeInfrastructure(string modulo)
    {
        var result = Types.InAssembly(Ensamblado(modulo, "Api"))
            .ShouldNot()
            .HaveDependencyOnAny($"Selvana.{modulo}.Domain", $"Selvana.{modulo}.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void ContractsSoloDependeDelSharedKernel(string modulo)
    {
        var result = Types.InAssembly(Ensamblado(modulo, "Contracts"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"Selvana.{modulo}.Domain",
                $"Selvana.{modulo}.Application",
                $"Selvana.{modulo}.Infrastructure",
                $"Selvana.{modulo}.Api",
                "Selvana.Infrastructure.Common",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Fact]
    public void SharedKernelNoDependeDeNadieDelProducto()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Results.Result).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Selvana.Infrastructure", "Selvana.Core", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Fact]
    public void LosContratosDeLaIaSoloDependenDelSharedKernel()
    {
        var result = Types.InAssembly(Assembly.Load("Selvana.AI.Contracts"))
            .ShouldNot()
            .HaveDependencyOnAny("Selvana.AI.Gateway", "Selvana.AI.Agentes", "Selvana.Core", "Selvana.Infrastructure", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    internal static Assembly Ensamblado(string modulo, string capa) => Assembly.Load($"Selvana.{modulo}.{capa}");

    internal static string Fallas(TestResult result) =>
        "tipos que violan la regla: " + string.Join(", ", result.FailingTypeNames ?? []);
}
