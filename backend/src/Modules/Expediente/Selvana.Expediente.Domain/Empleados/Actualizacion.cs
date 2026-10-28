using System.Globalization;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Domain.Empleados;

public enum EstadoLectura
{
    /// <summary>La IA está leyendo el documento.</summary>
    Leyendo,
    Fallida,

    /// <summary>Hay cambios propuestos u observaciones: espera la decisión de RRHH.</summary>
    PorRevisar,

    /// <summary>La IA lo leyó y no encontró nada que cambiar.</summary>
    SinCambios,
    Aplicada,
    Descartada,
}

public sealed class CampoLeido
{
    public string Campo { get; init; } = "";
    public string Valor { get; init; } = "";
}

/// <summary>Un cambio que proponen las reglas a partir de lo que leyó la IA: qué, de qué a qué y de dónde sale.</summary>
public sealed class CambioPropuesto
{
    public string Clave { get; init; } = "";
    public string Etiqueta { get; init; } = "";
    public string Actual { get; init; } = "";
    public string Propuesto { get; init; } = "";
    public string Evidencia { get; init; } = "";
}

public sealed class RazonLectura
{
    public string Signo { get; init; } = "+";
    public string Texto { get; init; } = "";
    public string Fuente { get; init; } = "";
}

/// <summary>Lectura de la IA de una versión de un documento, con los cambios que proponen las reglas y la decisión de RRHH.</summary>
public sealed class LecturaDocumento
{
    public EstadoLectura Estado { get; set; }
    public string TipoDetectado { get; set; } = "";
    public bool Legible { get; set; }
    public bool TipoCorrecto { get; set; }
    public List<CampoLeido> Campos { get; set; } = [];
    public List<string> Observaciones { get; set; } = [];
    public List<RazonLectura> Razones { get; set; } = [];
    public List<CambioPropuesto> Cambios { get; set; } = [];
    public string? Modelo { get; set; }
    public decimal CostoUsd { get; set; }
    public string? Error { get; set; }
    public string? ResueltaPor { get; set; }
    public DateTimeOffset? ResueltaEn { get; set; }
}

/// <summary>Valor que RRHH aplica (puede corregir el que propuso la IA).</summary>
public sealed record CambioAplicado(string Clave, string Valor);

public static class ActualizacionErrors
{
    public static readonly Error SinLectura = Error.Conflict("Expediente.SinLectura", "Ese documento no tiene una lectura de la IA por revisar.");
    public static readonly Error ClaveInvalida = Error.Validation("Expediente.CambioInvalido", "Uno de los cambios no se reconoce. Vuelva a abrir la propuesta.");
    public static readonly Error FechaInvalida = Error.Validation("Expediente.FechaInvalida", "La fecha de vencimiento no se entiende: use dd/mm/aaaa.");
    public static readonly Error Leyendo = Error.Conflict("Expediente.Leyendo", "La IA está leyendo este documento: espere un momento.");
}

/// <summary>
/// Reglas (sin IA) que convierten lo que leyó la IA en cambios del expediente: entidad de EPS, pensión, ARL o caja según el documento,
/// y la fecha de vencimiento. Si el número de documento leído no es el del empleado, no propone nada y lo deja como observación.
/// </summary>
public static class ReglasActualizacion
{
    public static readonly string[] Claves = ["Eps", "FondoPension", "Arl", "CajaCompensacion", "VenceEn"];
    private static readonly string[] FormatosFecha = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d 'de' MMMM 'de' yyyy"];
    private static readonly CultureInfo Colombia = new("es-CO");

    public static DateOnly? Fecha(string texto) =>
        DateOnly.TryParseExact(texto.Trim(), FormatosFecha, Colombia, DateTimeStyles.None, out var f) ? f : null;

    private static string? Valor(IEnumerable<CampoLeido> campos, params string[] nombres) =>
        campos.FirstOrDefault(c => nombres.Any(n => c.Campo.Contains(n, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(c.Valor))?.Valor.Trim();

    /// <summary>Qué afiliación es por el tipo detectado (o el tipo con que se subió).</summary>
    private static (string Clave, string Etiqueta)? Afiliacion(string tipo)
    {
        var t = tipo.ToUpperInvariant();
        return t.Contains("EPS", StringComparison.Ordinal) || t.Contains("SALUD", StringComparison.Ordinal) ? ("Eps", "EPS")
            : t.Contains("PENSI", StringComparison.Ordinal) || t.Contains("AFP", StringComparison.Ordinal) ? ("FondoPension", "Fondo de pensión")
            : t.Contains("ARL", StringComparison.Ordinal) || t.Contains("RIESGOS", StringComparison.Ordinal) ? ("Arl", "ARL")
            : t.Contains("CAJA", StringComparison.Ordinal) || t.Contains("COMPENSACI", StringComparison.Ordinal) ? ("CajaCompensacion", "Caja de compensación")
            : null;
    }

    public static (List<CambioPropuesto> Cambios, List<string> Observaciones) Proponer(Empleado e, DocumentoEmpleado d, string tipoDetectado, IReadOnlyList<CampoLeido> campos)
    {
        var cambios = new List<CambioPropuesto>();
        var observaciones = new List<string>();

        var numero = Valor(campos, "número de documento", "numero de documento", "cédula", "cedula", "identificación");
        if (numero is not null && new string(numero.Where(char.IsDigit).ToArray()) is { Length: > 0 } digitos && digitos != new string(e.Documento.Where(char.IsDigit).ToArray()))
        {
            observaciones.Add($"El documento trae el número {digitos} y el empleado tiene {e.Documento}: revise que sea de esta persona. No se propone ningún cambio.");
            return (cambios, observaciones);
        }

        if ((Afiliacion(tipoDetectado) ?? Afiliacion(d.Tipo + " " + d.Nombre)) is { } af && Valor(campos, "entidad", "eps", "fondo", "administradora", "caja") is { } entidad)
        {
            var actual = af.Clave switch
            {
                "Eps" => e.Afiliaciones.Eps,
                "FondoPension" => e.Afiliaciones.FondoPension,
                "Arl" => e.Afiliaciones.Arl,
                _ => e.Afiliaciones.CajaCompensacion,
            };
            if (!string.Equals(actual.Trim(), entidad, StringComparison.OrdinalIgnoreCase))
            {
                cambios.Add(new CambioPropuesto { Clave = af.Clave, Etiqueta = af.Etiqueta, Actual = actual, Propuesto = entidad, Evidencia = "Documento · entidad" });
            }
        }

        if (Valor(campos, "vencimiento", "vence", "vigente hasta", "vigencia", "válido hasta", "valido hasta") is { } texto)
        {
            if (Fecha(texto) is { } vence)
            {
                var actual = d.Vigente?.VenceEn;
                if (actual != vence)
                {
                    cambios.Add(new CambioPropuesto
                    {
                        Clave = "VenceEn", Etiqueta = $"Vencimiento de «{d.Nombre}»", Actual = actual?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
                        Propuesto = vence.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), Evidencia = "Documento · vigencia",
                    });
                }
            }
            else
            {
                observaciones.Add($"La vigencia dice «{texto}» y no se pudo leer como fecha: escríbala a mano si aplica.");
            }
        }

        return (cambios, observaciones);
    }
}
