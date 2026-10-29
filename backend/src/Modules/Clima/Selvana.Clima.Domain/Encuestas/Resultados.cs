namespace Selvana.Clima.Domain.Encuestas;

public sealed record ResultadoDimension(string Dimension, decimal Promedio, int Favorables);

public sealed record ResultadoGrupo(string Tipo, string Nombre, int Respuestas, decimal? Promedio, int? Enps, IReadOnlyList<ResultadoDimension> Dimensiones);

public sealed record ResultadosEncuesta(int Respuestas, decimal? Promedio, int? Enps, int? Promotores, int? Detractores, IReadOnlyList<ResultadoDimension> Dimensiones,
    IReadOnlyList<ResultadoGrupo> Grupos, int GruposOcultos);

/// <summary>Plantilla base de clima (camino sin IA): seis dimensiones con afirmaciones probadas; RRHH las edita antes de abrir.</summary>
public static class PlantillaClima
{
    public static IReadOnlyList<PreguntaClima> Preguntas() =>
    [
        new() { Id = "lid1", Dimension = "Liderazgo", Texto = "Mi líder me da retroalimentación útil sobre mi trabajo." },
        new() { Id = "lid2", Dimension = "Liderazgo", Texto = "Mi líder me trata con respeto y es justo con el equipo." },
        new() { Id = "com1", Dimension = "Comunicación", Texto = "Me entero a tiempo de lo que pasa en la empresa y me afecta." },
        new() { Id = "rec1", Dimension = "Reconocimiento", Texto = "Cuando hago bien mi trabajo, me lo reconocen." },
        new() { Id = "con1", Dimension = "Condiciones de trabajo", Texto = "Tengo las herramientas y los recursos para hacer bien mi trabajo." },
        new() { Id = "con2", Dimension = "Condiciones de trabajo", Texto = "Mis turnos y mi carga de trabajo son razonables." },
        new() { Id = "cre1", Dimension = "Crecimiento", Texto = "Veo posibilidades de aprender y crecer en esta empresa." },
        new() { Id = "bie1", Dimension = "Bienestar", Texto = "Me siento seguro y cuidado en mi lugar de trabajo." },
        new() { Id = "per1", Dimension = "Pertenencia", Texto = "Me siento orgulloso de trabajar aquí." },
    ];

    public const string PreguntaAbierta = "¿Qué es lo que más te gusta de trabajar aquí y qué cambiarías?";
}

public static class CalculoResultados
{
    /// <summary>
    /// Promedios por dimensión (y cuántos respondieron 4 o 5), eNPS (promotores 9-10 menos detractores 0-6) y los mismos datos por área y por sede,
    /// solo para grupos con al menos <see cref="Encuesta.MinimoGrupo"/> respuestas: así nadie queda expuesto.
    /// </summary>
    public static ResultadosEncuesta Calcular(Encuesta e, IReadOnlyList<RespuestaClima> respuestas)
    {
        if (respuestas.Count < Encuesta.MinimoGrupo)
        {
            return new ResultadosEncuesta(respuestas.Count, null, null, null, null, [], [], 0);
        }

        var (promedio, dimensiones) = Dimensiones(e, respuestas);
        var (enps, promotores, detractores) = Enps(respuestas);
        var grupos = new List<ResultadoGrupo>();
        var ocultos = 0;
        foreach (var (tipo, clave) in new (string, Func<RespuestaClima, string>)[] { ("Área", r => r.Area), ("Sede", r => r.Sede) })
        {
            foreach (var g in respuestas.Where(r => clave(r).Length > 0).GroupBy(clave).OrderBy(g => g.Key))
            {
                if (g.Count() < Encuesta.MinimoGrupo)
                {
                    ocultos++;
                    continue;
                }

                var lista = g.ToList();
                var (p, d) = Dimensiones(e, lista);
                grupos.Add(new ResultadoGrupo(tipo, g.Key, lista.Count, p, Enps(lista).Enps, d));
            }
        }

        return new ResultadosEncuesta(respuestas.Count, promedio, enps, promotores, detractores, dimensiones, grupos, ocultos);
    }

    private static (decimal? Promedio, List<ResultadoDimension> Dimensiones) Dimensiones(Encuesta e, IReadOnlyList<RespuestaClima> respuestas)
    {
        var dimensiones = e.Preguntas.GroupBy(p => p.Dimension).Select(g =>
        {
            var ids = g.Select(p => p.Id).ToHashSet();
            var valores = respuestas.SelectMany(r => r.Puntajes.Where(x => ids.Contains(x.PreguntaId)).Select(x => x.Valor)).ToList();
            return valores.Count == 0 ? null
                : new ResultadoDimension(g.Key, Math.Round((decimal)valores.Average(), 2), (int)Math.Round(100.0 * valores.Count(v => v >= 4) / valores.Count));
        }).OfType<ResultadoDimension>().ToList();
        return (dimensiones.Count == 0 ? null : Math.Round(dimensiones.Average(d => d.Promedio), 2), dimensiones);
    }

    private static (int? Enps, int? Promotores, int? Detractores) Enps(IReadOnlyList<RespuestaClima> respuestas)
    {
        var notas = respuestas.Where(r => r.Enps is not null).Select(r => r.Enps!.Value).ToList();
        if (notas.Count < Encuesta.MinimoGrupo)
        {
            return (null, null, null);
        }

        var promotores = (int)Math.Round(100.0 * notas.Count(n => n >= 9) / notas.Count);
        var detractores = (int)Math.Round(100.0 * notas.Count(n => n <= 6) / notas.Count);
        return (promotores - detractores, promotores, detractores);
    }
}

/// <summary>
/// C2 · Camino sin IA para los comentarios: los agrupa por temas con palabras clave y estima el tono con palabras positivas y negativas.
/// Es más gruesa que la IA, pero nunca deja a RRHH sin análisis.
/// </summary>
public static class TemasPorReglas
{
    private static readonly (string Tema, string[] Palabras, string Accion)[] Temas =
    [
        ("Salario y beneficios", ["salario", "sueldo", "pago", "bonific", "prima", "aumento", "beneficio", "plata"], "Revise la equidad salarial del cargo y explique la política de compensación."),
        ("Liderazgo y trato", ["jefe", "líder", "lider", "supervisor", "trato", "respeto", "grita", "coordinador"], "Converse con los líderes del área y refuerce la formación en liderazgo."),
        ("Turnos y carga de trabajo", ["turno", "horario", "carga", "extra", "descanso", "domingo", "noche", "cansad"], "Revise la programación de turnos y las horas extra del área."),
        ("Comunicación", ["comunica", "informa", "entera", "avisa", "claridad", "reunión"], "Defina un canal fijo para contar los cambios a tiempo."),
        ("Crecimiento y formación", ["crecer", "ascenso", "capacita", "aprender", "curso", "carrera", "oportunidad"], "Publique las vacantes internas y arme planes de formación por cargo."),
        ("Herramientas y condiciones", ["herramienta", "equipo", "dotación", "uniforme", "baño", "comedor", "computador", "máquina"], "Haga un inventario de las herramientas y condiciones que faltan por sede."),
        ("Compañerismo y ambiente", ["compañer", "ambiente", "equipo de trabajo", "amig", "familia", "unión"], "Reconozca lo que funciona del ambiente y compártalo con otras áreas."),
    ];

    private static readonly string[] Negativas = ["no ", "nunca", "mal", "falta", "poco", "injust", "cansad", "grita", "difícil", "dificil", "demora", "nadie"];
    private static readonly string[] Positivas = ["bien", "bueno", "buena", "excelente", "gusta", "agradezco", "feliz", "apoyo", "orgullo", "mejor"];

    public static (List<TemaClima> Temas, string Resumen) Analizar(IReadOnlyList<string> comentarios)
    {
        var textos = comentarios.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
        var temas = new List<TemaClima>();
        foreach (var (tema, palabras, accion) in Temas)
        {
            var del = textos.Where(t => palabras.Any(p => t.Contains(p, StringComparison.OrdinalIgnoreCase))).ToList();
            if (del.Count == 0)
            {
                continue;
            }

            var negativos = del.Count(t => Negativas.Any(n => t.Contains(n, StringComparison.OrdinalIgnoreCase)));
            var positivos = del.Count(t => Positivas.Any(n => t.Contains(n, StringComparison.OrdinalIgnoreCase)));
            var sentimiento = negativos > positivos * 1.5 ? "Negativo" : positivos > negativos * 1.5 ? "Positivo" : "Mixto";
            temas.Add(new TemaClima
            {
                Nombre = tema, Sentimiento = sentimiento, Cantidad = del.Count, Accion = accion,
                Citas = [.. del.Take(2).Select(t => t.Length > 140 ? t[..140] + "…" : t)],
            });
        }

        temas = [.. temas.OrderByDescending(t => t.Cantidad)];
        var resumen = temas.Count == 0
            ? "Los comentarios no se agrupan en temas claros. Léalos directamente."
            : $"De {textos.Count} comentarios, lo más mencionado es {temas[0].Nombre.ToLowerInvariant()} ({temas[0].Cantidad}), con tono {temas[0].Sentimiento.ToLowerInvariant()}.";
        return (temas, resumen);
    }
}
