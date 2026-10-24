using FluentAssertions;
using NetArchTest.Rules;

namespace Selvana.ArchitectureTests;

/// <summary>Los módulos solo se ven entre sí a través de sus Contracts (CLAUDE.md, regla 5).</summary>
public class ModulosTests
{
    // Agregar aquí cada módulo nuevo: Vinculacion, Expediente, Desempeno, Formacion, Clima
    private static readonly string[] Modulos = ["Core", "Reclutamiento", "Vinculacion"];
    private static readonly string[] Capas = ["Domain", "Application", "Infrastructure", "Api"];

    [Fact]
    public void NingunModuloReferenciaInternosDeOtro()
    {
        foreach (var modulo in Modulos)
        {
            var internosDeOtros = Modulos
                .Where(m => m != modulo)
                .SelectMany(m => Capas.Select(c => $"Selvana.{m}.{c}"))
                .ToArray();

            if (internosDeOtros.Length == 0)
            {
                continue;
            }

            foreach (var capa in Capas)
            {
                var result = Types.InAssembly(CapasTests.Ensamblado(modulo, capa))
                    .ShouldNot()
                    .HaveDependencyOnAny(internosDeOtros)
                    .GetResult();

                result.IsSuccessful.Should().BeTrue(CapasTests.Fallas(result));
            }
        }
    }
}
