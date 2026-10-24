using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Selvana.AI.Agentes.Reclutamiento;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Evaluacion;
using Selvana.AI.Persistence;

namespace Selvana.AI.Worker;

/// <summary>
/// Corre el banco de pruebas de los agentes contra los modelos reales y compara con lo esperado.
/// Uso: dotnet run --project src/Workers/Selvana.AI.Worker -- evaluar [lector|redactor|todo] [--max N | --completo]
/// Por defecto corre una muestra mínima (3 casos por agente) para cuidar el presupuesto de IA; el banco completo, con --completo.
/// Guarda el resultado en ia.evaluaciones_agentes y escribe el informe en docs/ia/evaluaciones.
/// </summary>
internal static class EjecutorEvaluacion
{
    /// <summary>Empresa ficticia de las evaluaciones: así su costo no se mezcla con el de los clientes.</summary>
    public static readonly Guid EmpresaEvaluacion = new("e7a1e7a1-0000-4000-8000-00000000e7a1");

    /// <summary>Casos por agente cuando no se pide el banco completo (unos USD 0,05 en total).</summary>
    private const int MuestraMinima = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<int> EjecutarAsync(IServiceProvider services, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var cual = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "todo";
        var max = args.Contains("--completo") ? int.MaxValue
            : args.SkipWhile(a => a != "--max").Skip(1).Select(a => int.TryParse(a, out var n) ? n : (int?)null).FirstOrDefault() ?? MuestraMinima;
        var raiz = BuscarRaiz();
        var cumplen = true;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IaDbContext>();
        await db.Database.MigrateAsync();

        if (cual is "lector" or "todo")
        {
            cumplen &= await LectorAsync(scope.ServiceProvider, db, raiz, max);
        }

        if (cual is "redactor" or "todo")
        {
            cumplen &= await RedactorAsync(scope.ServiceProvider, db, raiz, max);
        }

        return cumplen ? 0 : 2;
    }

    private static async Task<bool> LectorAsync(IServiceProvider sp, IaDbContext db, string raiz, int max)
    {
        var banco = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(raiz, "backend", "bancos-ia", "lector-hojas-de-vida.json"))).RootElement;
        var casos = banco.GetProperty("casos").EnumerateArray().Take(max).Select(c => new CasoHojaDeVida(
            c.GetProperty("id").GetString()!, c.GetProperty("grupo").GetString()!, c.GetProperty("formato").GetString()!, c.GetProperty("texto").GetString()!,
            c.GetProperty("esperado").Deserialize<EsperadoHojaDeVida>(Json)!, [.. c.GetProperty("noDebeAparecer").EnumerateArray().Select(x => x.GetString()!)])).ToList();
        var lector = sp.GetRequiredService<ILectorHojasDeVida>();
        Console.WriteLine($"\nLector de hojas de vida · {casos.Count} casos sintéticos");

        var inicio = DateTimeOffset.UtcNow;
        var resultados = new List<ResultadoCaso>();
        foreach (var caso in casos)
        {
            var r = await lector.LeerAsync(EmpresaEvaluacion, $"{caso.Id}.txt", "text/plain", Encoding.UTF8.GetBytes(caso.Texto), CancellationToken.None);
            var resultado = EvaluadorLector.Evaluar(caso, r.IsSuccess ? r.Value.Datos : null, r.IsFailure ? r.Error.Description : null);
            resultados.Add(resultado);
            Console.WriteLine($"  {(resultado.Fallas.Count == 0 ? "ok   " : "FALLA")} {caso.Id} {caso.Formato,-11} {(r.IsSuccess ? r.Value.Modelo : "")}{(resultado.Fallas.Count > 0 ? " · " + string.Join(" | ", resultado.Fallas) : "")}");
        }

        return await GuardarAsync(db, raiz, "lector-hojas-de-vida", "Lector de hojas de vida", banco, resultados, EvaluadorLector.Metricas(resultados), inicio);
    }

    private static async Task<bool> RedactorAsync(IServiceProvider sp, IaDbContext db, string raiz, int max)
    {
        var banco = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(raiz, "backend", "bancos-ia", "redactor-vacantes.json"))).RootElement;
        var casos = banco.GetProperty("casos").EnumerateArray().Take(max).Select(c => new CasoVacante(c.GetProperty("id").GetString()!, c.GetProperty("frase").GetString()!,
            c.GetProperty("esperado").GetProperty("ciudad").GetString()!, c.GetProperty("esperado").GetProperty("cupos").GetInt32())).ToList();
        var redactor = sp.GetRequiredService<IRedactorVacantes>();
        var filtro = sp.GetRequiredService<IFiltroVariablesProtegidas>();
        var minimo = sp.GetRequiredService<IOptions<RedactorVacantesOptions>>().Value.SalarioMinimoCop;
        Console.WriteLine($"\nRedactor de vacantes · {casos.Count} frases");

        var inicio = DateTimeOffset.UtcNow;
        var resultados = new List<ResultadoCaso>();
        foreach (var caso in casos)
        {
            var r = await redactor.RedactarAsync(new SolicitudBorradorVacante(EmpresaEvaluacion, caso.Frase), CancellationToken.None);
            var resultado = EvaluadorRedactor.Evaluar(caso, r.IsSuccess ? r.Value : null, r.IsFailure ? r.Error.Description : null, minimo, filtro);
            resultados.Add(resultado);
            Console.WriteLine($"  {(resultado.Fallas.Count == 0 ? "ok   " : "FALLA")} {caso.Id} {(r.IsSuccess ? r.Value.Modelo : "")}{(resultado.Fallas.Count > 0 ? " · " + string.Join(" | ", resultado.Fallas) : "")}");
        }

        return await GuardarAsync(db, raiz, "redactor-vacantes", "Redactor de vacantes", banco, resultados, EvaluadorRedactor.Metricas(resultados), inicio);
    }

    private static async Task<bool> GuardarAsync(IaDbContext db, string raiz, string agente, string nombre, JsonElement banco, List<ResultadoCaso> resultados,
        Dictionary<string, double> metricas, DateTimeOffset inicio)
    {
        var metas = banco.GetProperty("meta").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
        var usos = await db.UsosModelo.AsNoTracking().Where(u => u.EmpresaId == EmpresaEvaluacion && u.OcurridoEn >= inicio).ToListAsync();
        var evaluacion = new EvaluacionAgente
        {
            Agente = agente,
            Fecha = DateTimeOffset.UtcNow,
            Casos = resultados.Count,
            Fallidos = resultados.Count(r => r.Fallas.Count > 0),
            Metricas = metricas,
            Metas = metas,
            CumpleMetas = metas.All(m => metricas.GetValueOrDefault(m.Key) >= m.Value),
            CostoUsd = usos.Sum(u => u.CostoUsd),
            Modelos = string.Join(", ", usos.Where(u => u.Exito).GroupBy(u => u.Modelo).Select(g => $"{g.Key} ×{g.Count()}")),
            Errores = [.. resultados.Where(r => r.Fallas.Count > 0).Select(r => $"{r.Id}: {string.Join(" | ", r.Fallas)}")],
        };
        db.Evaluaciones.Add(evaluacion);
        await db.SaveChangesAsync();

        Console.WriteLine();
        foreach (var (clave, valor) in metricas.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            var meta = metas.TryGetValue(clave, out var x) ? $"  meta {x:P0} {(valor >= x ? "✔" : "✘")}" : "";
            Console.WriteLine($"  {clave,-28} {valor,7:P1}{meta}");
        }

        Console.WriteLine($"  Costo: USD {evaluacion.CostoUsd.ToString("0.0000", CultureInfo.InvariantCulture)} · Modelos: {evaluacion.Modelos}");
        Console.WriteLine(evaluacion.CumpleMetas ? "  CUMPLE LAS METAS" : "  NO CUMPLE LAS METAS");
        await EscribirInformeAsync(raiz, nombre, evaluacion);
        return evaluacion.CumpleMetas;
    }

    private static async Task EscribirInformeAsync(string raiz, string nombre, EvaluacionAgente e)
    {
        var carpeta = Path.Combine(raiz, "docs", "ia", "evaluaciones");
        Directory.CreateDirectory(carpeta);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Evaluación · {nombre}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Fecha: {e.Fecha:yyyy-MM-dd HH:mm} UTC · {e.Casos} casos sintéticos · {e.Fallidos} con alguna falla");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Resultado: **{(e.CumpleMetas ? "cumple las metas" : "no cumple las metas")}**");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Costo: USD {e.CostoUsd.ToString("0.0000", CultureInfo.InvariantCulture)} · Modelos: {e.Modelos}");
        sb.AppendLine();
        sb.AppendLine("| Métrica | Valor | Meta |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var (clave, valor) in e.Metricas.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            var meta = e.Metas.TryGetValue(clave, out var x) ? $"{x:P0} {(valor >= x ? "✔" : "✘")}" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {clave} | {valor:P1} | {meta} |");
        }

        if (e.Errores.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Casos con fallas");
            sb.AppendLine();
            foreach (var error in e.Errores)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {error}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Generado por `dotnet run --project backend/src/Workers/Selvana.AI.Worker -- evaluar`. El historial completo está en `ia.evaluaciones_agentes`.");
        await File.WriteAllTextAsync(Path.Combine(carpeta, $"{e.Agente}.md"), sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>La raíz del repositorio: la carpeta que contiene backend/bancos-ia.</summary>
    private static string BuscarRaiz()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend", "bancos-ia")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No encontré backend/bancos-ia: ejecute el comando dentro del repositorio.");
    }
}
