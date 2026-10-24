using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Selvana.Reclutamiento.Domain.Candidatos;

/// <summary>Un vacío o una inconsistencia de la hoja de vida, con su evidencia. Es un aviso para revisar, no un descarte.</summary>
public sealed record AlertaHojaDeVida(string Tipo, string Texto, string Evidencia);

/// <summary>
/// Revisa la hoja de vida sin IA (reglas): fechas superpuestas, huecos largos, fechas imposibles y experiencia sin fechas.
/// Las fechas de solo año se toman a mitad de año, para no marcar como superpuesto un cambio de trabajo dentro del mismo año.
/// </summary>
public static partial class RevisionHojaDeVida
{
    public const string Superpuestas = "FechasSuperpuestas";
    public const string Hueco = "Hueco";
    public const string FechaImposible = "FechaImposible";
    public const string SinFechas = "SinFechas";
    public const string SinExperiencia = "SinExperiencia";

    private const int MesesHueco = 12;
    private const int MesesSuperpuestos = 2;

    private static readonly string[] Meses = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public static IReadOnlyList<AlertaHojaDeVida> Revisar(DatosCandidato datos, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var alertas = new List<AlertaHojaDeVida>();
        if (datos.Experiencia.Count == 0)
        {
            alertas.Add(new AlertaHojaDeVida(SinExperiencia, "No registra experiencia laboral.", "HV · experiencia"));
            return alertas;
        }

        var ahora = hoy.Year * 12 + hoy.Month - 1;
        var periodos = new List<(ExperienciaLaboral Exp, int N, int Desde, int Hasta)>();
        for (var i = 0; i < datos.Experiencia.Count; i++)
        {
            var e = datos.Experiencia[i];
            var evidencia = $"HV · experiencia {i + 1}";
            var desde = Mes(e.Desde, ahora);
            var hasta = Mes(e.Hasta, ahora);
            if (desde is null && hasta is null)
            {
                alertas.Add(new AlertaHojaDeVida(SinFechas, $"«{Nombre(e)}» no tiene fechas: la experiencia no está sustentada.", evidencia));
                continue;
            }

            if (desde > ahora || hasta > ahora)
            {
                alertas.Add(new AlertaHojaDeVida(FechaImposible, $"«{Nombre(e)}» tiene una fecha futura ({e.Desde} a {e.Hasta}).", evidencia));
                continue;
            }

            if (desde is not null && hasta is not null && hasta < desde)
            {
                alertas.Add(new AlertaHojaDeVida(FechaImposible, $"«{Nombre(e)}» termina antes de empezar ({e.Desde} a {e.Hasta}).", evidencia));
                continue;
            }

            if (desde is not null && hasta is not null)
            {
                periodos.Add((e, i + 1, desde.Value, hasta.Value));
            }
        }

        var orden = periodos.OrderBy(p => p.Desde).ToList();
        for (var i = 0; i < orden.Count; i++)
        {
            for (var j = i + 1; j < orden.Count; j++)
            {
                var a = orden[i];
                var b = orden[j];
                var cruce = Math.Min(a.Hasta, b.Hasta) - b.Desde;
                if (cruce > MesesSuperpuestos)
                {
                    alertas.Add(new AlertaHojaDeVida(Superpuestas,
                        $"«{Nombre(a.Exp)}» ({a.Exp.Desde} a {a.Exp.Hasta}) y «{Nombre(b.Exp)}» ({b.Exp.Desde} a {b.Exp.Hasta}) se cruzan {cruce} meses. Puede ser un trabajo simultáneo: confírmelo.",
                        $"HV · experiencias {a.N} y {b.N}"));
                }
            }
        }

        for (var i = 1; i < orden.Count; i++)
        {
            var finAnterior = orden.Take(i).Max(p => p.Hasta);
            var hueco = orden[i].Desde - finAnterior;
            if (hueco > MesesHueco)
            {
                var anterior = orden.Take(i).MaxBy(p => p.Hasta);
                alertas.Add(new AlertaHojaDeVida(Hueco,
                    $"Hay {hueco} meses sin experiencia registrada entre «{Nombre(anterior.Exp)}» y «{Nombre(orden[i].Exp)}».",
                    $"HV · experiencias {anterior.N} y {orden[i].N}"));
            }
        }

        return alertas;
    }

    /// <summary>Cuánto duró una experiencia (null si las fechas no se entienden) y si sigue en ese trabajo.</summary>
    public static (int? Meses, bool Actual) Duracion(ExperienciaLaboral experiencia, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(experiencia);
        var ahora = hoy.Year * 12 + hoy.Month - 1;
        var desde = Mes(experiencia.Desde, ahora);
        var hasta = Mes(experiencia.Hasta, ahora);
        var actual = hasta == ahora;
        return desde is not null && hasta is not null && hasta >= desde && hasta <= ahora ? (hasta - desde, actual) : (null, actual);
    }

    /// <summary>Meses de experiencia con fechas (sin contar dos veces los periodos que se cruzan).</summary>
    public static int MesesDeExperiencia(DatosCandidato datos, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = hoy.Year * 12 + hoy.Month - 1;
        var periodos = datos.Experiencia.Select(e => (Desde: Mes(e.Desde, ahora), Hasta: Mes(e.Hasta, ahora)))
            .Where(p => p.Desde is not null && p.Hasta is not null && p.Hasta >= p.Desde && p.Hasta <= ahora)
            .Select(p => (Desde: p.Desde!.Value, Hasta: p.Hasta!.Value)).OrderBy(p => p.Desde).ToList();
        var total = 0;
        var fin = int.MinValue;
        foreach (var (desde, hasta) in periodos)
        {
            var inicio = Math.Max(desde, fin);
            if (hasta > inicio)
            {
                total += hasta - inicio;
            }

            fin = Math.Max(fin, hasta);
        }

        return total;
    }

    /// <summary>«2021» → junio de 2021; «03/2021», «2021-03», «marzo de 2021» → ese mes; «actual», «hoy», «presente» → este mes.</summary>
    internal static int? Mes(string? texto, int ahora)
    {
        var t = (texto ?? "").Trim().ToLowerInvariant();
        if (t.Length == 0)
        {
            return null;
        }

        if (t is "actual" or "hoy" or "presente" or "actualidad" or "la fecha" or "a la fecha" or "actualmente" or "vigente")
        {
            return ahora;
        }

        if (MesAnio().Match(t) is { Success: true } ma)
        {
            return Valor(int.Parse(ma.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(ma.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        if (AnioMes().Match(t) is { Success: true } am)
        {
            return Valor(int.Parse(am.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(am.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        if (Anio().Match(t) is not { Success: true } a)
        {
            return null;
        }

        var anio = int.Parse(a.Value, CultureInfo.InvariantCulture);
        var mes = Array.FindIndex(Meses, m => t.Contains(m, StringComparison.Ordinal));
        return Valor(anio, mes >= 0 ? mes + 1 : 6);
    }

    private static int? Valor(int anio, int mes) => anio is >= 1950 and <= 2100 && mes is >= 1 and <= 12 ? anio * 12 + mes - 1 : null;

    private static string Nombre(ExperienciaLaboral e) => string.IsNullOrWhiteSpace(e.Empresa) ? e.Cargo : $"{e.Cargo} · {e.Empresa}";

    [GeneratedRegex(@"\b(\d{1,2})\s*[/\-.]\s*(\d{4})\b")]
    private static partial Regex MesAnio();

    [GeneratedRegex(@"\b(\d{4})\s*[/\-.]\s*(\d{1,2})\b")]
    private static partial Regex AnioMes();

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex Anio();
}

/// <summary>
/// Normaliza cargos y habilidades contra un catálogo propio de sinónimos, y arma el texto de búsqueda del banco de talento.
/// Lo que no está en el catálogo queda como lo escribió el candidato.
/// </summary>
public static class Normalizacion
{
    private static readonly (string Canonico, string[] Sinonimos)[] Habilidades =
    [
        ("Operación de montacargas", ["montacarga", "montacargas", "montacarguista", "operador de montacargas", "operario de montacargas", "manejo de montacargas"]),
        ("Servicio al cliente", ["servicio al cliente", "atencion al cliente", "atencion a clientes", "atencion de clientes"]),
        ("Trabajo en alturas", ["trabajo en alturas", "alturas", "curso de alturas", "certificado de alturas"]),
        ("Manipulación de alimentos", ["manipulacion de alimentos", "manipulador de alimentos", "curso de manipulacion"]),
        ("Primeros auxilios", ["primeros auxilios"]),
        ("Excel", ["excel", "microsoft excel", "hojas de calculo"]),
        ("Inventarios", ["inventario", "inventarios", "control de inventario"]),
        ("Ventas", ["ventas", "venta", "asesoria comercial"]),
        ("Manejo de caja", ["manejo de caja", "caja", "arqueo de caja"]),
        ("Cobranza", ["cobranza", "cobro de cartera", "recuperacion de cartera"]),
        ("Licencia de conducción A2", ["licencia a2", "pase a2", "licencia de conduccion a2", "categoria a2"]),
        ("Licencia de conducción B1", ["licencia b1", "pase b1", "licencia de conduccion b1", "categoria b1"]),
        ("Licencia de conducción C1", ["licencia c1", "pase c1", "licencia de conduccion c1", "categoria c1"]),
        ("Licencia de conducción C2", ["licencia c2", "pase c2", "licencia de conduccion c2", "categoria c2"]),
    ];

    private static readonly (string Canonico, string[] Sinonimos)[] Cargos =
    [
        ("Auxiliar de bodega", ["auxiliar de bodega", "bodeguero", "auxiliar de almacen", "auxiliar de bodegaje"]),
        ("Operador de montacargas", ["montacarguista", "operador de montacargas", "operario de montacargas"]),
        ("Asesor de contact center", ["asesor de call center", "agente de call center", "asesor call center", "teleoperador", "asesor de contact center", "agente de contact center"]),
        ("Asesor de servicio al cliente", ["asesor de servicio al cliente", "asesor de servicio", "agente de servicio al cliente"]),
        ("Cajero", ["cajero", "cajera", "cajero(a)"]),
        ("Mensajero", ["mensajero", "domiciliario", "motorizado"]),
        ("Alistador de pedidos", ["alistador", "alistador de pedidos", "picker", "auxiliar de alistamiento"]),
        ("Operario de producción", ["operario de produccion", "operario", "auxiliar de produccion", "operario de planta"]),
    ];

    /// <summary>Sin tildes, en minúscula y con espacios simples: así se compara y se busca.</summary>
    public static string Simplificar(string? texto)
    {
        var sb = new StringBuilder();
        foreach (var c in (texto ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Habilidad(string texto) => Canonico(texto, Habilidades);

    public static string Cargo(string texto) => Canonico(texto, Cargos);

    /// <summary>Habilidades y cursos del candidato normalizados y sin repetir (las de los cursos también cuentan: «curso de alturas»).</summary>
    public static IReadOnlyList<string> HabilidadesDe(DatosCandidato datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return [.. datos.Habilidades.Concat(datos.Cursos).Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => Habilidad(h)).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Texto en el que busca el banco de talento: cargos, empresas, habilidades, cursos, estudios y ciudad (sin nombre ni documento).</summary>
    public static string TextoBusqueda(DatosCandidato datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var partes = new List<string> { datos.Ciudad };
        foreach (var e in datos.Experiencia)
        {
            partes.Add(e.Cargo);
            partes.Add(Cargo(e.Cargo));
            partes.Add(e.Empresa);
        }

        partes.AddRange(datos.Habilidades);
        partes.AddRange(datos.Cursos);
        partes.AddRange(HabilidadesDe(datos));
        partes.AddRange(datos.Formacion.Select(f => f.Titulo));
        var texto = Simplificar(string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase)));
        return texto.Length <= 4000 ? texto : texto[..4000];
    }

    private static string Canonico(string texto, (string Canonico, string[] Sinonimos)[] catalogo)
    {
        var simple = Simplificar(texto);
        foreach (var (canonico, sinonimos) in catalogo)
        {
            if (sinonimos.Any(s => simple == s || simple.StartsWith(s + " ", StringComparison.Ordinal) || simple.Contains(" " + s, StringComparison.Ordinal)))
            {
                return canonico;
            }
        }

        return texto.Trim();
    }
}
