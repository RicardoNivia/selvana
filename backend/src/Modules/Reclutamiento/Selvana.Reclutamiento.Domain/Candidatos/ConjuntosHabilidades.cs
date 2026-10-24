namespace Selvana.Reclutamiento.Domain.Candidatos;

public enum GrupoHabilidad
{
    Oficio,
    Herramientas,
    Certificaciones,
    Idiomas,
    Blandas,
}

/// <summary>Una habilidad normalizada y lo que escribió el candidato (la evidencia: «puntual» → Puntualidad).</summary>
public sealed record HabilidadDeclarada(string Nombre, string Menciona);

public sealed record ConjuntoHabilidades(GrupoHabilidad Grupo, IReadOnlyList<HabilidadDeclarada> Habilidades);

/// <summary>
/// Agrupa las habilidades y los cursos de la hoja de vida en conjuntos (blandas, del oficio, herramientas, certificaciones, idiomas)
/// con un catálogo propio, sin IA. Los cursos siempre van a certificaciones; lo que no está en el catálogo queda como habilidad del oficio.
/// Las blandas son declaradas por el candidato: se confirman en la preselección o en las pruebas.
/// </summary>
public static class ConjuntosHabilidades
{
    private static readonly (string Canonico, string[] Sinonimos)[] Blandas =
    [
        ("Responsabilidad", ["responsable", "responsabilidad"]),
        ("Puntualidad", ["puntual", "puntualidad"]),
        ("Trabajo en equipo", ["trabajo en equipo", "trabajar en equipo", "colaborativo", "colaborativa", "trabajo colaborativo"]),
        ("Comunicación asertiva", ["comunicacion asertiva", "comunicacion", "buena comunicacion", "asertivo", "asertiva", "asertividad"]),
        ("Liderazgo", ["liderazgo", "lider", "manejo de personal"]),
        ("Proactividad", ["proactivo", "proactiva", "proactividad", "iniciativa"]),
        ("Orientación al servicio", ["orientacion al servicio", "vocacion de servicio", "actitud de servicio", "orientacion al cliente"]),
        ("Adaptabilidad", ["adaptabilidad", "adaptable", "flexibilidad", "flexible", "adaptacion al cambio"]),
        ("Resolución de problemas", ["resolucion de problemas", "solucion de problemas", "resolucion de conflictos", "manejo de conflictos"]),
        ("Trabajo bajo presión", ["trabajo bajo presion", "manejo de presion", "tolerancia a la presion", "bajo presion"]),
        ("Honestidad", ["honesto", "honesta", "honestidad", "integridad", "etica"]),
        ("Compromiso", ["compromiso", "comprometido", "comprometida", "sentido de pertenencia"]),
        ("Organización", ["organizado", "organizada", "organizacion", "orden"]),
        ("Atención al detalle", ["atencion al detalle", "detallista", "precision"]),
        ("Buena actitud", ["buena actitud", "actitud positiva", "actitud"]),
        ("Empatía", ["empatia", "empatico", "empatica"]),
        ("Disciplina", ["disciplina", "disciplinado", "disciplinada"]),
        ("Aprendizaje rápido", ["aprendizaje rapido", "aprendo rapido", "facilidad de aprendizaje", "ganas de aprender"]),
        ("Creatividad", ["creatividad", "creativo", "creativa"]),
        ("Amabilidad", ["amable", "amabilidad", "cordial", "cordialidad"]),
    ];

    private static readonly string[] Herramientas =
    [
        "excel", "word", "office", "powerpoint", "outlook", "sap", "wms", "erp", "siigo", "power bi", "google sheets", "hojas de calculo",
        "punto de venta", "pos", "crm", "salesforce", "zendesk", "software", "sistemas", "computador", "digitacion", "internet", "windows",
    ];

    private static readonly string[] Certificaciones =
    [
        "licencia", "pase", "alturas", "manipulacion de alimentos", "primeros auxilios", "curso", "certificado", "certificacion", "diplomado",
        "brigada", "bpm", "haccp", "seguridad y salud", "sst", "vigilancia", "supervisor de seguridad",
    ];

    private static readonly string[] Idiomas = ["ingles", "frances", "portugues", "aleman", "italiano", "english", "bilingue"];

    public static IReadOnlyList<ConjuntoHabilidades> Agrupar(DatosCandidato datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var grupos = Enum.GetValues<GrupoHabilidad>().ToDictionary(g => g, _ => new List<HabilidadDeclarada>());
        foreach (var h in datos.Habilidades.Where(h => !string.IsNullOrWhiteSpace(h)))
        {
            var (grupo, nombre) = Clasificar(h);
            Agregar(grupos[grupo], nombre, h);
        }

        foreach (var c in datos.Cursos.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            Agregar(Contiene(Normalizacion.Simplificar(c), Idiomas) ? grupos[GrupoHabilidad.Idiomas] : grupos[GrupoHabilidad.Certificaciones], c.Trim(), c);
        }

        return [.. grupos.Where(g => g.Value.Count > 0).Select(g => new ConjuntoHabilidades(g.Key, g.Value))];
    }

    /// <summary>Las habilidades que dicen qué sabe hacer (sin las blandas): las que muestra el banco de talento.</summary>
    public static IReadOnlyList<string> Tecnicas(DatosCandidato datos) =>
        [.. Agrupar(datos).Where(g => g.Grupo != GrupoHabilidad.Blandas).SelectMany(g => g.Habilidades.Select(h => h.Nombre))];

    public static (GrupoHabilidad Grupo, string Nombre) Clasificar(string habilidad)
    {
        var simple = Normalizacion.Simplificar(habilidad);
        foreach (var (canonico, sinonimos) in Blandas)
        {
            if (sinonimos.Any(s => Frase(simple, s)))
            {
                return (GrupoHabilidad.Blandas, canonico);
            }
        }

        if (Contiene(simple, Idiomas))
        {
            return (GrupoHabilidad.Idiomas, habilidad.Trim());
        }

        var normal = Normalizacion.Habilidad(habilidad);
        if (Contiene(Normalizacion.Simplificar(normal), Certificaciones))
        {
            return (GrupoHabilidad.Certificaciones, normal);
        }

        return Contiene(simple, Herramientas) ? (GrupoHabilidad.Herramientas, normal) : (GrupoHabilidad.Oficio, normal);
    }

    private static void Agregar(List<HabilidadDeclarada> lista, string nombre, string menciona)
    {
        if (!lista.Any(x => string.Equals(x.Nombre, nombre, StringComparison.OrdinalIgnoreCase)))
        {
            lista.Add(new HabilidadDeclarada(nombre, menciona.Trim()));
        }
    }

    /// <summary>La frase aparece como palabras completas («pos» no coincide con «posgrado»).</summary>
    private static bool Frase(string texto, string frase) => $" {texto} ".Contains($" {frase} ", StringComparison.Ordinal);

    private static bool Contiene(string texto, string[] frases) => frases.Any(f => Frase(texto, f));
}
