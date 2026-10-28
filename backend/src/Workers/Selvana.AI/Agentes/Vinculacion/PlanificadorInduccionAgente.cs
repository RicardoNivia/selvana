using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Selvana.AI.Agentes.Reclutamiento;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.AI.Gateway;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Contracts.Ia;

namespace Selvana.AI.Agentes.Vinculacion;

/// <summary>Lo que devuelve el modelo; se valida antes de volver a Vinculación.</summary>
public sealed record BorradorPlanInduccion(IReadOnlyList<TareaInduccionPropuesta> Tareas, IReadOnlyList<RazonRedaccion> Razones, IReadOnlyList<string> Alertas);

public sealed record ResultadoPlanInduccion(BorradorPlanInduccion Plan, string Modelo, decimal CostoUsd);

public interface IPlanificadorInduccion
{
    Task<Result<ResultadoPlanInduccion>> PlanificarAsync(OnboardingSolicitado solicitud, CancellationToken cancellationToken);
}

/// <summary>
/// Planificador de inducción: con el cargo, la vacante y el tipo de contrato propone las tareas de la inducción (qué, quién y cuándo).
/// No recibe datos de la persona. Es una propuesta: RRHH la ajusta y la aprueba.
/// </summary>
internal sealed class PlanificadorInduccionAgente(IChatClient ia, IFiltroVariablesProtegidas filtro) : IPlanificadorInduccion
{
    public const string Nombre = "planificador-induccion";

    public static readonly string[] Momentos = ["AntesDelIngreso", "PrimerDia", "PrimeraSemana", "PrimerMes", "PeriodoPrueba"];
    public static readonly string[] Responsables = ["RRHH", "Líder", "Empleado", "SST", "TI", "Empresa usuaria"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private const string Instrucciones = """
        Eres el planificador de inducción de Selvana, un software de talento humano colombiano.
        Con el CARGO, la VACANTE y las CONDICIONES propones el plan de inducción de una persona nueva, en español de Colombia.

        "tareas": de 8 a 16, cada una con:
        - "titulo": acción corta que empieza con verbo ("Explicar el uso del montacargas", "Entregar dotación y elementos de protección").
        - "detalle": una o dos frases con lo concreto del cargo; nada genérico si la vacante dice algo específico.
        - "responsable": EXACTAMENTE uno de "RRHH", "Líder", "Empleado", "SST", "TI", "Empresa usuaria" (este último solo si hay empresa usuaria).
        - "momento": EXACTAMENTE uno de "AntesDelIngreso", "PrimerDia", "PrimeraSemana", "PrimerMes", "PeriodoPrueba" ("PeriodoPrueba" solo si hay periodo de prueba).
        - "dia": días desde el ingreso (negativo si es antes: -3 a -1; primer día 0; primera semana 1 a 6; primer mes 7 a 30; periodo de prueba hasta su último día).

        Debe incluir: bienvenida antes del ingreso, inducción en seguridad y salud en el trabajo (SG-SST) con los riesgos del cargo, presentación del equipo,
        explicación de funciones del cargo, seguimiento de la primera semana y revisión del primer mes. Si hay empresa usuaria, su inducción en el lugar de trabajo.
        Si hay periodo de prueba, su evaluación antes de que termine. Si las condiciones traen cómo se trabaja el contrato, respétalo.

        Reglas:
        - Nunca menciones edad, género, estado civil, embarazo, hijos, religión, origen ni salud de la persona.
        - No inventes beneficios, pagos ni herramientas que la vacante no diga.
        - "razones": 3 a 5 decisiones del plan, cada una con "signo" ("+" o "−"), "texto" corto y "fuente" ("Vacante", "Cargo", "Contrato", "SG-SST"…).
        - "alertas": lo que RRHH debe revisar (por ejemplo, un curso obligatorio que la vacante pide y no está claro quién lo dicta). Lista vacía si no hay nada.
        """;

    public async Task<Result<ResultadoPlanInduccion>> PlanificarAsync(OnboardingSolicitado solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var mensajes = new List<ChatMessage> { new(ChatRole.System, Instrucciones), new(ChatRole.User, Pregunta(solicitud)) };
        // Sin datos de la persona: puede ir a los modelos gratuitos de la cadena (ADR 0007)
        var opciones = new ContextoIa(solicitud.EmpresaId, Nombre, ContieneDatosPersonales: false).AplicarA(new ChatOptions { MaxOutputTokens = 4_000 });

        ChatResponse<BorradorPlanInduccion> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<BorradorPlanInduccion>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Pida de nuevo el plan o use el plan base.");
        }

        if (!respuesta.TryGetResult(out var plan) || plan?.Tareas is not { Count: > 0 })
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió un plan completo. Pídalo de nuevo o use el plan base.");
        }

        var limpio = Validar(plan, solicitud, filtro);
        if (limpio.Tareas.Count == 0)
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió tareas válidas. Pídalo de nuevo o use el plan base.");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoPlanInduccion(limpio, respuesta.ModelId ?? "", costo);
    }

    internal static string Pregunta(OnboardingSolicitado s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CARGO Y CONDICIONES");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Empleador: {s.Empresa}");
        sb.AppendLine(string.IsNullOrWhiteSpace(s.EmpresaUsuaria)
            ? "- Vinculación directa (no hay empresa usuaria)."
            : $"- Empresa usuaria donde trabajará (misión): {s.EmpresaUsuaria}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Cargo: {s.Cargo}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Ciudad: {s.Ciudad} · Jornada: {s.Jornada}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Tipo de contrato: {s.TipoContrato}");
        if (!string.IsNullOrWhiteSpace(s.ComoSeTrabaja))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Cómo trabaja la empresa este contrato: {s.ComoSeTrabaja}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- Fecha de ingreso: {s.FechaIngreso:dd/MM/yyyy}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Periodo de prueba: {(s.PeriodoPruebaDias > 0 ? $"{s.PeriodoPruebaDias} días" : "no tiene")}");
        sb.AppendLine();
        sb.AppendLine("VACANTE");
        sb.AppendLine(JsonSerializer.Serialize(new { s.DescripcionVacante, s.Requisitos }, Json));
        return sb.ToString();
    }

    /// <summary>Normaliza el plan: responsables y momentos de la lista, días en rango, sin textos con variables protegidas, sin escapes.</summary>
    internal static BorradorPlanInduccion Validar(BorradorPlanInduccion b, OnboardingSolicitado s, IFiltroVariablesProtegidas filtro)
    {
        static string Limpio(string? t) => CalificadorAfinidadAgente.SinEscapes(t).Trim();
        static string Recortar(string t, int maximo) => t.Length > maximo ? t[..maximo] : t;
        var maximoDia = Math.Max(30, s.PeriodoPruebaDias);
        var conUsuaria = !string.IsNullOrWhiteSpace(s.EmpresaUsuaria);

        var tareas = (b.Tareas ?? [])
            .Select(t => new
            {
                Titulo = Recortar(Limpio(t.Titulo), 200),
                Detalle = Recortar(Limpio(t.Detalle), 1000),
                Responsable = Responsables.FirstOrDefault(r => r.Equals(Limpio(t.Responsable), StringComparison.OrdinalIgnoreCase)),
                Momento = Momentos.FirstOrDefault(m => m.Equals(Limpio(t.Momento), StringComparison.OrdinalIgnoreCase)),
                t.Dia,
            })
            .Where(t => t.Titulo.Length > 0 && t.Momento is not null && !filtro.EsDiscriminatorio(t.Titulo + " " + t.Detalle))
            .Select(t => new TareaInduccionPropuesta(t.Titulo, t.Detalle,
                t.Responsable is null || (t.Responsable == "Empresa usuaria" && !conUsuaria) ? "RRHH" : t.Responsable,
                t.Momento!, Math.Clamp(t.Dia, -10, maximoDia)))
            .Take(20).ToList();
        var razones = (b.Razones ?? []).Where(r => !string.IsNullOrWhiteSpace(r.Texto) && !filtro.EsDiscriminatorio(r.Texto))
            .Select(r => new RazonRedaccion(r.Signo is "-" or "−" ? "−" : "+", Limpio(r.Texto), string.IsNullOrWhiteSpace(r.Fuente) ? "Vacante" : Limpio(r.Fuente)))
            .Take(5).ToList();
        var alertas = (b.Alertas ?? []).Select(Limpio).Where(a => a.Length > 0 && !filtro.EsDiscriminatorio(a)).Take(5).ToList();

        if (!tareas.Any(t => t.Responsable == "SST" || t.Titulo.Contains("seguridad", StringComparison.OrdinalIgnoreCase)))
        {
            alertas.Add("El plan no trae la inducción en seguridad y salud en el trabajo (SG-SST): agréguela.");
        }

        return new BorradorPlanInduccion(tareas, razones, alertas);
    }
}

/// <summary>El empleado quedó creado: la IA propone el plan de inducción y la respuesta vuelve a Vinculación por el outbox.</summary>
internal sealed class OnboardingSolicitadoConsumer(IPlanificadorInduccion agente, Persistence.IaDbContext db, TimeProvider reloj)
    : Selvana.SharedKernel.Messaging.IIntegrationEventConsumer<OnboardingSolicitado>
{
    public async Task Handle(OnboardingSolicitado e, CancellationToken cancellationToken)
    {
        var r = await agente.PlanificarAsync(e, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new PlanInduccionGenerado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, r.Value.Plan.Tareas, r.Value.Plan.Razones, r.Value.Plan.Alertas,
                r.Value.Modelo, r.Value.CostoUsd, null)
            : new PlanInduccionGenerado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, [], [], [], "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
