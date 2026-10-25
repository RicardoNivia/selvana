using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using Selvana.AI.Agentes.Reclutamiento;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.AI.Gateway;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Contracts.Ia;

namespace Selvana.AI.Agentes.Vinculacion;

/// <summary>Lo que devuelve el modelo; se valida antes de volver a Vinculación.</summary>
public sealed record PropuestaInduccion(IReadOnlyList<TareaInduccionPropuesta> Tareas, IReadOnlyList<RazonRedaccion> Razones, IReadOnlyList<string> Alertas);

public sealed record ResultadoInduccion(PropuestaInduccion Propuesta, string Modelo, decimal CostoUsd);

public interface IPlanificadorInduccion
{
    Task<Result<ResultadoInduccion>> PlanearAsync(Guid empresaId, DatosInduccion datos, CancellationToken cancellationToken);
}

/// <summary>
/// Agente planificador de inducción: con el cargo y las condiciones del ingreso propone las tareas propias del cargo (quién, qué y cuándo)
/// que se suman a las legales que ya puso Selvana por reglas. No recibe datos de la persona. Es un borrador: RRHH lo ajusta y lo aprueba.
/// </summary>
internal sealed class PlanificadorInduccionAgente(IChatClient ia, IFiltroVariablesProtegidas filtro) : IPlanificadorInduccion
{
    public const string Nombre = "planificador-induccion";
    public const int MaximoTareas = 12;

    private static readonly string[] Responsables = ["Recursos Humanos", "Líder inmediato", "Seguridad y salud en el trabajo", "Tecnología", "Trabajador"];

    private const string Instrucciones = """
        Eres el planificador de inducción de Selvana, un software de talento humano colombiano.
        Un trabajador nuevo entra al CARGO indicado. Selvana ya puso en el plan las tareas legales (inducción en SST, reglamento interno,
        EPP si aplica, evaluación del periodo de prueba): NO las repitas. Propón de 6 a 10 tareas propias del cargo para sus primeros 90 días.

        Para cada tarea en "tareas":
        - "titulo": acción concreta y corta, en infinitivo ("Recorrer la bodega y las zonas de cargue", "Crear el usuario del WMS").
        - "descripcion": una o dos frases con qué se hace y qué evidencia queda.
        - "responsable": EXACTAMENTE uno de: "Recursos Humanos", "Líder inmediato", "Seguridad y salud en el trabajo", "Tecnología", "Trabajador".
        - "dia": días desde el ingreso (0 = primer día; -2 = dos días antes, para preparar el puesto o los accesos; máximo 90).
        - "fuente": de dónde sale ("Descripción del cargo", "Requisito: licencia de montacargas", "Buena práctica de inducción").

        Reglas:
        - Ordena la primera semana con cuidado: preparación antes del ingreso, bienvenida y puesto el día 0, acompañamiento la primera semana,
          seguimientos a los 30, 60 y 90 días con el líder.
        - Usa solo lo que dicen el cargo, la descripción y los requisitos; no inventes software, certificaciones ni beneficios que no aparezcan.
          Si hace falta un dato, menciónalo en "alertas".
        - Nunca menciones edad, género, estado civil, embarazo, hijos, religión, origen ni salud.
        - "razones": 3 a 5 decisiones del plan, con "signo" ("+" o "−"), "texto" corto y "fuente".
        - "alertas": lo que RRHH debe revisar (por ejemplo, "La vacante no dice qué sistema usa: confirme los accesos"). Lista vacía si no hay nada.
        """;

    public async Task<Result<ResultadoInduccion>> PlanearAsync(Guid empresaId, DatosInduccion datos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var mensajes = new List<ChatMessage> { new(ChatRole.System, Instrucciones), new(ChatRole.User, Pregunta(datos)) };
        // Solo el cargo y sus condiciones, sin datos de la persona: puede ir a modelos gratis
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: false).AplicarA(new ChatOptions { MaxOutputTokens = 3_000 });

        ChatResponse<PropuestaInduccion> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<PropuestaInduccion>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Pida de nuevo el plan desde el proceso o complételo a mano.");
        }

        if (!respuesta.TryGetResult(out var propuesta) || propuesta?.Tareas is not { Count: > 0 })
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió tareas. Pida de nuevo el plan o complételo a mano.");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoInduccion(Validar(propuesta, datos, filtro), respuesta.ModelId ?? "", costo);
    }

    internal static string Pregunta(DatosInduccion d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INGRESO");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Empleador: {d.Empresa}");
        if (!string.IsNullOrWhiteSpace(d.EmpresaUsuaria))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Misión en la empresa usuaria: {d.EmpresaUsuaria}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- Cargo: {d.Cargo}");
        if (!string.IsNullOrWhiteSpace(d.Area))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Área: {d.Area}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- Sede: {d.Sede}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Contrato: {d.TipoContrato} · jornada: {d.Jornada}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Fecha de ingreso: {d.FechaIngreso:dd/MM/yyyy} · periodo de prueba: {(d.PeriodoPruebaDias > 0 ? $"{d.PeriodoPruebaDias} días" : "no tiene")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Clase de riesgo ARL: {d.ClaseRiesgoArl}");
        sb.AppendLine();
        sb.AppendLine("DESCRIPCIÓN DEL CARGO");
        sb.AppendLine(d.Descripcion);
        if (d.Requisitos.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("REQUISITOS");
            foreach (var r in d.Requisitos)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {r}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("TAREAS QUE YA ESTÁN EN EL PLAN (no las repitas)");
        foreach (var t in d.Existentes)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Día {t.Dia}: {t.Titulo} ({t.Responsable})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Limpia la propuesta: quita escapes, recorta, normaliza el responsable a la lista, limita el día, descarta las que repiten una tarea legal
    /// y retira lo que mencione variables protegidas.
    /// </summary>
    internal static PropuestaInduccion Validar(PropuestaInduccion p, DatosInduccion datos, IFiltroVariablesProtegidas filtro)
    {
        static string Limpio(string? t) => CalificadorAfinidadAgente.SinEscapes(t).Trim();
        static string Recortar(string t, int maximo) => t.Length > maximo ? t[..maximo] : t;
        static string Clave(string t) => new([.. t.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(char.IsLetterOrDigit)]);

        var existentes = datos.Existentes.Select(e => Clave(e.Titulo)).ToHashSet();
        var tareas = new List<TareaInduccionPropuesta>();
        foreach (var t in p.Tareas ?? [])
        {
            var titulo = Recortar(Limpio(t.Titulo), 200);
            var descripcion = Recortar(Limpio(t.Descripcion), 1000);
            if (titulo.Length == 0 || filtro.EsDiscriminatorio(titulo) || filtro.EsDiscriminatorio(descripcion) || existentes.Contains(Clave(titulo))
                || tareas.Any(x => Clave(x.Titulo) == Clave(titulo)))
            {
                continue;
            }

            var responsable = Responsables.FirstOrDefault(r => string.Equals(r, Limpio(t.Responsable), StringComparison.OrdinalIgnoreCase)) ?? "Recursos Humanos";
            tareas.Add(new TareaInduccionPropuesta(titulo, descripcion, responsable, Math.Clamp(t.Dia, -5, 90),
                string.IsNullOrWhiteSpace(t.Fuente) ? "Descripción del cargo" : Recortar(Limpio(t.Fuente), 120)));
        }

        var razones = (p.Razones ?? []).Where(r => !string.IsNullOrWhiteSpace(r.Texto) && !filtro.EsDiscriminatorio(r.Texto))
            .Select(r => new RazonRedaccion(r.Signo is "-" or "−" ? "−" : "+", Limpio(r.Texto), string.IsNullOrWhiteSpace(r.Fuente) ? "Cargo" : Limpio(r.Fuente)))
            .Take(5).ToList();
        var alertas = (p.Alertas ?? []).Select(Limpio).Where(a => a.Length > 0 && !filtro.EsDiscriminatorio(a)).Take(5).ToList();
        return new PropuestaInduccion([.. tareas.OrderBy(t => t.Dia).Take(MaximoTareas)], razones, alertas);
    }
}

/// <summary>Se creó el empleado: el planificador propone las tareas y la respuesta vuelve a Vinculación por el outbox.</summary>
internal sealed class PlanInduccionSolicitadoConsumer(IPlanificadorInduccion agente, Persistence.IaDbContext db, TimeProvider reloj)
    : Selvana.SharedKernel.Messaging.IIntegrationEventConsumer<PlanInduccionSolicitado>
{
    public async Task Handle(PlanInduccionSolicitado e, CancellationToken cancellationToken)
    {
        var r = await agente.PlanearAsync(e.EmpresaId, e.Datos, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new PlanInduccionRedactado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, r.Value.Propuesta.Tareas, r.Value.Propuesta.Razones, r.Value.Propuesta.Alertas,
                r.Value.Modelo, r.Value.CostoUsd, null)
            : new PlanInduccionRedactado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, [], [], [], "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
