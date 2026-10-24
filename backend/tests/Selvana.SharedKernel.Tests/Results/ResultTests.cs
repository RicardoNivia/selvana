using FluentAssertions;
using Selvana.SharedKernel.Results;

namespace Selvana.SharedKernel.Tests.Results;

public class ResultTests
{
    [Fact]
    public void SuccessNoTieneError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void FailureConservaElError()
    {
        var error = Error.NotFound("Vacante.NoExiste", "La vacante no existe.");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void ResultadoConValorSeConstruyeImplicitamente()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void LeerElValorDeUnFalloLanzaExcepcion()
    {
        Result<int> result = Error.Conflict("Vacante.YaPublicada", "La vacante ya fue publicada.");

        var leer = () => result.Value;

        leer.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValidationErrorEsDeTipoValidacion()
    {
        var error = new ValidationError([Error.Validation("Nombre", "Es obligatorio.")]);

        error.Type.Should().Be(ErrorType.Validation);
        error.Errors.Should().ContainSingle();
    }
}
