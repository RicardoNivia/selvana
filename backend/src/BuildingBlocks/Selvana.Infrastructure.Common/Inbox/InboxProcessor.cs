using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Infrastructure.Common.Inbox;

/// <summary>Procesa en segundo plano los eventos recibidos: un evento por scope, con reintentos.</summary>
public sealed partial class InboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IntegrationEventDispatcher<TContext> dispatcher,
    TimeProvider timeProvider,
    ILogger<InboxProcessor<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    private const int TamanoLote = 20;
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);

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
        List<Guid> pendientes;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            pendientes = await context.InboxMessages
                .Where(m => m.ProcessedOn == null && m.Intentos < InboxMessage.MaximoIntentos)
                .OrderBy(m => m.ReceivedOn)
                .Select(m => m.Id)
                .Take(TamanoLote)
                .ToListAsync(cancellationToken);
        }

        foreach (var id in pendientes)
        {
            await ProcesarUno(id, cancellationToken);
        }

        return pendientes.Count;
    }

    private async Task ProcesarUno(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var mensaje = await context.InboxMessages.SingleAsync(m => m.Id == id, cancellationToken);

        try
        {
            await dispatcher.DespacharAsync(scope.ServiceProvider, mensaje.Type, mensaje.Content, cancellationToken);
            mensaje.ProcessedOn = timeProvider.GetUtcNow();
            mensaje.Error = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            mensaje.Intentos++;
            mensaje.Error = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            EventoFallo(logger, mensaje.Type, mensaje.Intentos, ex);
            context.ChangeTracker.Clear();
            context.InboxMessages.Attach(mensaje).State = EntityState.Modified;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error procesando el inbox de {Contexto}")]
    private static partial void ErrorLote(ILogger logger, string contexto, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "El evento {Tipo} falló (intento {Intento})")]
    private static partial void EventoFallo(ILogger logger, string tipo, int intento, Exception ex);
}
