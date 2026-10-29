using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Seleccion;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Reclutamiento.Infrastructure;

/// <summary>Cada cinco minutos el agente de selección hace los pasos de bajo riesgo en las vacantes con modo automático.</summary>
internal sealed partial class AgenteSeleccion(IServiceScopeFactory scopes, ILogger<AgenteSeleccion> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await reloj.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var ids = await scope.ServiceProvider.GetRequiredService<IVacantes>().ConAvanceAutomaticoAsync(stoppingToken);
                foreach (var id in ids)
                {
                    // Un alcance por vacante: si una falla, las demás siguen en la próxima vuelta
                    await using var porVacante = scopes.CreateAsyncScope();
                    await porVacante.ServiceProvider.GetRequiredService<ICommandHandler<AvanzarAutomaticoCommand, int>>().Handle(new AvanzarAutomaticoCommand(id), stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                FallaAvance(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "El agente de selección no pudo avanzar las vacantes automáticas; se intenta en la próxima vuelta.")]
    private static partial void FallaAvance(ILogger logger, Exception ex);
}
