using Microsoft.Extensions.AI;
using Selvana.AI.Gateway;

namespace Selvana.AI.Tests;

/// <summary>Modelo falso: responde un texto fijo o lanza la excepción indicada.</summary>
internal sealed class ModeloFalso(Func<ChatResponse> responder) : IChatClient
{
    public int Llamadas { get; private set; }
    public ChatOptions? UltimasOpciones { get; private set; }

    public static ModeloFalso Que(string texto, long entrada = 100, long salida = 50) => new(() =>
        new ChatResponse(new ChatMessage(ChatRole.Assistant, texto))
        {
            Usage = new UsageDetails { InputTokenCount = entrada, OutputTokenCount = salida },
        });

    public static ModeloFalso QueFalla(Exception error) => new(() => throw error);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Llamadas++;
        UltimasOpciones = options;
        return Task.FromResult(responder());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No se usa en las pruebas.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}

internal sealed class RegistroEnMemoria : IRegistroUsoIa
{
    public List<UsoModelo> Usos { get; } = [];

    public Task RegistrarAsync(UsoModelo uso, CancellationToken cancellationToken)
    {
        Usos.Add(uso);
        return Task.CompletedTask;
    }
}

internal sealed class RelojManual(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _ahora = inicio;

    public override DateTimeOffset GetUtcNow() => _ahora;

    public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
}
