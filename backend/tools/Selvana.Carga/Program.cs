using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

// Sprint 10 · Prueba de carga de la selección masiva. Entra con un usuario de la consola, busca la vacante de selección masiva
// (la crea la API con Carga:Postulaciones) y golpea en paralelo las lecturas de la selección durante unos segundos.
// Reporta p50, p95 y máximo por endpoint; sale con código 1 si hay errores o si algún p95 pasa el umbral.
//
//   dotnet run --project tools/Selvana.Carga -- --url http://localhost:5000 --correo valentina.rios@talentoandino.co --contrasena <clave>
//   Opciones: --concurrencia 8 · --segundos 20 · --umbral-ms 1500 · --vacante <id>

var opciones = Opciones.Leer(args);
using var manejador = new HttpClientHandler { CookieContainer = new CookieContainer() };
using var http = new HttpClient(manejador) { BaseAddress = new Uri(opciones.Url), Timeout = TimeSpan.FromSeconds(60) };

var ingreso = await http.PostAsJsonAsync("/api/v1/sesion/ingresar", new { correo = opciones.Correo, contrasena = opciones.Contrasena });
if (!ingreso.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"No se pudo entrar con {opciones.Correo}: {(int)ingreso.StatusCode}");
    return 1;
}

var vacante = opciones.Vacante ?? await BuscarVacanteAsync(http);
if (vacante is null)
{
    Console.Error.WriteLine("No está la vacante de selección masiva: arranque la API con Ejemplo__Sembrar=true y Carga__Postulaciones=1000.");
    return 1;
}

var postulaciones = await ContarAsync(http, $"/api/v1/reclutamiento/vacantes/{vacante}/proceso?etapa=Recibida&tamano=1");
Console.WriteLine($"Vacante {vacante} · {postulaciones} recibidas · {opciones.Concurrencia} en paralelo durante {opciones.Segundos} s");

string[] rutas =
[
    $"/api/v1/reclutamiento/vacantes/{vacante}/proceso?tamano=25",
    $"/api/v1/reclutamiento/vacantes/{vacante}/proceso?etapa=Recibida&porDecidir=true&tamano=25",
    $"/api/v1/reclutamiento/vacantes/{vacante}/proceso?pagina=5&tamano=50&buscar=a",
    $"/api/v1/reclutamiento/vacantes/{vacante}/tablero?porColumna=20",
    $"/api/v1/reclutamiento/vacantes/{vacante}/postulantes?pagina=1&tamano=25",
    $"/api/v1/reclutamiento/vacantes/{vacante}/filtros",
    $"/api/v1/reclutamiento/vacantes/{vacante}",
    "/api/v1/reclutamiento/tablero-seleccion?dias=90",
    "/api/v1/reclutamiento/vacantes?pagina=1&tamano=25",
];

var tiempos = rutas.ToDictionary(r => r, _ => new List<double>());
var errores = rutas.ToDictionary(r => r, _ => 0);
var candado = new object();
var fin = Stopwatch.StartNew();
var trabajadores = Enumerable.Range(0, opciones.Concurrencia).Select(async n =>
{
    var i = n;
    while (fin.Elapsed.TotalSeconds < opciones.Segundos)
    {
        var ruta = rutas[i++ % rutas.Length];
        var reloj = Stopwatch.StartNew();
        bool ok;
        try
        {
            using var r = await http.GetAsync(ruta);
            _ = await r.Content.ReadAsByteArrayAsync();
            ok = r.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            ok = false;
        }
        catch (TaskCanceledException)
        {
            ok = false;
        }

        lock (candado)
        {
            tiempos[ruta].Add(reloj.Elapsed.TotalMilliseconds);
            if (!ok)
            {
                errores[ruta]++;
            }
        }
    }
});
await Task.WhenAll(trabajadores);

var falla = false;
var total = tiempos.Values.Sum(t => t.Count);
Console.WriteLine();
Console.WriteLine($"{"Endpoint",-82} {"N",5} {"p50",7} {"p95",7} {"máx",7} {"err",4}");
foreach (var ruta in rutas)
{
    var t = tiempos[ruta].Order().ToList();
    if (t.Count == 0)
    {
        continue;
    }

    var p95 = Percentil(t, 0.95);
    var lento = p95 > opciones.UmbralMs;
    falla |= lento || errores[ruta] > 0;
    var nombre = ruta.Replace(vacante.ToString()!, "{id}", StringComparison.Ordinal);
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{(nombre.Length > 80 ? nombre[..80] : nombre),-82} {t.Count,5} {Percentil(t, 0.5),7:0} {p95,7:0} {t[^1],7:0} {errores[ruta],4}{(lento ? "  LENTO" : "")}"));
}

Console.WriteLine();
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{total} peticiones en {fin.Elapsed.TotalSeconds:0.0} s · {total / fin.Elapsed.TotalSeconds:0.0} por segundo"));
Console.WriteLine(falla ? $"FALLA: hay errores o algún p95 supera {opciones.UmbralMs} ms." : $"OK: sin errores y todos los p95 bajo {opciones.UmbralMs} ms.");
return falla ? 1 : 0;

static double Percentil(List<double> ordenados, double p) => ordenados[Math.Clamp((int)Math.Ceiling(p * ordenados.Count) - 1, 0, ordenados.Count - 1)];

static async Task<Guid?> BuscarVacanteAsync(HttpClient http)
{
    using var r = await http.GetAsync("/api/v1/reclutamiento/vacantes?pagina=1&tamano=10&buscar=selecci%C3%B3n%20masiva");
    if (!r.IsSuccessStatusCode)
    {
        return null;
    }

    using var json = JsonDocument.Parse(await r.Content.ReadAsStreamAsync());
    return json.RootElement.GetProperty("items").EnumerateArray().Select(v => (Guid?)v.GetProperty("id").GetGuid()).FirstOrDefault();
}

static async Task<int> ContarAsync(HttpClient http, string ruta)
{
    using var r = await http.GetAsync(ruta);
    if (!r.IsSuccessStatusCode)
    {
        return 0;
    }

    using var json = JsonDocument.Parse(await r.Content.ReadAsStreamAsync());
    return json.RootElement.GetProperty("personas").GetProperty("total").GetInt32();
}

internal sealed record Opciones(string Url, string Correo, string Contrasena, int Concurrencia, int Segundos, double UmbralMs, Guid? Vacante)
{
    public static Opciones Leer(string[] args)
    {
        string? Valor(string nombre)
        {
            var i = Array.IndexOf(args, $"--{nombre}");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        return new Opciones(
            Valor("url") ?? "http://localhost:5000",
            Valor("correo") ?? "valentina.rios@talentoandino.co",
            Valor("contrasena") ?? Environment.GetEnvironmentVariable("SELVANA_CARGA_CONTRASENA") ?? "",
            int.TryParse(Valor("concurrencia"), out var c) ? Math.Clamp(c, 1, 64) : 8,
            int.TryParse(Valor("segundos"), out var s) ? Math.Clamp(s, 5, 600) : 20,
            double.TryParse(Valor("umbral-ms"), CultureInfo.InvariantCulture, out var u) ? u : 1500,
            Guid.TryParse(Valor("vacante"), out var v) ? v : null);
    }
}
