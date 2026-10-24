using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Selvana.Web.Common;

/// <summary>Sesión de la consola: cookie HttpOnly emitida por la API al ingresar con correo y contraseña.</summary>
public static class Sesiones
{
    public const string Esquema = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string NombreCookie = "selvana.sesion";
    public const string ClaimEmpresa = "selvana:empresa";

    /// <summary>Empresas usuarias que ve: «*» (todas) o los Id separados por coma.</summary>
    public const string ClaimEmpresasUsuarias = "selvana:empresas-usuarias";

    /// <summary>Política de límite de peticiones del ingreso (se configura en el host).</summary>
    public const string PoliticaIngreso = "ingreso";

    public static Task IniciarAsync(HttpContext http, Guid usuarioId, string nombre, string rol, Guid empresaId, bool todas, IEnumerable<Guid> empresasUsuarias)
    {
        ArgumentNullException.ThrowIfNull(http);
        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new Claim(ClaimTypes.Name, nombre),
            new Claim(ClaimTypes.Role, rol),
            new Claim(ClaimEmpresa, empresaId.ToString()),
            new Claim(ClaimEmpresasUsuarias, todas ? "*" : string.Join(',', empresasUsuarias)),
        ], Esquema);

        return http.SignInAsync(Esquema, new ClaimsPrincipal(identidad), new AuthenticationProperties { IsPersistent = false });
    }

    public static Task CerrarAsync(HttpContext http) => http.SignOutAsync(Esquema);
}
