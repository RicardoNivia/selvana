using System.Text;
using Microsoft.Extensions.AI;
using Selvana.AI.Agentes.Reclutamiento;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Clima;
using Selvana.AI.Gateway;
using Selvana.Clima.Contracts.Ia;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.Clima;

public sealed record AnalisisTemas(IReadOnlyList<TemaComentarios> Temas, string Resumen);

public sealed record ResultadoAnalisisClima(AnalisisTemas Analisis, string Modelo, decimal CostoUsd);

public interface IAnalistaClima
{
    Task<Result<ResultadoAnalisisClima>> AnalizarAsync(ComentariosClimaSolicitados solicitud, CancellationToken cancellationToken);
}

/// <summary>
/// C2 · Analista de clima: agrupa los comentarios abiertos por temas, con su tono, citas cortas sin datos que identifiquen y una acción sugerida.
/// Los comentarios pueden traer datos de personas: va a modelos de pago.
/// </summary>
internal sealed class AnalistaClimaAgente(IChatClient ia, IFiltroVariablesProtegidas filtro) : IAnalistaClima
{
    public const string Nombre = "analista-clima";
    public static readonly string[] Sentimientos = ["Positivo", "Negativo", "Mixto"];

    private const string Instrucciones = """
        Eres el analista de clima laboral de Selvana, un software de talento humano colombiano.
        Recibes los COMENTARIOS ANÓNIMOS de una encuesta de clima y los agrupas por temas, en español de Colombia.

        "temas": de 3 a 7, del más mencionado al menos. Cada uno con:
        - "nombre": 2 a 4 palabras ("Turnos y carga de trabajo", "Trato de los líderes").
        - "sentimiento": EXACTAMENTE uno de "Positivo", "Negativo", "Mixto".
        - "cantidad": cuántos comentarios tocan el tema.
        - "citas": 1 o 2 frases cortas tomadas de los comentarios, SIN nombres, cargos únicos, fechas ni nada que permita saber quién lo escribió.
          Si una frase identifica a alguien, parafraséala.
        - "accion": una acción concreta que puede tomar Recursos Humanos.
        "resumen": dos frases con lo más importante.

        Reglas:
        - Nunca menciones ni deduzcas edad, género, estado civil, embarazo, hijos, religión, origen ni salud de nadie.
        - No intentes adivinar quién escribió un comentario.
        """;

    public async Task<Result<ResultadoAnalisisClima>> AnalizarAsync(ComentariosClimaSolicitados s, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(s);
        var texto = new StringBuilder("COMENTARIOS\n");
        foreach (var comentario in s.Comentarios.Take(300))
        {
            texto.Append("- ").AppendLine(comentario.Length > 600 ? comentario[..600] : comentario);
        }

        var mensajes = new List<ChatMessage> { new(ChatRole.System, Instrucciones), new(ChatRole.User, texto.ToString()) };
        var opciones = new ContextoIa(s.EmpresaId, Nombre, ContieneDatosPersonales: true).AplicarA(new ChatOptions { MaxOutputTokens = 3_000 });

        ChatResponse<AnalisisTemas> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<AnalisisTemas>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Use el análisis por palabras clave.");
        }

        if (!respuesta.TryGetResult(out var a) || a?.Temas is not { Count: > 0 })
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió temas. Pídalo de nuevo o use el análisis por palabras clave.");
        }

        static string L(string? t) => CalificadorAfinidadAgente.SinEscapes(t).Trim();
        var temas = a.Temas.Where(t => !string.IsNullOrWhiteSpace(t.Nombre) && !filtro.EsDiscriminatorio(t.Nombre + " " + t.Accion))
            .Select(t => new TemaComentarios(L(t.Nombre), Sentimientos.FirstOrDefault(x => x.Equals(L(t.Sentimiento), StringComparison.OrdinalIgnoreCase)) ?? "Mixto",
                Math.Clamp(t.Cantidad, 1, s.Comentarios.Count), [.. (t.Citas ?? []).Select(L).Where(c => c.Length > 0 && !filtro.EsDiscriminatorio(c)).Take(2)], L(t.Accion)))
            .Take(7).ToList();
        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoAnalisisClima(new AnalisisTemas(temas, L(a.Resumen)), respuesta.ModelId ?? "", costo);
    }
}

internal sealed class ComentariosClimaSolicitadosConsumer(IAnalistaClima agente, Persistence.IaDbContext db, TimeProvider reloj)
    : Selvana.SharedKernel.Messaging.IIntegrationEventConsumer<ComentariosClimaSolicitados>
{
    public async Task Handle(ComentariosClimaSolicitados e, CancellationToken cancellationToken)
    {
        var r = await agente.AnalizarAsync(e, cancellationToken);
        db.AddIntegrationEvent(r.IsSuccess
            ? new TemasClimaGenerados(Guid.CreateVersion7(), reloj.GetUtcNow(), e.EmpresaId, e.EncuestaId, r.Value.Analisis.Temas, r.Value.Analisis.Resumen, r.Value.Modelo,
                r.Value.CostoUsd, null)
            : new TemasClimaGenerados(Guid.CreateVersion7(), reloj.GetUtcNow(), e.EmpresaId, e.EncuestaId, [], "", "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
