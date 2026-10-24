using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Selvana.AI.Gateway;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Motor;

public interface IMotorAgentes
{
    Task<Result<ResultadoAgente>> EjecutarAsync(DefinicionAgente agente, SolicitudAgente solicitud, CancellationToken cancellationToken);
}

public static class MotorAgentesErrors
{
    public static readonly Error SolicitudVacia = Error.Validation("Agente.SolicitudVacia", "Dile al agente qué necesitas y sobre qué caso.");
    public static readonly Error SinModelos = Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Intenta de nuevo en unos minutos.");
    public static readonly Error DemasiadosPasos = Error.Failure("Agente.DemasiadosPasos", "El agente no terminó en los pasos permitidos. Revisa la bitácora del caso.");
}

/// <summary>
/// Motor de agentes: conversa con el modelo, ejecuta las herramientas de lectura, convierte las de escritura en propuestas
/// para la bandeja, guarda la memoria del caso y registra cada paso con su costo.
/// </summary>
public sealed class MotorAgentes(IChatClient ia, IMemoriaCasos memorias, IRegistroAccionesAgente registro, TimeProvider reloj) : IMotorAgentes
{
    public const string HerramientaRecordar = "recordar";
    internal const int MaximoHechos = 30;
    private const int LargoDetalle = 1_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly AIFunction Recordar = AIFunctionFactory.Create(
        (string clave, string valor) => "", HerramientaRecordar,
        "Guarda un hecho útil de este caso para la próxima vez (por ejemplo: cupos pendientes, decisión tomada). Nunca guardes salud, edad, género, estado civil ni dirección.");

    private const string Reglas = """

        Reglas del motor de agentes de Selvana:
        - Consulta con las herramientas; no inventes datos.
        - Las herramientas que cambian algo solo proponen: una persona lo aprueba en la bandeja. No repitas una propuesta.
        - Usa la herramienta "recordar" para guardar lo que sirva la próxima vez sobre este caso.
        - Responde en español de Colombia, en pocas frases, y di qué quedó propuesto.
        """;

    public async Task<Result<ResultadoAgente>> EjecutarAsync(DefinicionAgente agente, SolicitudAgente solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agente);
        ArgumentNullException.ThrowIfNull(solicitud);
        if (string.IsNullOrWhiteSpace(solicitud.Mensaje) || string.IsNullOrWhiteSpace(solicitud.Caso))
        {
            return MotorAgentesErrors.SolicitudVacia;
        }

        var ejecucion = new Ejecucion(agente, solicitud, reloj);
        var memoria = await memorias.BuscarAsync(solicitud.EmpresaId, agente.Nombre, solicitud.Caso, cancellationToken).ConfigureAwait(false)
            ?? new MemoriaCaso { EmpresaId = solicitud.EmpresaId, Agente = agente.Nombre, Caso = solicitud.Caso };
        var herramientas = agente.Herramientas.ToDictionary(h => h.Nombre, StringComparer.Ordinal);
        var mensajes = new List<ChatMessage>
        {
            new(ChatRole.System, agente.Instrucciones + Reglas + TextoMemoria(memoria)),
            new(ChatRole.User, solicitud.Mensaje.Trim()),
        };
        var opciones = new ContextoIa(solicitud.EmpresaId, agente.Nombre, agente.ContieneDatosPersonales).AplicarA(new ChatOptions
        {
            MaxOutputTokens = 4_000,
            Tools = [.. agente.Herramientas.Select(h => (AITool)h.Funcion), Recordar],
        });

        try
        {
            for (var paso = 1; paso <= agente.MaximoPasos; paso++)
            {
                var inicio = reloj.GetTimestamp();
                ChatResponse respuesta;
                try
                {
                    respuesta = await ia.GetResponseAsync(mensajes, opciones, cancellationToken).ConfigureAwait(false);
                }
                catch (CadenaModelosAgotadaException error)
                {
                    ejecucion.Registrar(paso, TipoAccionAgente.Error, "cadena", error.Message, inicio);
                    return MotorAgentesErrors.SinModelos;
                }

                var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
                ejecucion.Registrar(paso, TipoAccionAgente.Modelo, respuesta.ModelId ?? "",
                    $"{respuesta.Usage?.InputTokenCount ?? 0} tokens de entrada, {respuesta.Usage?.OutputTokenCount ?? 0} de salida", inicio, costo);
                mensajes.AddRange(respuesta.Messages);

                var llamadas = respuesta.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
                if (llamadas.Count == 0)
                {
                    ejecucion.Registrar(paso, TipoAccionAgente.Respuesta, agente.Nombre, respuesta.Text, inicio);
                    return new ResultadoAgente(ejecucion.Id, respuesta.Text.Trim(), ejecucion.Propuestas, memoria.Hechos, paso,
                        ejecucion.Modelos, ejecucion.CostoUsd);
                }

                var resultados = new List<AIContent>();
                foreach (var llamada in llamadas)
                {
                    var resultado = await UsarHerramientaAsync(llamada, herramientas, memoria, ejecucion, paso, cancellationToken).ConfigureAwait(false);
                    resultados.Add(new FunctionResultContent(llamada.CallId, resultado));
                }

                mensajes.Add(new ChatMessage(ChatRole.Tool, resultados));
            }

            ejecucion.Registrar(agente.MaximoPasos, TipoAccionAgente.Error, agente.Nombre, "Alcanzó el máximo de pasos sin responder.", reloj.GetTimestamp());
            return MotorAgentesErrors.DemasiadosPasos;
        }
        finally
        {
            // La memoria y la bitácora se guardan aunque el agente falle a mitad de camino, sin el token del llamador.
            memoria.ActualizadaEn = reloj.GetUtcNow();
            await memorias.GuardarAsync(memoria, CancellationToken.None).ConfigureAwait(false);
            await registro.RegistrarAsync(ejecucion.Acciones, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<object?> UsarHerramientaAsync(FunctionCallContent llamada, Dictionary<string, HerramientaAgente> herramientas,
        MemoriaCaso memoria, Ejecucion ejecucion, int paso, CancellationToken cancellationToken)
    {
        var inicio = ejecucion.Reloj.GetTimestamp();
        var argumentos = llamada.Arguments ?? new Dictionary<string, object?>();
        var textoArgumentos = Serializar(argumentos);

        if (llamada.Name == HerramientaRecordar)
        {
            var clave = Texto(argumentos, "clave");
            var valor = Texto(argumentos, "valor");
            if (clave.Length == 0 || (memoria.Hechos.Count >= MaximoHechos && !memoria.Hechos.ContainsKey(clave)))
            {
                ejecucion.Registrar(paso, TipoAccionAgente.Error, HerramientaRecordar, $"No se guardó: {textoArgumentos}", inicio);
                return "No se guardó: falta la clave o la memoria del caso está llena.";
            }

            memoria.Hechos[Recortar(clave, 80)] = Recortar(valor, 500);
            ejecucion.Registrar(paso, TipoAccionAgente.Recuerdo, HerramientaRecordar, $"{clave}: {valor}", inicio);
            return "Guardado en la memoria del caso.";
        }

        if (!herramientas.TryGetValue(llamada.Name, out var herramienta))
        {
            ejecucion.Registrar(paso, TipoAccionAgente.Error, llamada.Name, "Herramienta inexistente", inicio);
            return $"La herramienta «{llamada.Name}» no existe.";
        }

        if (herramienta.Riesgo == RiesgoHerramienta.Escritura)
        {
            ejecucion.Propuestas.Add(new AccionPropuesta(herramienta.Nombre, new Dictionary<string, object?>(argumentos)));
            ejecucion.Registrar(paso, TipoAccionAgente.Propuesta, herramienta.Nombre, textoArgumentos, inicio);
            return "Quedó propuesto en la bandeja de aprobaciones; una persona decide. No se ejecutó todavía.";
        }

        try
        {
            var resultado = await herramienta.Funcion.InvokeAsync(new AIFunctionArguments(argumentos), cancellationToken).ConfigureAwait(false);
            ejecucion.Registrar(paso, TipoAccionAgente.Herramienta, herramienta.Nombre, $"{textoArgumentos} → {Serializar(resultado)}", inicio);
            return resultado;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            ejecucion.Registrar(paso, TipoAccionAgente.Error, herramienta.Nombre, $"{textoArgumentos} → {error.Message}", inicio);
            return $"La herramienta falló: {error.Message}";
        }
    }

    internal static string TextoMemoria(MemoriaCaso memoria)
    {
        if (memoria.Hechos.Count == 0)
        {
            return "\nEs la primera vez que trabajas en este caso.";
        }

        var sb = new StringBuilder("\nLo que ya sabes de este caso (de ejecuciones anteriores):");
        foreach (var (clave, valor) in memoria.Hechos)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\n- {clave}: {valor}");
        }

        return sb.ToString();
    }

    private static string Texto(IDictionary<string, object?> argumentos, string nombre) =>
        argumentos.TryGetValue(nombre, out var valor) ? (valor is JsonElement { ValueKind: JsonValueKind.String } e ? e.GetString() : valor?.ToString())?.Trim() ?? "" : "";

    private static string Serializar(object? valor) => valor switch
    {
        null => "",
        string s => s,
        _ => JsonSerializer.Serialize(valor, Json),
    };

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];

    /// <summary>Estado de una ejecución: acciones registradas, propuestas y costo acumulado.</summary>
    private sealed class Ejecucion(DefinicionAgente agente, SolicitudAgente solicitud, TimeProvider reloj)
    {
        public Guid Id { get; } = Guid.CreateVersion7();
        public TimeProvider Reloj { get; } = reloj;
        public List<AccionAgente> Acciones { get; } = [];
        public List<AccionPropuesta> Propuestas { get; } = [];
        public decimal CostoUsd => Acciones.Sum(a => a.CostoUsd);
        public string Modelos => string.Join(", ", Acciones.Where(a => a.Tipo == TipoAccionAgente.Modelo).Select(a => a.Nombre).Distinct());

        public void Registrar(int paso, TipoAccionAgente tipo, string nombre, string detalle, long inicio, decimal costo = 0) =>
            Acciones.Add(new AccionAgente
            {
                EjecucionId = Id,
                EmpresaId = solicitud.EmpresaId,
                Agente = agente.Nombre,
                Caso = solicitud.Caso,
                Paso = paso,
                Tipo = tipo,
                Nombre = Recortar(nombre, 100),
                Detalle = Recortar(detalle, LargoDetalle),
                CostoUsd = costo,
                DuracionMs = (long)Reloj.GetElapsedTime(inicio).TotalMilliseconds,
                OcurridoEn = Reloj.GetUtcNow(),
            });
    }
}
