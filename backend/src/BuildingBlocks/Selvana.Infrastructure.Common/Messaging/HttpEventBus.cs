using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Selvana.Infrastructure.Common.Messaging;

/// <summary>Entrega cada evento por HTTP al inbox de los destinos suscritos. Si falla, el outbox lo reintenta.</summary>
public sealed class HttpEventBus(HttpClient http, IOptions<MensajeriaOptions> options) : IEventBus
{
    public async Task PublishAsync(string type, string content, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var destinos = o.Destinos.Where(d => d.Prefijos.Any(p => type.StartsWith(p, StringComparison.Ordinal)));

        foreach (var destino in destinos)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, destino.Url)
            {
                Content = JsonContent.Create(new EventoEntrante(type, content)),
            };
            request.Headers.Add(MensajeriaOptions.EncabezadoClave, o.ClaveInterna);

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
    }
}
