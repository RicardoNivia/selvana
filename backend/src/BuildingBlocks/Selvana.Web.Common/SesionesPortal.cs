using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Selvana.Web.Common;

/// <summary>
/// Sesión del portal de la persona (ADR 0013): otra cookie y otro esquema que la consola, así una sesión nunca sirve en la otra.
/// </summary>
public static class SesionesPortal
{
    public const string Esquema = "Portal";
    public const string NombreCookie = "selvana.portal";

    /// <summary>Política que exigen los endpoints de la cuenta de la persona.</summary>
    public const string Politica = "portal";

    /// <summary>Límite de peticiones de ingreso y registro del portal (se configura en el host).</summary>
    public const string PoliticaIngreso = "ingreso-portal";

    public static Task IniciarAsync(HttpContext http, Guid cuentaId, string nombre)
    {
        ArgumentNullException.ThrowIfNull(http);
        var identidad = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, cuentaId.ToString()), new Claim(ClaimTypes.Name, nombre)], Esquema);
        return http.SignInAsync(Esquema, new ClaimsPrincipal(identidad), new AuthenticationProperties { IsPersistent = true });
    }

    public static Task CerrarAsync(HttpContext http) => http.SignOutAsync(Esquema);

    /// <summary>
    /// Filtro para endpoints públicos donde la sesión del portal es opcional (postularse, ADR 0014): si llega la cookie del portal, la persona
    /// queda identificada; si no, sigue como anónima.
    /// </summary>
    public static async ValueTask<object?> UsarSiHay(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(siguiente);
        var resultado = await contexto.HttpContext.AuthenticateAsync(Esquema);
        if (resultado.Succeeded)
        {
            contexto.HttpContext.User = resultado.Principal;
        }

        return await siguiente(contexto);
    }
}
