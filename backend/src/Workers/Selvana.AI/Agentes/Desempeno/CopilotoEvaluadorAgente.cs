using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using Selvana.AI.Agentes.Reclutamiento;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Desempeno;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.AI.Gateway;
using Selvana.Desempeno.Contracts.Ia;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.Desempeno;

public sealed record BorradorCopiloto(IReadOnlyList<string> Fortalezas, IReadOnlyList<string> Oportunidades, string Comentario, IReadOnlyList<RazonRedaccion> Razones);

public sealed record ResultadoCopiloto(BorradorCopiloto Borrador, string Modelo, decimal CostoUsd);

public interface ICopilotoEvaluador
{
    Task<Result<ResultadoCopiloto>> RedactarAsync(BorradorEvaluacionSolicitado solicitud, CancellationToken cancellationToken);
}

/// <summary>
/// D3 · Copiloto del evaluador: con los objetivos, su avance, las evidencias y la autoevaluación redacta fortalezas, oportunidades y un comentario
/// que el líder ajusta. Nunca califica: la calificación es una decisión humana. Recibe evidencias de una persona: va a modelos de pago.
/// </summary>
internal sealed class CopilotoEvaluadorAgente(IChatClient ia, IFiltroVariablesProtegidas filtro) : ICopilotoEvaluador
{
    public const string Nombre = "copiloto-evaluador";

    private const string Instrucciones = """
        Eres el copiloto del evaluador de Selvana, un software de talento humano colombiano.
        Con los OBJETIVOS (meta, avance y evidencias), las COMPETENCIAS y la AUTOEVALUACIÓN de una persona, redactas un borrador para su líder,
        en español de Colombia, tuteando a la persona evaluada.

        "fortalezas": 2 a 4 frases cortas, cada una apoyada en una evidencia o en un avance concreto.
        "oportunidades": 1 a 3 frases cortas y accionables, sin juicios sobre la persona, sobre la conducta o el resultado.
        "comentario": un párrafo de 60 a 120 palabras con la retroalimentación completa: reconoce lo logrado, nombra lo que debe mejorar y
        propone un siguiente paso. Tono cálido, claro y respetuoso.
        "razones": 3 a 5, cada una con "signo" ("+" o "−"), "texto" corto y "fuente" (el objetivo o la competencia de donde sale).

        Reglas:
        - NUNCA pongas una calificación, un puntaje ni una recomendación de despido o sanción: eso lo decide el líder.
        - No inventes logros que no estén en las evidencias. Si casi no hay evidencias, dilo en "razones" con signo "−".
        - Nunca menciones edad, género, estado civil, embarazo, hijos, religión, origen ni salud.
        """;

    public async Task<Result<ResultadoCopiloto>> RedactarAsync(BorradorEvaluacionSolicitado s, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(s);
        var mensajes = new List<ChatMessage> { new(ChatRole.System, Instrucciones), new(ChatRole.User, Pregunta(s)) };
        var opciones = new ContextoIa(s.EmpresaId, Nombre, ContieneDatosPersonales: true).AplicarA(new ChatOptions { MaxOutputTokens = 2_000 });

        ChatResponse<BorradorCopiloto> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<BorradorCopiloto>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Escriba su comentario a mano o pida el borrador más tarde.");
        }

        if (!respuesta.TryGetResult(out var b) || string.IsNullOrWhiteSpace(b?.Comentario))
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió un borrador completo. Pídalo de nuevo o escriba su comentario a mano.");
        }

        static string L(string? t) => CalificadorAfinidadAgente.SinEscapes(t).Trim();
        var limpio = new BorradorCopiloto(
            [.. (b.Fortalezas ?? []).Select(L).Where(x => x.Length > 0 && !filtro.EsDiscriminatorio(x)).Take(4)],
            [.. (b.Oportunidades ?? []).Select(L).Where(x => x.Length > 0 && !filtro.EsDiscriminatorio(x)).Take(3)],
            filtro.EsDiscriminatorio(b.Comentario) ? "" : L(b.Comentario),
            [.. (b.Razones ?? []).Where(r => !string.IsNullOrWhiteSpace(r.Texto) && !filtro.EsDiscriminatorio(r.Texto))
                .Select(r => new RazonRedaccion(r.Signo is "-" or "−" ? "−" : "+", L(r.Texto), string.IsNullOrWhiteSpace(r.Fuente) ? "Evidencias" : L(r.Fuente))).Take(5)]);
        if (limpio.Comentario.Length == 0)
        {
            return Error.Failure("Ia.RespuestaInvalida", "El borrador no pasó el filtro de datos protegidos. Escriba su comentario a mano.");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoCopiloto(limpio, respuesta.ModelId ?? "", costo);
    }

    internal static string Pregunta(BorradorEvaluacionSolicitado s)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"CARGO: {s.Cargo}");
        sb.AppendLine("OBJETIVOS");
        foreach (var o in s.Objetivos)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {o.Descripcion} · meta {o.Meta} · avance {o.Avance} %");
            foreach (var e in o.Evidencias.Take(5))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  · evidencia: {e}");
            }
        }

        sb.AppendLine("COMPETENCIAS");
        foreach (var c in s.Competencias)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {c.Nombre}: {c.Descripcion}{(c.Autoevaluacion is { } a ? $" · se autoevaluó con {a} de 5" : "")}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"AUTOEVALUACIÓN: {(string.IsNullOrWhiteSpace(s.ComentarioAutoevaluacion) ? "(no escribió comentario)" : s.ComentarioAutoevaluacion)}");
        return sb.ToString();
    }
}

internal sealed class BorradorEvaluacionSolicitadoConsumer(ICopilotoEvaluador agente, Persistence.IaDbContext db, TimeProvider reloj)
    : Selvana.SharedKernel.Messaging.IIntegrationEventConsumer<BorradorEvaluacionSolicitado>
{
    public async Task Handle(BorradorEvaluacionSolicitado e, CancellationToken cancellationToken)
    {
        var r = await agente.RedactarAsync(e, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new BorradorEvaluacionGenerado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.EvaluacionId, r.Value.Borrador.Fortalezas, r.Value.Borrador.Oportunidades,
                r.Value.Borrador.Comentario, r.Value.Borrador.Razones, r.Value.Modelo, r.Value.CostoUsd, null)
            : new BorradorEvaluacionGenerado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.EvaluacionId, [], [], "", [], "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
