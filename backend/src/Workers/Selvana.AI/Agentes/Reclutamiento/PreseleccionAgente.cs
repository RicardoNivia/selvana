using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.AI.Gateway;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.Reclutamiento;

/// <summary>Un turno del agente: qué le responde al candidato, qué entendió hasta ahora y si terminó o hay que pasar a una persona.</summary>
public sealed record TurnoPreseleccion(string Mensaje, ResumenConversacion Resumen, bool Completa, bool Escalar, string MotivoEscalar);

public sealed record ResultadoTurno(TurnoPreseleccion Turno, string Modelo, decimal CostoUsd);

public interface IAgentePreseleccion
{
    Task<Result<ResultadoTurno>> ResponderAsync(Guid empresaId, string nombre, VacantePreseleccion vacante, IReadOnlyList<MensajePrevio> mensajes, CancellationToken cancellationToken);
}

/// <summary>
/// Agente conversacional de preselección: confirma interés, disponibilidad, ubicación y aspiración, hace las preguntas técnicas de la vacante
/// y responde dudas solo con lo que dice la vacante. Pasa a una persona si el candidato lo pide o si no está seguro.
/// Nunca pregunta ni guarda variables protegidas, y no descarta ni promete nada: una persona decide.
/// </summary>
internal sealed class PreseleccionAgente(IChatClient ia, IFiltroVariablesProtegidas filtro, TimeProvider reloj) : IAgentePreseleccion
{
    public const string Nombre = "preseleccion";
    public const string MensajeRetirado = "Gracias por tu mensaje. Una persona de la empresa te responde aquí mismo en un momento.";

    private static readonly CultureInfo Pesos = new("es-CO");

    private const string Instrucciones = """
        Eres el asistente de preselección de Selvana y conversas por chat con un candidato que se postuló a una vacante en Colombia.
        Tuteas, en español de Colombia, con frases cortas y cálidas: máximo 3 frases por mensaje y UNA sola pregunta a la vez.

        Lo que debes averiguar, en este orden (salta lo que ya esté respondido en la conversación):
        1. Si sigue interesado en la vacante.
        2. Disponibilidad para la jornada de la vacante y desde cuándo podría empezar.
        3. Ubicación: solo ciudad o municipio, y si puede llegar al lugar de trabajo. NUNCA pidas la dirección ni el barrio.
        4. Aspiración salarial mensual en pesos (puedes mencionar el rango de la vacante si pregunta).
        5. Las preguntas técnicas de la vacante, una por una.
        Al terminar, agradece y dile que el equipo de la empresa revisará y le avisará. Marca "completa" en true.

        Reglas:
        - Si el candidato pregunta algo, respóndele SOLO con los datos de la vacante. Si no está en la vacante, dile que lo consultas con el equipo y marca "escalar" con el motivo.
        - Si pide hablar con una persona, o algo te genera duda (un reclamo, un tema legal, una situación delicada), marca "escalar" con el motivo y dile que una persona le responde aquí mismo.
        - NUNCA preguntes, comentes ni guardes edad, fecha de nacimiento, género, estado civil, hijos, embarazo, religión, salud, apariencia, estrato ni dirección. Si el candidato los menciona, no los registres.
        - No descartes, no prometas el trabajo ni des fechas que la vacante no dice.
        - "resumen": lo que sabes hasta ahora. "interesado" true/false o null si no lo ha dicho; "aspiracionSalarialCop" en pesos o null; "respuestas" con cada pregunta técnica respondida; "dudas" con lo que preguntó el candidato; "texto" un resumen de una o dos frases para el reclutador.
        """;

    public async Task<Result<ResultadoTurno>> ResponderAsync(Guid empresaId, string nombre, VacantePreseleccion vacante, IReadOnlyList<MensajePrevio> mensajes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vacante);
        ArgumentNullException.ThrowIfNull(mensajes);
        var chat = new List<ChatMessage> { new(ChatRole.System, Instrucciones + "\n\n" + Contexto(nombre, vacante, DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))) };
        chat.AddRange(mensajes.TakeLast(30).Select(m => new ChatMessage(m.Autor == "Candidato" ? ChatRole.User : ChatRole.Assistant,
            m.Autor == "Persona" ? $"[Respondió una persona de la empresa] {m.Texto}" : m.Texto)));
        // La conversación tiene datos personales: nunca va a modelos gratis
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: true).AplicarA(new ChatOptions { MaxOutputTokens = 2_000 });

        ChatResponse<TurnoPreseleccion> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<TurnoPreseleccion>(chat, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió.");
        }

        if (!respuesta.TryGetResult(out var turno) || turno is null || string.IsNullOrWhiteSpace(turno.Mensaje))
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió una respuesta válida.");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoTurno(Validar(turno, filtro), respuesta.ModelId ?? "", costo);
    }

    internal static string Contexto(string nombre, VacantePreseleccion v, DateOnly? hoy = null)
    {
        var sb = new StringBuilder();
        if (hoy is { } h)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Fecha de hoy: {h:yyyy-MM-dd}.");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"Candidato: {nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "candidato"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Vacante: {v.Titulo} · empresa {v.Empresa} · {v.Ciudad} · jornada: {v.Jornada} · {v.Cupos} cupos");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Rango salarial: {v.SalarioMinimo.ToString("C0", Pesos)} a {v.SalarioMaximo.ToString("C0", Pesos)} mensuales");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Descripción: {v.Descripcion}");
        sb.AppendLine("Requisitos:");
        foreach (var r in v.Requisitos.Take(10))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {r}");
        }

        sb.AppendLine("Preguntas técnicas que debes hacer:");
        foreach (var p in v.PreguntasTecnicas.Take(6))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {p}");
        }

        return sb.ToString();
    }

    /// <summary>Si el agente menciona una variable protegida, su mensaje se retira y responde una persona; el resumen nunca la guarda.</summary>
    internal static TurnoPreseleccion Validar(TurnoPreseleccion t, IFiltroVariablesProtegidas filtro)
    {
        var r = t.Resumen ?? new ResumenConversacion(null, null, "", "", [], [], "");
        string Limpio(string? s) => string.IsNullOrWhiteSpace(s) || filtro.EsDiscriminatorio(s) ? "" : s.Trim();
        var resumen = new ResumenConversacion(r.Interesado, r.AspiracionSalarialCop is > 0 and < 100_000_000 ? r.AspiracionSalarialCop : null,
            Limpio(r.Disponibilidad), Limpio(r.Ubicacion),
            [.. (r.Respuestas ?? []).Where(x => !filtro.EsDiscriminatorio(x.Pregunta) && !filtro.EsDiscriminatorio(x.Respuesta)).Take(10)],
            [.. (r.Dudas ?? []).Where(x => !filtro.EsDiscriminatorio(x)).Take(10)], Limpio(r.Texto));

        if (filtro.EsDiscriminatorio(t.Mensaje))
        {
            return new TurnoPreseleccion(MensajeRetirado, resumen, false, true, "Se retiró una respuesta de la IA que mencionaba una variable protegida");
        }

        return t with { Mensaje = t.Mensaje.Trim(), Resumen = resumen, MotivoEscalar = t.Escalar ? (string.IsNullOrWhiteSpace(t.MotivoEscalar) ? "La IA pidió apoyo de una persona" : t.MotivoEscalar.Trim()) : "" };
    }
}

/// <summary>El candidato escribió: el agente responde y la respuesta vuelve a Reclutamiento por el outbox.</summary>
internal sealed class MensajePreseleccionRecibidoConsumer(IAgentePreseleccion agente, Persistence.IaDbContext db, TimeProvider reloj)
    : Selvana.SharedKernel.Messaging.IIntegrationEventConsumer<MensajePreseleccionRecibido>
{
    public async Task Handle(MensajePreseleccionRecibido e, CancellationToken cancellationToken)
    {
        var r = await agente.ResponderAsync(e.EmpresaId, e.NombreCandidato, e.Vacante, e.Mensajes, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new RespuestaPreseleccion(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ConversacionId, r.Value.Turno.Mensaje, r.Value.Turno.Resumen, r.Value.Turno.Completa,
                r.Value.Turno.Escalar ? r.Value.Turno.MotivoEscalar : null, r.Value.Modelo, r.Value.CostoUsd, null)
            : new RespuestaPreseleccion(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ConversacionId, "", null, false, null, "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
