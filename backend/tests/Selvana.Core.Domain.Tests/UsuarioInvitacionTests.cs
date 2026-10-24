using FluentAssertions;
using Selvana.Core.Domain.Usuarios;

namespace Selvana.Core.Domain.Tests;

public sealed class UsuarioInvitacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static Usuario Invitado() => Usuario.Invitar(Guid.NewGuid(), "jorge.pena@serviciosandinos.co", "Jorge Peña", Rol.Lider, null, "Jefe de bodega",
        todasLasEmpresasUsuarias: false, [Guid.NewGuid()], Rol.Lider, "Tiene un equipo a cargo", "Laura Moreno", Ahora).Value;

    [Fact]
    public void LaInvitacionVenceALosSieteDias()
    {
        var usuario = Invitado();
        usuario.EmitirInvitacion("huella", Ahora);

        usuario.InvitacionVigente(Ahora.AddDays(6)).Should().BeTrue();
        usuario.InvitacionVigente(Ahora.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void ActivarCreaLaContrasenaYElEnlaceDejaDeServir()
    {
        var usuario = Invitado();
        usuario.EmitirInvitacion("huella", Ahora);

        usuario.ActivarCuenta("pbkdf2-sha256$1$sal$hash", Ahora.AddHours(1)).IsSuccess.Should().BeTrue();

        usuario.Estado.Should().Be(EstadoUsuario.Activo);
        usuario.InvitacionHash.Should().BeNull();
        usuario.PuedeIntentar(Ahora).Should().BeNull();
        usuario.ActivarCuenta("otra", Ahora.AddHours(2)).Error.Should().Be(UsuarioErrors.InvitacionVencida);
    }

    [Fact]
    public void ConLaInvitacionVencidaNoSeActiva()
    {
        var usuario = Invitado();
        usuario.EmitirInvitacion("huella", Ahora);

        usuario.ActivarCuenta("pbkdf2", Ahora.AddDays(10)).Error.Should().Be(UsuarioErrors.InvitacionVencida);
    }

    [Fact]
    public void QuienYaTieneContrasenaNoRecibeOtraInvitacion()
    {
        var usuario = Invitado();
        usuario.EstablecerContrasena("pbkdf2");

        usuario.EmitirInvitacion("huella", Ahora).Error.Should().Be(UsuarioErrors.YaActivo);
    }

    [Fact]
    public void SiSeCambiaElRolSugeridoQuedaRegistrado()
    {
        var usuario = Invitado();
        usuario.RolCorregido.Should().BeFalse();

        usuario.Cambiar("Jorge Peña", Rol.Analista, false, usuario.EmpresasUsuarias, activo: true);

        usuario.RolCorregido.Should().BeTrue();
    }

    [Theory]
    [InlineData("corta1", false)]
    [InlineData("solamenteletras", false)]
    [InlineData("1234567890", false)]
    [InlineData("Dosel-Selva-2026", true)]
    public void LaContrasenaNecesitaDiezCaracteresConLetrasYNumeros(string contrasena, bool aceptable)
    {
        Usuario.ContrasenaAceptable(contrasena).Should().Be(aceptable);
    }

    [Theory]
    [InlineData("Jefe de bodega", "Bodega", Rol.Lider)]
    [InlineData("Coordinadora de Gestión Humana", "Talento humano", Rol.GerenteRrhh)]
    [InlineData("Analista de selección", "Talento humano", Rol.Analista)]
    [InlineData("Psicóloga de selección", "Gestión humana", Rol.Analista)]
    [InlineData("Supervisor de turno", "Operaciones", Rol.Lider)]
    [InlineData("Auxiliar de bodega", "Bodega", Rol.Empleado)]
    public void ElRolSeSugiereSegunElCargoDeLaNomina(string cargo, string area, Rol esperado)
    {
        var sugerencia = SugeridorRol.Sugerir(cargo, area, 12);

        sugerencia.Rol.Should().Be(esperado);
        sugerencia.Evidencia.Should().Contain(cargo).And.Contain("12 personas");
    }

    [Fact]
    public void NuncaSeSugiereAdministrador()
    {
        SugeridorRol.Sugerir("Administrador de sistemas", "Tecnología", 1).Rol.Should().NotBe(Rol.Administrador);
    }
}
