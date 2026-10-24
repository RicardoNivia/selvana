using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Selvana.Infrastructure.Common.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Mensajería HTTP entre la API y el AI.Worker (ADR 0008): sin broker, con outbox e inbox.</summary>
    public static IServiceCollection AddMensajeriaHttp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MensajeriaOptions>(configuration.GetSection(MensajeriaOptions.Section));
        services.AddHttpClient<IEventBus, HttpEventBus>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
