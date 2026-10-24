using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Infrastructure.Common.Persistence;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Infrastructure.Common.Inbox;

/// <summary>
/// Convierte un evento recibido (tipo + JSON) en su clase y lo entrega a los consumidores del módulo dueño de <typeparamref name="TContext"/>.
/// Hay uno por módulo: cada módulo tiene su inbox y solo conoce sus propios eventos.
/// </summary>
#pragma warning disable CA1812 // Se instancia por inyección de dependencias
public sealed class IntegrationEventDispatcher<TContext>(IReadOnlyDictionary<string, Type> tiposConocidos)
    where TContext : ModuleDbContext
{
    public bool Conoce(string tipo) => tiposConocidos.ContainsKey(tipo);

    public async Task DespacharAsync(IServiceProvider services, string tipo, string contenido, CancellationToken cancellationToken)
    {
        if (!tiposConocidos.TryGetValue(tipo, out var tipoEvento))
        {
            throw new InvalidOperationException($"Nadie maneja el evento '{tipo}'.");
        }

        var evento = JsonSerializer.Deserialize(contenido, tipoEvento)
            ?? throw new InvalidOperationException($"El contenido del evento '{tipo}' está vacío.");

        // Invocador está anidado en una clase genérica: su definición abierta lleva TContext y TEvent
        var invocador = (IInvocador)Activator.CreateInstance(typeof(Invocador<>).MakeGenericType(typeof(TContext), tipoEvento))!;
        await invocador.InvocarAsync(services, evento, cancellationToken).ConfigureAwait(false);
    }

    private interface IInvocador
    {
        Task InvocarAsync(IServiceProvider services, object evento, CancellationToken cancellationToken);
    }

    private sealed class Invocador<TEvent> : IInvocador
        where TEvent : IIntegrationEvent
    {
        public async Task InvocarAsync(IServiceProvider services, object evento, CancellationToken cancellationToken)
        {
            foreach (var consumidor in services.GetServices<IIntegrationEventConsumer<TEvent>>())
            {
                await consumidor.Handle((TEvent)evento, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
#pragma warning restore CA1812

public static class IntegrationEventConsumersServiceCollectionExtensions
{
    /// <summary>Registra los consumidores de eventos del módulo de <typeparamref name="TContext"/> y su despachador.</summary>
    public static IServiceCollection AddIntegrationEventConsumers<TContext>(this IServiceCollection services, params Assembly[] assemblies)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var tipos = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var tipo in assemblies.SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var contrato in tipo.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventConsumer<>)))
            {
                services.AddScoped(contrato, tipo);
                var evento = contrato.GetGenericArguments()[0];
                tipos[evento.FullName!] = evento;
            }
        }

        services.AddSingleton(new IntegrationEventDispatcher<TContext>(tipos));
        return services;
    }
}
