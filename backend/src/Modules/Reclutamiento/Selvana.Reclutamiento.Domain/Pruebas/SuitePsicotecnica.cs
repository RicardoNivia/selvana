namespace Selvana.Reclutamiento.Domain.Pruebas;

/// <summary>Cómo se califica: aptitud (una respuesta correcta), juicio situacional (opciones con 0 a 2 puntos) o personalidad (escala 1 a 5 por dimensión).</summary>
public enum TipoPrueba
{
    Aptitud,
    JuicioSituacional,
    Personalidad,
}

public sealed record OpcionPregunta(string Id, string Texto, int Puntaje);

/// <param name="Dimension">Solo en personalidad: a qué rasgo suma la pregunta.</param>
/// <param name="Inversa">Solo en personalidad: «muy de acuerdo» resta en vez de sumar.</param>
public sealed record PreguntaPrueba(string Id, string Texto, IReadOnlyList<OpcionPregunta> Opciones, string? Dimension = null, bool Inversa = false);

/// <param name="Validada">El psicólogo la validó con datos colombianos (ADR 0011). Si no, se aplica pero su puntaje es provisional.</param>
/// <param name="Sensible">Sus resultados son datos sensibles (Ley 1581): el detalle solo lo ve quien tiene VerDatosSensibles.</param>
public sealed record PruebaDefinicion(
    string Codigo, string Version, string Nombre, string Mide, string Instrucciones, TipoPrueba Tipo, int Minutos, bool Validada, bool Sensible,
    IReadOnlyList<PreguntaPrueba> Preguntas);

/// <summary>
/// Suite psicotécnica propia de Selvana, versión básica (ADR 0011, fase 1). Vive versionada en el código: cada cambio pasa por la
/// revisión del psicólogo del equipo. Las preguntas de personalidad son una adaptación al español del Mini-IPIP (International
/// Personality Item Pool, de dominio público). Ninguna está validada todavía con datos colombianos.
/// </summary>
public static class SuitePsicotecnica
{
    private static OpcionPregunta[] Opciones(int correcta, params string[] textos) =>
        [.. textos.Select((t, i) => new OpcionPregunta(((char)('a' + i)).ToString(), t, i == correcta ? 1 : 0))];

    private static OpcionPregunta[] Situacional(params (string Texto, int Puntaje)[] opciones) =>
        [.. opciones.Select((o, i) => new OpcionPregunta(((char)('a' + i)).ToString(), o.Texto, o.Puntaje))];

    private static readonly OpcionPregunta[] Likert =
    [
        new("1", "Muy en desacuerdo", 1), new("2", "En desacuerdo", 2), new("3", "Ni de acuerdo ni en desacuerdo", 3),
        new("4", "De acuerdo", 4), new("5", "Muy de acuerdo", 5),
    ];

    public static readonly PruebaDefinicion Atencion = new(
        "ATENCION", "1.0", "Atención y precisión", "Fijarse en los detalles y comparar datos sin equivocarse",
        "Compara cada par de datos y marca si son exactamente iguales. Trabaja rápido pero con cuidado.", TipoPrueba.Aptitud, 4, false, false,
        [
            new("a1", "CC 1.023.456.789  —  CC 1.023.465.789", Opciones(1, "Iguales", "Diferentes")),
            new("a2", "Calle 45 # 12-30, Funza  —  Calle 45 # 12-30, Funza", Opciones(0, "Iguales", "Diferentes")),
            new("a3", "REF-88341-B  —  REF-88314-B", Opciones(1, "Iguales", "Diferentes")),
            new("a4", "$1.750.905  —  $1.750.905", Opciones(0, "Iguales", "Diferentes")),
            new("a5", "Lote 2026-10-A7  —  Lote 2026-10-A7", Opciones(0, "Iguales", "Diferentes")),
            new("a6", "300 412 7781  —  300 412 7187", Opciones(1, "Iguales", "Diferentes")),
            new("a7", "Montacargas eléctrico 2,5 t  —  Montacargas eléctrico 2.5 t", Opciones(1, "Iguales", "Diferentes")),
            new("a8", "NIT 900.412.887-9  —  NIT 900.412.887-9", Opciones(0, "Iguales", "Diferentes")),
            new("a9", "Pasillo C, estante 14, nivel 3  —  Pasillo C, estante 14, nivel 8", Opciones(1, "Iguales", "Diferentes")),
            new("a10", "Orden 55021 · 48 cajas  —  Orden 55021 · 48 cajas", Opciones(0, "Iguales", "Diferentes")),
        ]);

    public static readonly PruebaDefinicion Numerico = new(
        "NUMERICO", "1.0", "Razonamiento numérico básico", "Resolver cálculos sencillos del día a día del trabajo",
        "Elige la respuesta correcta. Puedes usar papel; no uses calculadora.", TipoPrueba.Aptitud, 8, false, false,
        [
            new("n1", "Un turno de 8 horas empieza a las 10:00 p. m. ¿A qué hora termina?", Opciones(2, "4:00 a. m.", "5:00 a. m.", "6:00 a. m.", "7:00 a. m.")),
            new("n2", "Una estiba lleva 6 filas de 8 cajas. ¿Cuántas cajas hay en 3 estibas?", Opciones(1, "96", "144", "48", "132")),
            new("n3", "Un producto cuesta $40.000 y tiene 25 % de descuento. ¿Cuánto se paga?", Opciones(0, "$30.000", "$35.000", "$25.000", "$32.500")),
            new("n4", "Atiendes 12 llamadas por hora. ¿Cuántas atiendes en una jornada de 7 horas y media?", Opciones(3, "84", "86", "96", "90")),
            new("n5", "De 250 unidades, 15 llegaron dañadas. ¿Qué porcentaje está dañado?", Opciones(2, "5 %", "15 %", "6 %", "1,5 %")),
            new("n6", "Si 3 operarios descargan un camión en 2 horas, ¿cuánto tardan 6 operarios al mismo ritmo?", Opciones(0, "1 hora", "4 horas", "3 horas", "30 minutos")),
            new("n7", "Recibes $100.000 para pagar $63.500. ¿Cuánto devuelves?", Opciones(1, "$37.500", "$36.500", "$46.500", "$35.500")),
            new("n8", "Un pedido de 1.200 unidades se reparte en partes iguales en 8 tiendas. ¿Cuántas recibe cada una?", Opciones(3, "120", "160", "175", "150")),
        ]);

    public static readonly PruebaDefinicion ServicioCliente = new(
        "SJT-SERVICIO", "1.0", "Juicio situacional: servicio al cliente", "Cómo actúa frente a situaciones reales de atención a clientes",
        "Lee cada situación y elige lo que harías. No hay trucos: responde como actuarías de verdad.", TipoPrueba.JuicioSituacional, 10, false, false,
        [
            new("s1", "Un cliente llama molesto porque su pedido llegó incompleto por segunda vez.", Situacional(
                ("Le explico que el error fue de bodega y que no depende de mí.", 0),
                ("Escucho su queja, me disculpo, reviso el caso y le doy una solución con fecha concreta.", 2),
                ("Le pido que escriba al correo de reclamos para que lo atiendan allá.", 1))),
            new("s2", "Tienes una fila larga y un cliente pide algo que no sabes resolver.", Situacional(
                ("Le digo que no sé y sigo con el siguiente.", 0),
                ("Le invento una respuesta para no demorar la fila.", 0),
                ("Le digo que voy a confirmar, pido apoyo a un compañero o supervisor y le respondo.", 2))),
            new("s3", "Un cliente te insulta durante la llamada.", Situacional(
                ("Mantengo la calma, le pido respetuosamente que hablemos sin ofensas y sigo ayudándolo; si continúa, aplico el protocolo.", 2),
                ("Le cuelgo de inmediato.", 0),
                ("Le respondo en el mismo tono para que entienda.", 0))),
            new("s4", "Te das cuenta de que le diste a un cliente una información equivocada ayer.", Situacional(
                ("No digo nada para que no se note el error.", 0),
                ("Lo contacto, le aclaro la información correcta y le ofrezco una solución.", 2),
                ("Le cuento a un compañero para que él lo llame.", 1))),
            new("s5", "Un cliente pide un descuento que no estás autorizado a dar.", Situacional(
                ("Se lo doy para que quede contento.", 0),
                ("Le explico con amabilidad que no puedo autorizarlo y le ofrezco las opciones que sí existen.", 2),
                ("Le digo que no y termino la conversación.", 1))),
            new("s6", "Faltan 5 minutos para terminar tu turno y entra un cliente con un caso largo.", Situacional(
                ("Lo atiendo y, si no alcanzo, dejo el caso documentado y se lo entrego a quien sigue.", 2),
                ("Le pido que vuelva mañana.", 0),
                ("Lo atiendo rápido aunque quede sin resolver.", 1))),
        ]);

    public static readonly PruebaDefinicion Personalidad = new(
        "PERSONALIDAD", "1.0", "Personalidad laboral", "Cinco grandes rasgos de personalidad en el trabajo (adaptación del Mini-IPIP)",
        "Indica qué tanto te describe cada frase tal como eres hoy, no como te gustaría ser. No hay respuestas buenas ni malas.",
        TipoPrueba.Personalidad, 6, false, true,
        [
            new("p1", "Animo el ambiente en las reuniones.", Likert, "Extraversión"),
            new("p2", "Me pongo en el lugar de los demás.", Likert, "Amabilidad"),
            new("p3", "Hago mis tareas de inmediato.", Likert, "Responsabilidad"),
            new("p4", "Tengo cambios de ánimo frecuentes.", Likert, "Estabilidad emocional", Inversa: true),
            new("p5", "Tengo una imaginación muy viva.", Likert, "Apertura"),
            new("p6", "Hablo poco.", Likert, "Extraversión", Inversa: true),
            new("p7", "Me interesan poco los problemas de los demás.", Likert, "Amabilidad", Inversa: true),
            new("p8", "Con frecuencia dejo las cosas fuera de su lugar.", Likert, "Responsabilidad", Inversa: true),
            new("p9", "Estoy tranquilo la mayor parte del tiempo.", Likert, "Estabilidad emocional"),
            new("p10", "Me interesan poco las ideas abstractas.", Likert, "Apertura", Inversa: true),
            new("p11", "En una reunión converso con muchas personas distintas.", Likert, "Extraversión"),
            new("p12", "Siento las emociones de los demás.", Likert, "Amabilidad"),
            new("p13", "Me gusta el orden.", Likert, "Responsabilidad"),
            new("p14", "Me altero con facilidad.", Likert, "Estabilidad emocional", Inversa: true),
            new("p15", "Me cuesta entender ideas abstractas.", Likert, "Apertura", Inversa: true),
            new("p16", "Prefiero pasar desapercibido.", Likert, "Extraversión", Inversa: true),
            new("p17", "En realidad no me interesan mucho los demás.", Likert, "Amabilidad", Inversa: true),
            new("p18", "Desordeno las cosas.", Likert, "Responsabilidad", Inversa: true),
            new("p19", "Rara vez me siento triste.", Likert, "Estabilidad emocional"),
            new("p20", "No tengo mucha imaginación.", Likert, "Apertura", Inversa: true),
        ]);

    public static IReadOnlyList<PruebaDefinicion> Todas { get; } = [Atencion, Numerico, ServicioCliente, Personalidad];

    public static PruebaDefinicion? Buscar(string codigo) => Todas.FirstOrDefault(p => p.Codigo == codigo);
}

public sealed record DimensionResultado(string Nombre, int Puntaje);

/// <summary>Puntaje de 0 a 100. En personalidad no hay un total: cada dimensión de 0 a 100.</summary>
public sealed record Calificacion(int? Puntaje, IReadOnlyList<DimensionResultado> Dimensiones, int Respondidas, int Total);

public static class CalificadorPruebas
{
    /// <param name="respuestas">Pregunta → opción elegida. Las que no se respondieron cuentan como 0 en aptitud y se omiten en personalidad.</param>
    public static Calificacion Calificar(PruebaDefinicion prueba, IReadOnlyDictionary<string, string> respuestas)
    {
        ArgumentNullException.ThrowIfNull(prueba);
        ArgumentNullException.ThrowIfNull(respuestas);
        var respondidas = prueba.Preguntas.Count(p => respuestas.ContainsKey(p.Id));

        if (prueba.Tipo == TipoPrueba.Personalidad)
        {
            var dimensiones = prueba.Preguntas.Where(p => p.Dimension is not null).GroupBy(p => p.Dimension!).Select(g =>
            {
                var valores = g.Where(p => respuestas.ContainsKey(p.Id))
                    .Select(p => (p, opcion: p.Opciones.FirstOrDefault(o => o.Id == respuestas[p.Id])))
                    .Where(x => x.opcion is not null)
                    .Select(x => x.p.Inversa ? 6 - x.opcion!.Puntaje : x.opcion!.Puntaje).ToList();
                // Media de 1 a 5 llevada a 0-100
                var puntaje = valores.Count == 0 ? 0 : (int)Math.Round((valores.Average() - 1) / 4 * 100);
                return new DimensionResultado(g.Key, puntaje);
            }).ToList();
            return new Calificacion(null, dimensiones, respondidas, prueba.Preguntas.Count);
        }

        var maximo = prueba.Preguntas.Sum(p => p.Opciones.Max(o => o.Puntaje));
        var obtenido = prueba.Preguntas.Sum(p => respuestas.TryGetValue(p.Id, out var id) ? p.Opciones.FirstOrDefault(o => o.Id == id)?.Puntaje ?? 0 : 0);
        return new Calificacion(maximo == 0 ? 0 : (int)Math.Round(obtenido * 100.0 / maximo), [], respondidas, prueba.Preguntas.Count);
    }
}
