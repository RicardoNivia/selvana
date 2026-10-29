using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Selvana.AI.Gateway;

namespace Selvana.AI.Agentes.Reclutamiento;

/// <summary>
/// Herramienta de desarrollo (comando «probar-hv» del worker): lee una hoja de vida con el mismo lector de producción y, si falla,
/// repite la llamada para mostrar qué modelo respondió y el texto crudo de su respuesta. Sirve para diagnosticar lecturas fallidas.
/// </summary>
public static class DiagnosticoLector
{
    public static async Task<int> ProbarAsync(IServiceProvider services, string ruta)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var contenido = await File.ReadAllBytesAsync(ruta);
        var tipo = Path.GetExtension(ruta).ToUpperInvariant() switch
        {
            ".PDF" => "application/pdf",
            ".DOCX" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".PNG" => "image/png",
            _ => "image/jpeg",
        };
        var lector = services.GetRequiredService<ILectorHojasDeVida>();
        var r = await lector.LeerAsync(Guid.Empty, Path.GetFileName(ruta), tipo, contenido, CancellationToken.None);
        if (r.IsSuccess)
        {
            Console.WriteLine($"Leída por {r.Value.Modelo}: {r.Value.Datos.Nombre} · {r.Value.Datos.Experiencia.Count} experiencias · {r.Value.Datos.Formacion.Count} estudios");
            return 0;
        }

        Console.WriteLine($"Falló: {r.Error.Code} · {r.Error.Description}\nRepito la llamada para ver la respuesta cruda…");
        var texto = tipo.Contains("word", StringComparison.Ordinal) ? LectorHojasDeVidaAgente.TextoDeWord(contenido) : "(archivo binario)";
        var ia = services.GetRequiredService<IChatClient>();
        var opciones = new ContextoIa(Guid.Empty, LectorHojasDeVidaAgente.Nombre, ContieneDatosPersonales: true)
            .AplicarA(new ChatOptions { MaxOutputTokens = 16_000, ResponseFormat = ChatResponseFormat.Json });
        var respuesta = await ia.GetResponseAsync([new ChatMessage(ChatRole.User, $"Extrae en JSON nombre, documento, celular, correo, ciudad y experiencia de esta hoja de vida:\n{texto}")],
            opciones);
        Console.WriteLine($"Modelo: {respuesta.ModelId} · terminó por: {respuesta.FinishReason} · tokens salida: {respuesta.Usage?.OutputTokenCount}");
        Console.WriteLine($"Contenidos: {string.Join(", ", respuesta.Messages.SelectMany(m => m.Contents).Select(c => c.GetType().Name))}");
        Console.WriteLine($"Texto (inicio): {(respuesta.Text.Length > 600 ? respuesta.Text[..600] : respuesta.Text)}");
        return 1;
    }
}
