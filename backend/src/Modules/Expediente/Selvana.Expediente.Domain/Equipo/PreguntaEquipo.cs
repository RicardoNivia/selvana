using System.Globalization;
using System.Text;

namespace Selvana.Expediente.Domain.Equipo;

/// <summary>Atajos de la vista «Mi equipo»: lo mismo que entiende la pregunta, para filtrar a mano.</summary>
public enum FiltroRapido
{
    Todos,
    ConAlertas,
    DocumentosVencidos,
    DocumentosPorVencer,
    ContratoPorTerminar,
    Afiliaciones,
    PorEvaluar,
    RiesgoRotacion,
    Nuevos,
}

/// <summary>Lo que se entendió de la pregunta: un atajo, los términos que deben aparecer (en documentos, nombre, cargo o sede) y cómo explicarlo.</summary>
public sealed record InterpretacionEquipo(FiltroRapido Filtro, IReadOnlyList<string> Terminos, IReadOnlyList<string> Entendido)
{
    public bool Vacia => Filtro == FiltroRapido.Todos && Terminos.Count == 0;
}

/// <summary>
/// E4 · Preguntas del líder en lenguaje natural, sin IA (costo cero y siempre disponible): «¿quién tiene el curso de alturas vencido?» →
/// documentos vencidos que contienen «alturas». Lo que no reconoce queda como término de búsqueda y se muestra lo que entendió.
/// </summary>
public static class PreguntaEquipo
{
    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "a", "al", "con", "de", "del", "el", "en", "la", "las", "lo", "los", "para", "por", "que", "un", "una", "y", "o", "mi", "mis", "me", "se", "su", "sus",
        "quien", "quienes", "cual", "cuales", "cuantos", "cuantas", "tiene", "tienen", "tengo", "esta", "estan", "son", "es", "hay", "le", "les", "falta", "faltan",
        "equipo", "persona", "personas", "gente", "empleado", "empleados", "dame", "muestrame", "ver", "lista", "todos", "todas", "ya", "aun", "todavia", "mas",
        "vencido", "vencidos", "vencida", "vencidas", "vencio", "vencieron", "vence", "vencen", "vencer", "proximo", "proximos", "pronto", "semana", "mes",
        "documento", "documentos", "carpeta", "curso", "cursos", "certificado", "certificados", "contrato", "contratos", "termina", "terminan", "terminar",
        "evaluar", "evaluacion", "evaluaciones", "evaluado", "calificar", "pendiente", "pendientes", "sin", "riesgo", "irse", "renunciar", "rotacion", "nuevo",
        "nuevos", "nueva", "nuevas", "ingreso", "ingresaron", "ingresos", "recien", "llegaron", "alerta", "alertas", "afiliacion", "afiliaciones", "registrar",
        "eps", "arl", "pension", "caja", "compensacion", "induccion", "desempeno", "calificacion", "renuncia",
    };

    public static InterpretacionEquipo Interpretar(string? pregunta)
    {
        var texto = Simplificar(pregunta);
        if (texto.Length == 0)
        {
            return new(FiltroRapido.Todos, [], []);
        }

        bool Hay(params string[] raices) => raices.Any(r => texto.Contains(r, StringComparison.Ordinal));

        var (filtro, entendido) =
            Hay("riesgo", "irse", "renunci", "rotacion", "se vaya", "se quiere ir") ? (FiltroRapido.RiesgoRotacion, "Riesgo de rotación medio o alto")
            : Hay("evalua", "calific", "desempeno") ? (FiltroRapido.PorEvaluar, "Evaluación de desempeño pendiente de su calificación")
            : Hay("contrato") && Hay("termin", "venc", "renov", "fin") ? (FiltroRapido.ContratoPorTerminar, "Contrato que termina en los próximos 45 días")
            : Hay("afiliac", "eps", "arl", "pension", "caja") ? (FiltroRapido.Afiliaciones, "Afiliaciones sin registrar")
            : Hay("por vencer", "vence", "proxim", "pronto") ? (FiltroRapido.DocumentosPorVencer, "Documentos que vencen en los próximos 30 días")
            : Hay("vencid", "vencio", "caducad") ? (FiltroRapido.DocumentosVencidos, "Documentos vencidos")
            : Hay("nuevo", "nueva", "ingres", "recien", "llegaron", "induccion") ? (FiltroRapido.Nuevos, "Ingresaron en los últimos 90 días")
            : Hay("alerta", "pendiente", "atender") ? (FiltroRapido.ConAlertas, "Con alguna alerta")
            : (FiltroRapido.Todos, (string?)null);

        // Lo que queda (por ejemplo «alturas» o «montacargas») se busca en los documentos, el nombre, el cargo y la sede
        var terminos = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !Vacias.Contains(t) && !t.All(char.IsDigit))
            .Select(t => t.Length > 4 && t.EndsWith('s') ? t[..^1] : t)
            .Distinct().ToList();
        var lista = new List<string>();
        if (entendido is not null)
        {
            lista.Add(entendido);
        }

        lista.AddRange(terminos.Select(t => $"Que mencione «{t}»"));
        return new(filtro, terminos, lista);
    }

    /// <summary>Minúsculas, sin tildes ni signos.</summary>
    public static string Simplificar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return "";
        }

        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
