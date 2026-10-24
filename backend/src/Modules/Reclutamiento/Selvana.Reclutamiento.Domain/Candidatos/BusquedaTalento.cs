namespace Selvana.Reclutamiento.Domain.Candidatos;

/// <summary>Lo que se entendió de la búsqueda: términos (todos deben estar en el texto del candidato), ciudad y cómo mostrarlo.</summary>
public sealed record InterpretacionBusqueda(IReadOnlyList<string> Terminos, string? Ciudad, string? Documento, IReadOnlyList<string> Entendido)
{
    public bool Vacia => Terminos.Count == 0 && Ciudad is null && Documento is null;
}

/// <summary>
/// Banco de talento en lenguaje natural, sin IA: «montacarguistas con licencia C1 en Mosquera» → términos «montacarg», «licencia», «c1»
/// y ciudad Mosquera. Quita palabras vacías, plurales y el género, y usa sinónimos del catálogo propio.
/// </summary>
public static class BusquedaTalento
{
    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "a", "al", "con", "de", "del", "el", "en", "la", "las", "lo", "los", "para", "por", "que", "un", "una", "unos", "unas", "y", "o", "u", "e",
        "busco", "buscar", "necesito", "necesitamos", "requiero", "quiero", "dame", "muestrame", "candidatos", "candidato", "candidatas", "personas",
        "persona", "gente", "hojas", "hoja", "vida", "hv", "tengan", "tenga", "tiene", "tienen", "sepan", "sepa", "experiencia", "conocimiento",
        "conocimientos", "anos", "ano", "mas", "menos", "como", "sean", "sea", "viva", "vivan", "vive", "viven", "cerca", "ciudad", "zona",
    };

    /// <summary>Sinónimos de la búsqueda → raíz que aparece en el texto del candidato.</summary>
    private static readonly Dictionary<string, string> Raices = new(StringComparer.Ordinal)
    {
        ["montacarguista"] = "montacarg", ["montacarga"] = "montacarg", ["montacargas"] = "montacarg",
        ["pase"] = "licencia", ["licencia"] = "licencia", ["licencias"] = "licencia", ["bodeguero"] = "bodega", ["bodeguera"] = "bodega",
        ["call"] = "call", ["callcenter"] = "call", ["contact"] = "contact", ["ingles"] = "ingles", ["excel"] = "excel",
    };

    private static readonly string[] Ciudades =
    [
        "bogota", "medellin", "cali", "barranquilla", "cartagena", "bucaramanga", "pereira", "manizales", "cucuta", "ibague", "santa marta",
        "villavicencio", "pasto", "monteria", "neiva", "armenia", "valledupar", "popayan", "sincelejo", "tunja", "riohacha", "florencia",
        "yopal", "quibdo", "soacha", "bello", "itagui", "envigado", "soledad", "palmira", "funza", "mosquera", "madrid", "cota", "chia",
        "zipaquira", "facatativa", "cajica", "tocancipa", "sibate", "girardot", "fusagasuga", "rionegro", "sabaneta", "dosquebradas",
        "floridablanca", "giron", "piedecuesta", "jamundi", "yumbo", "tulua", "buenaventura", "barrancabermeja", "malambo", "turbaco",
    ];

    public static InterpretacionBusqueda Interpretar(string? consulta)
    {
        var texto = Normalizacion.Simplificar(consulta);
        if (texto.Length == 0)
        {
            return new InterpretacionBusqueda([], null, null, []);
        }

        var entendido = new List<string>();
        var digitos = new string([.. texto.Where(char.IsDigit)]);
        if (digitos.Length >= 5 && texto.Replace(" ", "", StringComparison.Ordinal).All(char.IsDigit))
        {
            return new InterpretacionBusqueda([], null, digitos, [$"Documento que contiene {digitos}"]);
        }

        // La ciudad se reconoce entera (también «santa marta») y sale de los términos
        string? ciudad = null;
        var relleno = $" {texto} ";
        foreach (var c in Ciudades.OrderByDescending(c => c.Length))
        {
            if (relleno.Contains($" {c} ", StringComparison.Ordinal))
            {
                ciudad = c;
                relleno = relleno.Replace($" {c} ", " ", StringComparison.Ordinal);
                entendido.Add($"Ciudad: {Titulo(c)}");
                break;
            }
        }

        var terminos = new List<string>();
        foreach (var palabra in relleno.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Vacias.Contains(palabra) || (palabra.Length < 2 && !char.IsDigit(palabra[0])))
            {
                continue;
            }

            var raiz = Raiz(palabra);
            if (!terminos.Contains(raiz))
            {
                terminos.Add(raiz);
                entendido.Add(palabra);
            }
        }

        return new InterpretacionBusqueda(terminos, ciudad is null ? null : Titulo(ciudad), null, entendido);
    }

    /// <summary>«auxiliares» → «auxiliar», «cajeras» → «cajer», «c1» → «c1». Así coincide con plurales y con el femenino o masculino.</summary>
    public static string Raiz(string palabra)
    {
        if (Raices.TryGetValue(palabra, out var raiz))
        {
            return raiz;
        }

        if (palabra.Any(char.IsDigit) || palabra.Length <= 4)
        {
            return palabra;
        }

        var p = palabra;
        if (p.EndsWith("es", StringComparison.Ordinal) && p.Length > 5 && !"aeiou".Contains(p[^3]))
        {
            p = p[..^2];
        }
        else if (p.EndsWith('s'))
        {
            p = p[..^1];
        }

        if (Raices.TryGetValue(p, out raiz))
        {
            return raiz;
        }

        return p.Length > 5 && (p.EndsWith('a') || p.EndsWith('o')) ? p[..^1] : p;
    }

    /// <summary>Nombre para mostrar, con sus tildes.</summary>
    private static readonly Dictionary<string, string> ConTilde = new(StringComparer.Ordinal)
    {
        ["bogota"] = "Bogotá", ["medellin"] = "Medellín", ["cucuta"] = "Cúcuta", ["ibague"] = "Ibagué", ["monteria"] = "Montería",
        ["popayan"] = "Popayán", ["quibdo"] = "Quibdó", ["itagui"] = "Itagüí", ["chia"] = "Chía", ["zipaquira"] = "Zipaquirá",
        ["facatativa"] = "Facatativá", ["cajica"] = "Cajicá", ["tocancipa"] = "Tocancipá", ["sibate"] = "Sibaté", ["fusagasuga"] = "Fusagasugá",
        ["giron"] = "Girón", ["jamundi"] = "Jamundí", ["tulua"] = "Tuluá",
    };

    private static string Titulo(string ciudad) =>
        ConTilde.TryGetValue(ciudad, out var nombre) ? nombre : string.Join(' ', ciudad.Split(' ').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
