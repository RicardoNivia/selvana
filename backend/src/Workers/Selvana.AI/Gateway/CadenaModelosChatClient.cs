using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Selvana.AI.Gateway;

/// <summary>Un modelo de la cadena con su cliente y el momento hasta el que se omite tras una falla.</summary>
public sealed class EslabonCadena(EslabonModeloOptions opciones, IChatClient cliente)
{
    private long _omitirHastaTicks;

    public EslabonModeloOptions Opciones { get; } = opciones;
    public IChatClient Cliente { get; } = cliente;

    public bool OmitidoEn(DateTimeOffset ahora) => ahora.UtcTicks < Interlocked.Read(ref _omitirHastaTicks);

    public void OmitirHasta(DateTimeOffset momento) => Interlocked.Exchange(ref _omitirHastaTicks, momento.UtcTicks);

    public override string ToString() => Opciones.ToString();
}

/// <summary>
/// Gateway de modelos: prueba cada modelo en orden (gratis → pago → respaldo) y devuelve la primera respuesta.
/// Registra cada intento con tokens y costo. Los modelos gratis nunca reciben datos personales (Ley 1581).
/// </summary>
public sealed partial class CadenaModelosChatClient(
    IReadOnlyList<EslabonCadena> eslabones,
    IRegistroUsoIa registro,
    IClasificadorFallas clasificador,
    IOptions<IaOptions> options,
    TimeProvider reloj,
    ILogger<CadenaModelosChatClient> logger) : IChatClient
{
    public const string PropiedadProveedor = "selvana.proveedor";
    public const string PropiedadCostoUsd = "selvana.costo_usd";
    public const string PropiedadModelo = "selvana.modelo";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var mensajes = messages as IList<ChatMessage> ?? [.. messages];
        var contexto = ContextoIa.De(options);
        var fallas = new List<string>();
        var intento = 0;

        foreach (var eslabon in eslabones)
        {
            if (contexto.ContieneDatosPersonales && eslabon.Opciones.Nivel == NivelModelo.Gratis)
            {
                continue;
            }

            if (eslabon.OmitidoEn(reloj.GetUtcNow()))
            {
                fallas.Add($"{eslabon}: omitido por una falla reciente");
                continue;
            }

            intento++;
            var inicio = reloj.GetTimestamp();
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(TiempoMaximo);

            try
            {
                var opciones = options?.Clone() ?? new ChatOptions();
                opciones.ModelId = eslabon.Opciones.Modelo;

                var respuesta = await eslabon.Cliente.GetResponseAsync(ParaModelo(mensajes, eslabon.Opciones.Modelo), opciones, limite.Token)
                    .ConfigureAwait(false);

                var entrada = respuesta.Usage?.InputTokenCount ?? 0;
                var salida = respuesta.Usage?.OutputTokenCount ?? 0;
                var costo = eslabon.Opciones.CalcularCostoUsd(entrada, salida);

                await RegistrarAsync(contexto, eslabon, intento, null, entrada, salida, costo, inicio).ConfigureAwait(false);

                respuesta.ModelId ??= eslabon.Opciones.Modelo;
                respuesta.AdditionalProperties ??= [];
                respuesta.AdditionalProperties[PropiedadProveedor] = eslabon.Opciones.Proveedor;
                respuesta.AdditionalProperties[PropiedadCostoUsd] = costo;
                foreach (var mensaje in respuesta.Messages)
                {
                    mensaje.AdditionalProperties ??= [];
                    mensaje.AdditionalProperties[PropiedadModelo] = eslabon.Opciones.Modelo;
                }

                return respuesta;
            }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested)
            {
                var tipo = error is OperationCanceledException ? TipoFalla.Transitoria : clasificador.Clasificar(error);
                Castigar(eslabon, tipo);

                var motivo = $"{tipo}: {Resumir(error)}";
                fallas.Add($"{eslabon}: {motivo}");
                if (tipo == TipoFalla.Transitoria)
                {
                    ModeloFallo(logger, eslabon.ToString(), tipo, error);
                }
                else
                {
                    ModeloOmitido(logger, eslabon.ToString(), motivo);
                }

                await RegistrarAsync(contexto, eslabon, intento, motivo, 0, 0, 0, inicio).ConfigureAwait(false);
            }
        }

        throw new CadenaModelosAgotadaException(fallas);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // El respaldo entre modelos solo es seguro antes de emitir texto: se resuelve completo y se reparte en partes.
        var respuesta = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var parte in respuesta.ToChatResponseUpdates())
        {
            yield return parte;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        foreach (var eslabon in eslabones)
        {
            eslabon.Cliente.Dispose();
        }
    }

    /// <summary>
    /// El razonamiento (thinking) va firmado por el modelo que lo produjo y otro modelo lo rechaza: en una conversación con
    /// herramientas que pasa de un modelo a otro se quita el razonamiento ajeno y se conservan el texto y las llamadas.
    /// </summary>
    internal static IList<ChatMessage> ParaModelo(IList<ChatMessage> mensajes, string modelo)
    {
        static bool Ajeno(ChatMessage m, string modelo) =>
            m.AdditionalProperties?.TryGetValue(PropiedadModelo, out var autor) == true && !Equals(autor, modelo) && m.Contents.Any(c => c is TextReasoningContent);

        return !mensajes.Any(m => Ajeno(m, modelo)) ? mensajes : [.. mensajes.Select(m => Ajeno(m, modelo)
            ? new ChatMessage(m.Role, [.. m.Contents.Where(c => c is not TextReasoningContent)]) { AuthorName = m.AuthorName, MessageId = m.MessageId, AdditionalProperties = m.AdditionalProperties }
            : m)];
    }

    private TimeSpan TiempoMaximo => options.Value.TiempoMaximoPorIntento;

    private void Castigar(EslabonCadena eslabon, TipoFalla tipo)
    {
        var espera = tipo switch
        {
            TipoFalla.CuotaAgotada => options.Value.EsperaTrasCuotaAgotada,
            TipoFalla.ModeloNoDisponible => options.Value.EsperaTrasModeloNoDisponible,
            _ => TimeSpan.Zero,
        };

        if (espera > TimeSpan.Zero)
        {
            eslabon.OmitirHasta(reloj.GetUtcNow() + espera);
        }
    }

    private async Task RegistrarAsync(
        ContextoIa contexto, EslabonCadena eslabon, int intento, string? falla,
        long entrada, long salida, decimal costo, long inicio)
    {
        var uso = new UsoModelo
        {
            EmpresaId = contexto.EmpresaId,
            Agente = contexto.Agente,
            Proveedor = eslabon.Opciones.Proveedor,
            Modelo = eslabon.Opciones.Modelo,
            Nivel = eslabon.Opciones.Nivel,
            Intento = intento,
            Exito = falla is null,
            Falla = falla,
            TokensEntrada = entrada,
            TokensSalida = salida,
            CostoUsd = costo,
            DuracionMs = (long)reloj.GetElapsedTime(inicio).TotalMilliseconds,
            OcurridoEn = reloj.GetUtcNow(),
        };

        try
        {
            // Sin el token del llamador: el registro de costo no se pierde si la petición se cancela al final.
            await registro.RegistrarAsync(uso, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            RegistroFallo(logger, eslabon.ToString(), error);
        }
    }

    private static string Resumir(Exception error)
    {
        var mensaje = error.Message.ReplaceLineEndings(" ");
        return mensaje.Length <= 200 ? mensaje : mensaje[..200];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "El modelo {Modelo} falló ({Tipo}); se intenta el siguiente")]
    private static partial void ModeloFallo(ILogger logger, string modelo, TipoFalla tipo, Exception error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "El modelo {Modelo} no está disponible por ahora ({Motivo}); se intenta el siguiente")]
    private static partial void ModeloOmitido(ILogger logger, string modelo, string motivo);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo registrar el uso del modelo {Modelo}")]
    private static partial void RegistroFallo(ILogger logger, string modelo, Exception error);
}

/// <summary>Ningún modelo de la cadena respondió.</summary>
public sealed class CadenaModelosAgotadaException(IReadOnlyList<string> fallas)
    : Exception("Ningún modelo de IA respondió: " + string.Join(" | ", fallas))
{
    public IReadOnlyList<string> Fallas { get; } = fallas;
}
