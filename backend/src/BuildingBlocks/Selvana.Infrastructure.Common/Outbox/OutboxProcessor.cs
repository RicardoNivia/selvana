using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Infrastructure.Common.Outbox;

/// <summary>Publica periódicamente los mensajes pendientes del outbox de un módulo.</summary>
public sealed partial class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    private const int TamanoLote = 50;
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcesarLote(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ErrorLote(logger, typeof(TContext).Name, ex);
            }
        }
    }

    public async Task<int> ProcesarLote(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var mensajes = await context.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOn)
            .Take(TamanoLote)
            .ToListAsync(cancellationToken);

        foreach (var mensaje in mensajes)
        {
            try
            {
                await bus.PublishAsync(mensaje.Type, mensaje.Content, cancellationToken);
                mensaje.Error = null;
                mensaje.ProcessedOn = timeProvider.GetUtcNow();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mensaje.Error = ex.Message;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return mensajes.Count;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error procesando el outbox de {Contexto}")]
    private static partial void ErrorLote(ILogger logger, string contexto, Exception ex);
}
