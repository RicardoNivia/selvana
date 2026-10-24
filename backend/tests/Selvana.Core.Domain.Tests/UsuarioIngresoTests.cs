using FluentAssertions;
using Selvana.Core.Domain.Usuarios;

namespace Selvana.Core.Domain.Tests;

public sealed class UsuarioIngresoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static Usuario Laura(bool conContrasena = true)
    {
        var usuario = Usuario.Invitar(Guid.NewGuid(), " Laura.Moreno@ServiciosAndinos.co ", "Laura Moreno", Rol.GerenteRrhh, null, null,
            todasLasEmpresasUsuarias: true, [], null, null, "Sistema", Ahora).Value;
        if (conContrasena)
        {
            usuario.EstablecerContrasena("pbkdf2-sha256$600000$sal$hash");
        }

        return usuario;
    }

    [Fact]
    public void ElCorreoSeGuardaNormalizado()
    {
        Laura().Correo.Should().Be("laura.moreno@serviciosandinos.co");
    }

    [Fact]
    public void SinContrasenaNoSePuedeIngresarConCorreo()
    {
        Laura(conContrasena: false).PuedeIntentar(Ahora).Should().Be(RechazoIngreso.CredencialesInvalidas);
        Laura().PuedeIntentar(Ahora).Should().BeNull();
    }

    [Fact]
    public void AlQuintoIntentoFallidoSeBloqueaQuinceMinutos()
    {
        var usuario = Laura();
        for (var i = 0; i < Usuario.MaximoIntentos - 1; i++)
        {
            usuario.RegistrarIntentoFallido(Ahora);
        }

        usuario.EstaBloqueado(Ahora).Should().BeFalse();
        usuario.RegistrarIntentoFallido(Ahora);

        usuario.PuedeIntentar(Ahora.AddMinutes(14)).Should().Be(RechazoIngreso.Bloqueado);
        usuario.PuedeIntentar(Ahora.AddMinutes(16)).Should().BeNull();
    }

    [Fact]
    public void IngresarActivaLaInvitacionYReiniciaLosIntentos()
    {
        var usuario = Laura();
        usuario.RegistrarIntentoFallido(Ahora);

        usuario.RegistrarIngreso(Ahora);

        usuario.Estado.Should().Be(EstadoUsuario.Activo);
        usuario.IntentosFallidos.Should().Be(0);
        usuario.UltimoIngreso.Should().Be(Ahora);
    }

    [Fact]
    public void UnUsuarioDesactivadoNoPuedeIngresar()
    {
        var usuario = Laura();
        usuario.Cambiar("Laura Moreno", Rol.GerenteRrhh, true, [], activo: false);

        usuario.PuedeIntentar(Ahora).Should().Be(RechazoIngreso.Desactivado);
    }
}
