using System.Globalization;
using System.Text.RegularExpressions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;

namespace Selvana.Reclutamiento.Domain.Filtros;

public enum ResultadoFiltros
{
    /// <summary>La vacante no tiene filtros excluyentes.</summary>
    SinFiltros,
    Cumple,

    /// <summary>De algún excluyente no hay evidencia ni a favor ni en contra: lo revisa una persona.</summary>
    PorRevisar,
    NoCumple,
}

public enum EstadoChequeo
{
    Cumple,
    NoCumple,
    SinEvidencia,
}

/// <summary>Un filtro revisado: qué se pidió, qué se encontró y de dónde sale.</summary>
public sealed record ChequeoFiltro(string Tipo, string Filtro, EstadoChequeo Estado, string Detalle, string Evidencia);

public sealed record EvaluacionFiltros(ResultadoFiltros Resultado, IReadOnlyList<ChequeoFiltro> Chequeos)
{
    public ChequeoFiltro? PrimerNoCumple => Chequeos.FirstOrDefault(c => c.Estado == EstadoChequeo.NoCumple);

    /// <summary>Motivo del descarte que se registra si una persona lo aprueba.</summary>
    public string? MotivoDescarte => PrimerNoCumple is { } c ? Recortar($"Filtro ATS · {c.Filtro}: {c.Detalle}", 500) : null;

    private static string Recortar(string t, int n) => t.Length <= n ? t : t[..n];
}

/// <summary>Un filtro sugerido para la vacante con la razón.</summary>
public sealed record FiltroSugerido(string Tipo, string Filtro, string Razon);

/// <summary>
/// Filtros ATS por reglas (sin IA): revisa cada excluyente contra la hoja de vida (con sinónimos), las respuestas a las preguntas filtro,
/// la ciudad, los meses de experiencia con fechas y lo que el agente de ranking encontró. Nunca mira edad, género, foto ni dirección.
/// No descarta: produce el resultado y la evidencia para que una persona decida.
/// </summary>
public static partial class EvaluadorFiltros
{
    public const string TipoRequisito = "Requisito";
    public const string TipoPregunta = "Pregunta";
    public const string TipoCiudad = "Ciudad";
    public const string TipoExperiencia = "Experiencia";

    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "de", "del", "la", "el", "los", "las", "en", "con", "para", "por", "y", "o", "un", "una", "minimo", "minima", "deseable", "vigente", "tener",
        "contar", "curso", "certificado", "certificacion", "experiencia", "conocimiento", "conocimientos", "manejo", "al", "a", "su", "sus", "que",
    };

    public static EvaluacionFiltros Evaluar(FiltrosVacante? filtros, DatosCandidato datos, IReadOnlyList<RespuestaFiltro> respuestas, AfinidadPostulacion? afinidad,
        DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(respuestas);
        if (filtros is null || filtros.Vacios)
        {
            return new EvaluacionFiltros(ResultadoFiltros.SinFiltros, []);
        }

        var chequeos = new List<ChequeoFiltro>();
        foreach (var pregunta in filtros.PreguntasExcluyentes)
        {
            var r = respuestas.FirstOrDefault(x => Normalizacion.Simplificar(x.Pregunta) == Normalizacion.Simplificar(pregunta));
            chequeos.Add(r is null
                ? new ChequeoFiltro(TipoPregunta, pregunta, EstadoChequeo.SinEvidencia, "No respondió esta pregunta (se agregó después de su postulación).", "Preguntas filtro")
                : r.Respuesta
                    ? new ChequeoFiltro(TipoPregunta, pregunta, EstadoChequeo.Cumple, "Respondió Sí.", "Preguntas filtro")
                    : new ChequeoFiltro(TipoPregunta, pregunta, EstadoChequeo.NoCumple, $"Respondió No a «{pregunta}».", "Preguntas filtro"));
        }

        var meses = RevisionHojaDeVida.MesesDeExperiencia(datos, hoy);
        foreach (var requisito in filtros.RequisitosExcluyentes)
        {
            chequeos.Add(Requisito(requisito, datos, afinidad, meses));
        }

        if (filtros.Ciudades.Count > 0)
        {
            var ciudad = Normalizacion.Simplificar(datos.Ciudad);
            var lista = string.Join(", ", filtros.Ciudades);
            chequeos.Add(ciudad.Length == 0
                ? new ChequeoFiltro(TipoCiudad, lista, EstadoChequeo.SinEvidencia, "No dijo en qué ciudad vive.", "HV · ciudad")
                : filtros.Ciudades.Any(c => Normalizacion.Simplificar(c) == ciudad)
                    ? new ChequeoFiltro(TipoCiudad, lista, EstadoChequeo.Cumple, $"Vive en {datos.Ciudad}.", "HV · ciudad")
                    : new ChequeoFiltro(TipoCiudad, lista, EstadoChequeo.NoCumple, $"Vive en {datos.Ciudad}; la vacante es para {lista}.", "HV · ciudad"));
        }

        if (filtros.ExperienciaMinimaMeses is > 0 and var minimo)
        {
            chequeos.Add(meses >= minimo
                ? new ChequeoFiltro(TipoExperiencia, $"Experiencia mínima de {Meses(minimo)}", EstadoChequeo.Cumple, $"Tiene {Meses(meses)} con fechas.", "HV · experiencia")
                : new ChequeoFiltro(TipoExperiencia, $"Experiencia mínima de {Meses(minimo)}", EstadoChequeo.NoCumple, $"Tiene {Meses(meses)} con fechas; la vacante pide {Meses(minimo)}.",
                    "HV · experiencia"));
        }

        var resultado = chequeos.Any(c => c.Estado == EstadoChequeo.NoCumple) ? ResultadoFiltros.NoCumple
            : chequeos.Any(c => c.Estado == EstadoChequeo.SinEvidencia) ? ResultadoFiltros.PorRevisar
            : ResultadoFiltros.Cumple;
        return new EvaluacionFiltros(resultado, chequeos);
    }

    private static ChequeoFiltro Requisito(string requisito, DatosCandidato datos, AfinidadPostulacion? afinidad, int meses)
    {
        // «2 años de experiencia»: se mide con los meses que tienen fechas
        if (AniosPedidos(requisito) is { } anios)
        {
            return meses >= anios * 12
                ? new ChequeoFiltro(TipoRequisito, requisito, EstadoChequeo.Cumple, $"Tiene {Meses(meses)} de experiencia con fechas.", "HV · experiencia")
                : new ChequeoFiltro(TipoRequisito, requisito, EstadoChequeo.NoCumple, $"Tiene {Meses(meses)} con fechas; pide {anios} {(anios == 1 ? "año" : "años")}.", "HV · experiencia");
        }

        // Habilidades y cursos del catálogo con sinónimos («pase C2» = «Licencia de conducción C2»)
        var canonico = Normalizacion.Habilidad(requisito);
        if (Normalizacion.HabilidadesDe(datos).Contains(canonico, StringComparer.OrdinalIgnoreCase))
        {
            return new ChequeoFiltro(TipoRequisito, requisito, EstadoChequeo.Cumple, $"La hoja de vida dice «{canonico}».", "HV · habilidades y cursos");
        }

        // Todas las palabras importantes del requisito aparecen en la hoja de vida
        var palabras = Normalizacion.Simplificar(requisito).Split(' ').Where(p => p.Length > 2 && !Vacias.Contains(p)).ToList();
        var texto = " " + Normalizacion.TextoBusqueda(datos) + " ";
        if (palabras.Count > 0 && palabras.All(p => texto.Contains(" " + p, StringComparison.Ordinal)))
        {
            return new ChequeoFiltro(TipoRequisito, requisito, EstadoChequeo.Cumple, "Las palabras del requisito aparecen en la hoja de vida.", "HV");
        }

        // Lo que encontró el agente de ranking (copia el texto de los requisitos)
        var simple = Normalizacion.Simplificar(requisito);
        if (afinidad?.Cumplidos.Any(c => Normalizacion.Simplificar(c) == simple) == true)
        {
            return new ChequeoFiltro(TipoRequisito, requisito, EstadoChequeo.Cumple, "El agente de ranking lo encontró en la hoja de vida.", "Ranking IA");
        }

        if (afinidad?.Faltantes.Any(c => Normalizacion.Simplificar(c) == simple) == true)
        {
            return new ChequeoFiltro(TipoRequisito, requisito, EstadoChequeo.NoCumple, "La hoja de vida no lo demuestra.", "Ranking IA");
        }

        return new ChequeoFiltro(TipoRequisito, requisito, EstadoChequeo.SinEvidencia, "No encontramos evidencia en la hoja de vida.", "HV");
    }

    /// <summary>Sugiere qué es excluyente: requisitos con licencia, curso, certificado, título o años; las preguntas de sí o no; la ciudad.</summary>
    public static IReadOnlyList<FiltroSugerido> Sugerir(Vacante vacante)
    {
        ArgumentNullException.ThrowIfNull(vacante);
        var sugeridos = new List<FiltroSugerido>();
        foreach (var r in vacante.Requisitos.Where(r => !VariablesProtegidas.Menciona(r)))
        {
            var s = Normalizacion.Simplificar(r);
            if (AniosPedidos(r) is { } a)
            {
                sugeridos.Add(new FiltroSugerido(TipoRequisito, r, $"Pide {a} {(a == 1 ? "año" : "años")} de experiencia: se mide con las fechas de la hoja de vida."));
            }
            else if (Clave().Match(s) is { Success: true } m)
            {
                sugeridos.Add(new FiltroSugerido(TipoRequisito, r, $"Menciona «{m.Value}»: suele ser obligatorio para el cargo."));
            }
        }

        // Solo las de sí o no: «¿Cuántos años…?» o «¿Qué…?» no se responden con un sí que cumpla
        foreach (var p in vacante.PreguntasFiltro.Where(p => !VariablesProtegidas.Menciona(p) && !Abierta().IsMatch(Normalizacion.Simplificar(p))))
        {
            sugeridos.Add(new FiltroSugerido(TipoPregunta, p, "Es una pregunta de sí o no: un «No» indica que no cumple."));
        }

        if (!string.IsNullOrWhiteSpace(vacante.Ciudad))
        {
            sugeridos.Add(new FiltroSugerido(TipoCiudad, vacante.Ciudad, "La vacante es presencial en esa ciudad."));
        }

        return sugeridos;
    }

    public static int? AniosPedidos(string requisito)
    {
        var m = Anios().Match(Normalizacion.Simplificar(requisito));
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is > 0 and <= 40 ? n : null;
    }

    private static string Meses(int meses) => meses >= 12
        ? $"{meses / 12} {(meses / 12 == 1 ? "año" : "años")}{(meses % 12 > 0 ? $" y {meses % 12} {(meses % 12 == 1 ? "mes" : "meses")}" : "")}"
        : $"{meses} {(meses == 1 ? "mes" : "meses")}";

    [GeneratedRegex(@"^(cuant[oa]s?|que|cual(es)?|como|donde|cuando|por que)\b")]
    private static partial Regex Abierta();

    [GeneratedRegex(@"\b(\d{1,2})\s*anos?\b(?!\s*de\s*edad)")]
    private static partial Regex Anios();

    [GeneratedRegex(@"\b(licencia|pase|curso|certificado|certificacion|titulo|tecnico|tecnologo|bachiller|profesional|tarjeta profesional|obligatori[oa]|indispensable|excluyente)\b")]
    private static partial Regex Clave();
}
