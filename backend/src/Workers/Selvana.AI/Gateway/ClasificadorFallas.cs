using System.Net;
using System.Reflection;

namespace Selvana.AI.Gateway;

public enum TipoFalla
{
    /// <summary>Error pasajero (5xx, red, tiempo agotado): se intenta el siguiente modelo sin castigar a este.</summary>
    Transitoria,

    /// <summary>429 o cuota del plan gratis agotada: se omite el modelo un rato.</summary>
    CuotaAgotada,

    /// <summary>401, 403 o 404: clave inválida o modelo retirado. Se omite por más tiempo.</summary>
    ModeloNoDisponible,
}

public interface IClasificadorFallas
{
    TipoFalla Clasificar(Exception error);
}

/// <summary>
/// Cada SDK lanza sus propias excepciones; se clasifican por código HTTP cuando lo exponen
/// y, si no, por las marcas que usan Google ("RESOURCE_EXHAUSTED") y Anthropic ("rate_limit").
/// </summary>
internal sealed class ClasificadorFallas : IClasificadorFallas
{
    public TipoFalla Clasificar(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (CodigoHttp(error) is { } codigo)
        {
            return codigo switch
            {
                429 => TipoFalla.CuotaAgotada,
                401 or 403 or 404 => TipoFalla.ModeloNoDisponible,
                _ => TipoFalla.Transitoria,
            };
        }

        var texto = error.GetType().Name + " " + error.Message;
        if (Contiene(texto, "RateLimit", "rate_limit", "RESOURCE_EXHAUSTED", "quota", "429"))
        {
            return TipoFalla.CuotaAgotada;
        }

        return Contiene(texto, "NOT_FOUND", "PERMISSION_DENIED", "UNAUTHENTICATED", "authentication_error", "404", "401", "403")
            ? TipoFalla.ModeloNoDisponible
            : TipoFalla.Transitoria;
    }

    private static int? CodigoHttp(Exception error)
    {
        for (var actual = error; actual is not null; actual = actual.InnerException)
        {
            if (actual is HttpRequestException { StatusCode: { } status })
            {
                return (int)status;
            }

            // Hay SDK que redefinen StatusCode en una clase derivada: GetProperty(nombre) sería ambiguo.
            var propiedad = actual.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name == "StatusCode" && p.GetIndexParameters().Length == 0);
            var valor = propiedad?.GetValue(actual);
            if (valor is HttpStatusCode http)
            {
                return (int)http;
            }

            if (valor is int numero)
            {
                return numero;
            }
        }

        return null;
    }

    private static bool Contiene(string texto, params string[] marcas) =>
        marcas.Any(m => texto.Contains(m, StringComparison.OrdinalIgnoreCase));
}
