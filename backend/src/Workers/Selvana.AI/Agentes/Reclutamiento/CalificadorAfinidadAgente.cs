using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.AI.Gateway;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.Reclutamiento;

/// <summary>Lo que devuelve el modelo; se valida antes de guardarse como sugerencia.</summary>
public sealed record PuntajeAfinidad(int Puntaje, IReadOnlyList<RazonAfinidad> Razones, IReadOnlyList<string> RequisitosCumplidos, IReadOnlyList<string> RequisitosFaltantes);

/// <summary>El puntaje validado y cuántas razones se retiraron por mencionar variables protegidas.</summary>
public sealed record ResultadoAfinidad(PuntajeAfinidad Afinidad, string Modelo, decimal CostoUsd, int RazonesRetiradas = 0);

public interface ICalificadorAfinidad
{
    Task<Result<ResultadoAfinidad>> CalificarAsync(Guid empresaId, RequisitosVacante vacante, PerfilAnonimo perfil, IReadOnlyList<CriterioEmpresa>? calibracion,
        CancellationToken cancellationToken);
}

/// <summary>
/// Agente de ranking: califica de 0 a 100 qué tan bien encaja un perfil anónimo con la vacante y lo explica con 3 a 5 razones
/// citadas de la hoja de vida. Nunca recibe nombre, documento, contacto ni variables protegidas, y las razones que las mencionen se retiran.
/// Es una sugerencia: una persona decide.
/// </summary>
internal sealed class CalificadorAfinidadAgente(IChatClient ia, IFiltroVariablesProtegidas filtro, TimeProvider reloj) : ICalificadorAfinidad
{
    public const string Nombre = "ranking-afinidad";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private const string Instrucciones = """
        Eres el agente de ranking de Selvana, un software de talento humano colombiano. Calificas qué tan bien encaja un candidato con una vacante.
        Recibes la vacante y un perfil ANÓNIMO del candidato (sin nombre, edad, género, estado civil, foto ni dirección).

        Reglas:
        - "puntaje" de 0 a 100: 85 o más = encaja muy bien; 60 a 84 = encaja con reservas; menos de 60 = no encaja.
        - Pesa, en este orden: experiencia en el mismo cargo u oficio (y cuánta), requisitos obligatorios de la vacante (licencias, cursos, estudios), habilidades del oficio, respuestas a las preguntas filtro (un "No" en una pregunta clave pesa mucho) y cercanía de la ciudad.
        - Entre 3 y 5 razones. Cada una con "signo" ("+" a favor o "−" en contra), un "texto" corto y concreto, y "evidencia": de dónde sale, con este formato exacto: "HV · experiencia N" (N empieza en 1, como viene la lista), "HV · formación", "HV · cursos", "HV · habilidades", "HV · ciudad" o "Preguntas filtro".
        - Nunca uses, infieras ni menciones edad, género, estado civil, hijos, origen, religión, apariencia, estrato, barrio ni dirección. No castigues un hueco en la experiencia: solo menciónalo si la vacante pide experiencia continua.
        - "requisitosCumplidos" y "requisitosFaltantes": copia el texto de los requisitos de la vacante que el perfil cumple o no demuestra.
        - Sé estricto con la evidencia: si el perfil no lo dice, no lo supongas.
        """;

    public async Task<Result<ResultadoAfinidad>> CalificarAsync(Guid empresaId, RequisitosVacante vacante, PerfilAnonimo perfil, IReadOnlyList<CriterioEmpresa>? calibracion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vacante);
        ArgumentNullException.ThrowIfNull(perfil);
        var mensajes = new List<ChatMessage>
        {
            new(ChatRole.System, Instrucciones),
            new(ChatRole.User, Pregunta(vacante, perfil, calibracion, DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))),
        };
        // Aunque es anónimo, el perfil sale de una hoja de vida: nunca a modelos gratis
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: true).AplicarA(new ChatOptions { MaxOutputTokens = 4_000 });

        ChatResponse<PuntajeAfinidad> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<PuntajeAfinidad>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. La afinidad se puede recalcular desde la vacante.");
        }

        if (!respuesta.TryGetResult(out var afinidad) || afinidad?.Razones is null)
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió un puntaje válido. Vuelva a calcular la afinidad.");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        var validada = Validar(afinidad, filtro);
        return new ResultadoAfinidad(validada, respuesta.ModelId ?? "", costo, Math.Max(0, Math.Min(afinidad.Razones.Count, 5) - validada.Razones.Count));
    }

    /// <summary>A veces el modelo devuelve las tildes escapadas («conducci\u00f3n»): se vuelven texto normal.</summary>
    internal static string SinEscapes(string? texto) => string.IsNullOrEmpty(texto) ? texto ?? "" :
        System.Text.RegularExpressions.Regex.Replace(texto, @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    internal static string Pregunta(RequisitosVacante vacante, PerfilAnonimo perfil, IReadOnlyList<CriterioEmpresa>? calibracion = null, DateOnly? hoy = null)
    {
        var sb = new StringBuilder();
        // Sin la fecha de hoy el modelo no sabe si un curso o una licencia siguen vigentes
        if (hoy is { } h)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"FECHA DE HOY: {h:yyyy-MM-dd}. Un curso, certificado o licencia con vigencia anterior a esta fecha está VENCIDO y no cumple el requisito.");
            sb.AppendLine();
        }

        if (calibracion is { Count: > 0 })
        {
            // Las decisiones anteriores de la empresa calibran el puntaje; nunca se usan para inferir variables protegidas
            sb.AppendLine("CRITERIOS DE ESTA EMPRESA (decisiones anteriores de su equipo sobre candidatos parecidos; úsalas para calibrar cuánto pesa cada requisito):");
            foreach (var c in calibracion.Take(5))
            {
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"- Cargo «{c.Cargo}» · perfil: {c.Perfil} · la IA dio {c.PuntajeIa?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "sin puntaje"} · decisión: {c.Decision}");
                sb.AppendLine(string.IsNullOrWhiteSpace(c.Motivo) ? "" : $" · motivo: «{c.Motivo}»");
            }

            sb.AppendLine();
        }

        sb.AppendLine("VACANTE");
        sb.AppendLine(JsonSerializer.Serialize(vacante, Json));
        sb.AppendLine();
        sb.AppendLine("PERFIL ANÓNIMO DEL CANDIDATO");
        sb.AppendLine(JsonSerializer.Serialize(perfil, Json));
        return sb.ToString();
    }

    /// <summary>Puntaje entre 0 y 100, máximo 5 razones, signos válidos, evidencia con formato y ninguna razón con variables protegidas.</summary>
    internal static PuntajeAfinidad Validar(PuntajeAfinidad a, IFiltroVariablesProtegidas filtro)
    {
        a = a with
        {
            Razones = [.. (a.Razones ?? []).Select(r => r with { Texto = SinEscapes(r.Texto), Evidencia = SinEscapes(r.Evidencia) })],
            RequisitosCumplidos = [.. (a.RequisitosCumplidos ?? []).Select(SinEscapes)],
            RequisitosFaltantes = [.. (a.RequisitosFaltantes ?? []).Select(SinEscapes)],
        };
        var razones = a.Razones
            .Where(r => !string.IsNullOrWhiteSpace(r.Texto) && !filtro.EsDiscriminatorio(r.Texto))
            .Select(r => new RazonAfinidad(r.Signo is "-" or "−" ? "−" : "+", r.Texto.Trim(),
                r.Evidencia?.StartsWith("HV", StringComparison.Ordinal) == true || r.Evidencia == "Preguntas filtro" ? r.Evidencia : "HV"))
            .Take(5).ToList();
        return new PuntajeAfinidad(Math.Clamp(a.Puntaje, 0, 100), razones, a.RequisitosCumplidos ?? [], a.RequisitosFaltantes ?? []);
    }
}
