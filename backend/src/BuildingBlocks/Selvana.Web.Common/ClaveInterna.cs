using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Selvana.Infrastructure.Common.Messaging;

namespace Selvana.Web.Common;

/// <summary>Endpoints internos entre la API y el AI.Worker: se autentican con la clave compartida, no con un usuario.</summary>
public static class ClaveInterna
{
    /// <summary>Nombre de la política de límite de peticiones del portal público (se configura en el host).</summary>
    public const string PoliticaPortalPublico = "portal-publico";

    public static bool Valida(string? recibida, string esperada) =>
        !string.IsNullOrEmpty(esperada) && recibida is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recibida), Encoding.UTF8.GetBytes(esperada));

    public static RouteHandlerBuilder RequiereClaveInterna(this RouteHandlerBuilder builder) =>
        builder.AllowAnonymous().ExcludeFromDescription().AddEndpointFilter(async (contexto, siguiente) =>
        {
            var opciones = contexto.HttpContext.RequestServices.GetRequiredService<IOptions<MensajeriaOptions>>().Value;
            return Valida(contexto.HttpContext.Request.Headers[MensajeriaOptions.EncabezadoClave], opciones.ClaveInterna)
                ? await siguiente(contexto)
                : Results.Unauthorized();
        });
}
