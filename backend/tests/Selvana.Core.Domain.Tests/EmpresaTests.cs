using FluentAssertions;
using Selvana.Core.Domain.Empresas;

namespace Selvana.Core.Domain.Tests;

public sealed class EmpresaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("890903938", "890903938-8")]
    [InlineData("890.903.938-8", "890903938-8")]
    [InlineData("900412887", "900412887-9")]
    public void ElNitSeNormalizaConElDigitoDeVerificacionDeLaDian(string nit, string esperado)
    {
        Empresa.NormalizarNit(nit).Should().Be(esperado);
    }

    [Theory]
    [InlineData("890903938-1")]
    [InlineData("12345")]
    [InlineData("")]
    public void UnNitConDigitoEquivocadoOIncompletoNoEsValido(string nit)
    {
        Empresa.NormalizarNit(nit).Should().BeNull();
    }

    [Fact]
    public void SoloUnaEmpresaTemporalManejaEmpresasUsuarias()
    {
        var corporativa = Empresa.Crear(Guid.NewGuid(), "Alimentos La Sabana", "890903938", esTemporal: false).Value;

        corporativa.AgregarEmpresaUsuaria("Cliente", "900412887", "Retail", "Bogotá", Ahora).Error.Should().Be(EmpresaErrors.NoEsTemporal);
    }

    [Fact]
    public void NoSeRepiteElNitDeUnaEmpresaUsuaria()
    {
        var est = Empresa.Crear(Guid.NewGuid(), "Servicios Andinos EST", "890903938", esTemporal: true).Value;
        est.AgregarEmpresaUsuaria("Contact Center Norte", "900412887", "BPO", "Bogotá", Ahora).IsSuccess.Should().BeTrue();

        est.AgregarEmpresaUsuaria("Otra", "900.412.887-9", "BPO", "Cali", Ahora).Error.Should().Be(EmpresaErrors.NitRepetido);
        est.EmpresasUsuarias.Should().ContainSingle();
    }
}
