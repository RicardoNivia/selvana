using FluentAssertions;
using Selvana.Core.Domain.Empresas;
using Selvana.Core.Domain.Usuarios;

namespace Selvana.Core.Domain.Tests;

public class RegistroTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 30, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LaEmpresaQueSeRegistraArrancaConTreintaDiasDePrueba()
    {
        var e = Empresa.Registrar(Guid.NewGuid(), " Andina de Servicios ", "900.412.887", true, "Servicios temporales", "51 a 200", Ahora).Value;

        e.Nombre.Should().Be("Andina de Servicios");
        e.Nit.Should().Be("900412887-9");
        e.PruebaHasta.Should().Be(Ahora.AddDays(30));
        e.DiasDePrueba(Ahora).Should().Be(30);
        e.DiasDePrueba(Ahora.AddDays(29.5)).Should().Be(1);
        e.DiasDePrueba(Ahora.AddDays(45)).Should().Be(0);
    }

    [Fact]
    public void ElNitDebeSerValido() =>
        Empresa.Registrar(Guid.NewGuid(), "Andina", "900412887-1", false, "", "", Ahora).Error.Should().Be(EmpresaErrors.NitInvalido);

    [Fact]
    public void LasEmpresasDeEjemploNoEstanEnPrueba() =>
        Empresa.Crear(Guid.NewGuid(), "Andina", "900412887", false).Value.DiasDePrueba(Ahora).Should().BeNull();

    [Fact]
    public void QuienRegistraQuedaComoAdministradorActivo()
    {
        var u = Usuario.RegistrarDueno(Guid.NewGuid(), "Laura@Andina.co", "Laura Moreno", "hash", Ahora).Value;

        u.Rol.Should().Be(Rol.Administrador);
        u.Estado.Should().Be(EstadoUsuario.Activo);
        u.Correo.Should().Be("laura@andina.co");
        u.ContrasenaHash.Should().Be("hash");
        u.TodasLasEmpresasUsuarias.Should().BeTrue();
    }
}
