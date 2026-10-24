using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Web.Common;

public static class EventosEntrantesEndpoint
{
    /// <summary>
    /// Recibe eventos de integración por HTTP y los guarda en el inbox de <typeparamref name="TContext"/>.
    /// Responde 202 de inmediato: el trabajo (por ejemplo, de IA) lo hace el <see cref="InboxProcessor{TContext}"/>.
    /// </summary>
    public static RouteHandlerBuilder MapEventosEntrantes<TContext>(this IEndpointRouteBuilder app, string pattern)
        where TContext : ModuleDbContext =>
        app.MapPost(pattern, async (EventoEntrante evento, HttpRequest request, TContext db,
                IntegrationEventDispatcher<TContext> dispatcher, IOptions<MensajeriaOptions> options, TimeProvider reloj, CancellationToken ct) =>
            {
                if (!ClaveInterna.Valida(request.Headers[MensajeriaOptions.EncabezadoClave], options.Value.ClaveInterna))
                {
                    return Results.Unauthorized();
                }

                if (!dispatcher.Conoce(evento.Tipo))
                {
                    return Results.Problem(title: "Evento.Desconocido", detail: $"Nadie maneja '{evento.Tipo}' aquí.", statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var id = LeerId(evento.Contenido);
                if (id is null)
                {
                    return Results.Problem(title: "Evento.SinId", detail: "El evento no trae Id.", statusCode: StatusCodes.Status400BadRequest);
                }

                if (!await db.InboxMessages.AnyAsync(m => m.Id == id, ct))
                {
                    db.InboxMessages.Add(new InboxMessage { Id = id.Value, Type = evento.Tipo, Content = evento.Contenido, ReceivedOn = reloj.GetUtcNow() });
                    await db.SaveChangesAsync(ct);
                }

                return Results.Accepted();
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

    private static Guid? LeerId(string contenido)
    {
        using var json = JsonDocument.Parse(contenido);
        return json.RootElement.TryGetProperty("Id", out var id) && id.TryGetGuid(out var guid) ? guid : null;
    }
}
