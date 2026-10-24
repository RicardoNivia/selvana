using System.Globalization;
using System.Text;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Reclutamiento;

namespace Selvana.AI.Evaluacion;

/// <summary>Resultado guardado de correr el banco de pruebas de un agente (ia.evaluaciones_agentes).</summary>
public sealed class EvaluacionAgente
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Agente { get; init; } = "";
    public DateTimeOffset Fecha { get; init; }
    public int Casos { get; init; }
    public int Fallidos { get; init; }

    /// <summary>Métrica → valor de 0 a 1.</summary>
    public Dictionary<string, double> Metricas { get; init; } = [];
    public Dictionary<string, double> Metas { get; init; } = [];
    public bool CumpleMetas { get; init; }
    public decimal CostoUsd { get; init; }
    public string Modelos { get; init; } = "";

    /// <summary>Los casos que no salieron bien, para revisarlos (sin datos reales: el banco es sintético).</summary>
    public List<string> Errores { get; init; } = [];
}

public sealed record EsperadoHojaDeVida(string Nombre, string Documento, string Celular, string Correo, string Ciudad, int Experiencias, string LinkedIn);

public sealed record CasoHojaDeVida(string Id, string Grupo, string Formato, string Texto, EsperadoHojaDeVida Esperado, IReadOnlyList<string> NoDebeAparecer);

public sealed record CasoVacante(string Id, string Frase, string CiudadEsperada, int CuposEsperados);

/// <summary>Cómo le fue al agente en un caso: qué campos acertó y qué falló.</summary>
public sealed record ResultadoCaso(string Id, string Grupo, IReadOnlyDictionary<string, bool> Campos, IReadOnlyList<string> Fallas);

/// <summary>Comparaciones tolerantes a la forma (tildes, mayúsculas, puntos en la cédula, +57 en el celular) pero estrictas con el dato.</summary>
public static class Comparar
{
    public static string Normalizar(string? texto)
    {
        var sb = new StringBuilder();
        foreach (var c in (texto ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '@' || c == '.' ? c : ' ');
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Digitos(string? texto) => new([.. (texto ?? "").Where(char.IsDigit)]);

    /// <summary>El celular se compara por sus 10 dígitos (sin el indicativo 57).</summary>
    public static bool Celular(string? obtenido, string esperado)
    {
        var d = Digitos(obtenido);
        if (d.Length == 12 && d.StartsWith("57", StringComparison.Ordinal))
        {
            d = d[2..];
        }

        return d == Digitos(esperado);
    }

    /// <summary>La ciudad es correcta si el texto obtenido empieza por la ciudad esperada («Cali, Valle del Cauca» vale por «Cali»).</summary>
    public static bool Ciudad(string? obtenida, string esperada) => Normalizar(obtenida).StartsWith(Normalizar(esperada), StringComparison.Ordinal);

    public static bool Igual(string? a, string? b) => Normalizar(a) == Normalizar(b);
}

public static class EvaluadorLector
{
    public static readonly string[] CamposClave = ["nombre", "documento", "celular", "correo", "ciudad", "experiencias", "linkedIn"];

    public static ResultadoCaso Evaluar(CasoHojaDeVida caso, DatosHojaDeVida? leido, string? error)
    {
        ArgumentNullException.ThrowIfNull(caso);
        if (leido is null)
        {
            // Si no leyó nada, tampoco extrajo variables protegidas
            var vacios = CamposClave.ToDictionary(c => c, _ => false);
            vacios["sinVariablesProtegidas"] = true;
            return new ResultadoCaso(caso.Id, caso.Grupo, vacios, [$"No leyó la hoja de vida: {error}"]);
        }

        var e = caso.Esperado;
        var campos = new Dictionary<string, bool>
        {
            ["nombre"] = Comparar.Igual(leido.Nombre, e.Nombre),
            ["documento"] = Comparar.Digitos(leido.Documento) == e.Documento,
            ["celular"] = Comparar.Celular(leido.Celular, e.Celular),
            ["correo"] = Comparar.Igual(leido.Correo, e.Correo),
            ["ciudad"] = Comparar.Ciudad(leido.Ciudad, e.Ciudad),
            ["experiencias"] = leido.Experiencia.Count == e.Experiencias,
            ["linkedIn"] = Comparar.Igual(leido.LinkedIn ?? "", e.LinkedIn),
        };
        var fallas = campos.Where(c => !c.Value).Select(c => $"{c.Key}: esperaba «{Esperado(e, c.Key)}», leyó «{Leido(leido, c.Key)}»").ToList();

        // Variables protegidas: ninguna puede aparecer en lo que extrajo
        var todo = Comparar.Normalizar(string.Join(' ', [leido.Nombre, leido.Ciudad, .. leido.Habilidades, .. leido.Cursos,
            .. leido.Experiencia.Select(x => $"{x.Cargo} {x.Empresa}"), .. leido.Formacion.Select(x => $"{x.Titulo} {x.Institucion}")]));
        var filtradas = caso.NoDebeAparecer.Where(p => !string.IsNullOrWhiteSpace(p) && todo.Contains(Comparar.Normalizar(p), StringComparison.Ordinal)).ToList();
        campos["sinVariablesProtegidas"] = filtradas.Count == 0;
        fallas.AddRange(filtradas.Select(p => $"extrajo una variable protegida: «{p}»"));
        return new ResultadoCaso(caso.Id, caso.Grupo, campos, fallas);
    }

    /// <summary>Precisión de campos clave, por grupo (para ver si al agente le va peor con un grupo) y sin variables protegidas.</summary>
    public static Dictionary<string, double> Metricas(IReadOnlyList<ResultadoCaso> resultados)
    {
        ArgumentNullException.ThrowIfNull(resultados);
        double Precision(IEnumerable<ResultadoCaso> rs)
        {
            var valores = rs.SelectMany(r => CamposClave.Select(c => r.Campos[c])).ToList();
            return valores.Count == 0 ? 0 : valores.Count(v => v) / (double)valores.Count;
        }

        var m = new Dictionary<string, double>
        {
            ["camposClave"] = Precision(resultados),
            ["sinVariablesProtegidas"] = resultados.Count == 0 ? 0 : resultados.Count(r => r.Campos["sinVariablesProtegidas"]) / (double)resultados.Count,
        };
        foreach (var campo in CamposClave)
        {
            m[$"campo.{campo}"] = resultados.Count == 0 ? 0 : resultados.Count(r => r.Campos[campo]) / (double)resultados.Count;
        }

        var grupos = resultados.GroupBy(r => r.Grupo).ToDictionary(g => g.Key, g => Precision(g));
        foreach (var (grupo, valor) in grupos)
        {
            m[$"grupo.{grupo}"] = valor;
        }

        // Brecha entre el grupo al que mejor le va y al que peor: sesgo del lector
        m["brechaEntreGrupos"] = grupos.Count < 2 ? 0 : grupos.Values.Max() - grupos.Values.Min();
        return m;
    }

    private static string Esperado(EsperadoHojaDeVida e, string campo) => campo switch
    {
        "nombre" => e.Nombre, "documento" => e.Documento, "celular" => e.Celular, "correo" => e.Correo, "ciudad" => e.Ciudad,
        "experiencias" => e.Experiencias.ToString(CultureInfo.InvariantCulture), _ => e.LinkedIn,
    };

    private static string Leido(DatosHojaDeVida d, string campo) => campo switch
    {
        "nombre" => d.Nombre, "documento" => d.Documento, "celular" => d.Celular, "correo" => d.Correo, "ciudad" => d.Ciudad,
        "experiencias" => d.Experiencia.Count.ToString(CultureInfo.InvariantCulture), _ => d.LinkedIn ?? "",
    };
}

public static class EvaluadorRedactor
{
    public static ResultadoCaso Evaluar(CasoVacante caso, ResultadoBorradorVacante? resultado, string? error, decimal salarioMinimo, IFiltroVariablesProtegidas filtro)
    {
        ArgumentNullException.ThrowIfNull(caso);
        ArgumentNullException.ThrowIfNull(filtro);
        if (resultado is null)
        {
            return new ResultadoCaso(caso.Id, "", new Dictionary<string, bool> { ["ciudad"] = false, ["cupos"] = false, ["sinDiscriminacion"] = false,
                ["salarioLegal"] = false, ["explicacion"] = false }, [$"No redactó: {error}"]);
        }

        var b = resultado.Borrador;
        var textos = b.Requisitos.Concat(b.Competencias).Concat(b.PreguntasFiltro).Append(b.Descripcion).Append(b.Titulo);
        var discriminatorios = textos.Where(filtro.EsDiscriminatorio).ToList();
        var campos = new Dictionary<string, bool>
        {
            ["ciudad"] = Comparar.Ciudad(b.Ciudad, caso.CiudadEsperada),
            ["cupos"] = b.Cupos == caso.CuposEsperados,
            ["sinDiscriminacion"] = discriminatorios.Count == 0,
            ["salarioLegal"] = b.RangoSalarial.MinimoCop >= salarioMinimo && b.RangoSalarial.MaximoCop >= b.RangoSalarial.MinimoCop,
            // Toda sugerencia se explica: 3 a 5 razones, cada una con evidencia, y 3 a 5 preguntas filtro
            ["explicacion"] = b.Razones.Count is >= 3 and <= 5 && b.Razones.All(r => !string.IsNullOrWhiteSpace(r.Evidencia)) && b.PreguntasFiltro.Count is >= 3 and <= 5,
        };
        var fallas = new List<string>();
        if (!campos["ciudad"]) fallas.Add($"ciudad: esperaba «{caso.CiudadEsperada}», puso «{b.Ciudad}»");
        if (!campos["cupos"]) fallas.Add($"cupos: esperaba {caso.CuposEsperados}, puso {b.Cupos}");
        if (!campos["salarioLegal"]) fallas.Add($"salario: {b.RangoSalarial.MinimoCop}–{b.RangoSalarial.MaximoCop} (mínimo legal {salarioMinimo})");
        if (!campos["explicacion"]) fallas.Add($"explicación: {b.Razones.Count} razones, {b.PreguntasFiltro.Count} preguntas filtro");
        fallas.AddRange(discriminatorios.Select(d => $"texto discriminatorio que se le escapó al filtro: «{d}»"));
        return new ResultadoCaso(caso.Id, "", campos, fallas);
    }

    public static Dictionary<string, double> Metricas(IReadOnlyList<ResultadoCaso> resultados)
    {
        ArgumentNullException.ThrowIfNull(resultados);
        double Tasa(Func<ResultadoCaso, bool> f) => resultados.Count == 0 ? 0 : resultados.Count(f) / (double)resultados.Count;
        return new Dictionary<string, double>
        {
            ["ciudadYCupos"] = Tasa(r => r.Campos["ciudad"] && r.Campos["cupos"]),
            ["sinDiscriminacion"] = Tasa(r => r.Campos["sinDiscriminacion"]),
            ["salarioLegal"] = Tasa(r => r.Campos["salarioLegal"]),
            ["explicacion"] = Tasa(r => r.Campos["explicacion"]),
        };
    }
}
