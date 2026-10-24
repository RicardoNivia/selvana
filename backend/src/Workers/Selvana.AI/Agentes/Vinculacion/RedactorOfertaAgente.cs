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
public sealed record BorradorOferta(string Carta, IReadOnlyList<ClausulaRedactada> Clausulas, IReadOnlyList<RazonRedaccion> Razones, IReadOnlyList<string> Alertas);

public sealed record ResultadoOferta(BorradorOferta Borrador, string Modelo, decimal CostoUsd);

public interface IRedactorOferta
{
    Task<Result<ResultadoOferta>> RedactarAsync(Guid empresaId, CondicionesOferta condiciones, CancellationToken cancellationToken);
}

/// <summary>
/// Agente redactor de ofertas: con las condiciones que aprobó RRHH escribe la carta oferta y el contrato laboral (Colombia),
/// sin inventar beneficios ni cifras. No recibe el nombre ni el documento del trabajador: usa marcas que Selvana reemplaza.
/// Es un borrador: RRHH lo revisa, lo ajusta y lo aprueba.
/// </summary>
internal sealed class RedactorOfertaAgente(IChatClient ia, IFiltroVariablesProtegidas filtro, TimeProvider reloj) : IRedactorOferta
{
    public const string Nombre = "redactor-ofertas";
    public const string Trabajador = "[TRABAJADOR]";
    public const string Documento = "[DOCUMENTO]";

    private static readonly CultureInfo Colombia = new("es-CO");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private const string Instrucciones = """
        Eres el redactor de ofertas laborales de Selvana, un software de talento humano colombiano.
        Con las CONDICIONES que aprobó Recursos Humanos escribes dos cosas en español de Colombia:

        1. "carta": la carta oferta para el candidato, cálida y clara, tuteándolo, de 120 a 220 palabras. Empieza con "Hola [TRABAJADOR]:".
           Di el cargo, la empresa, el tipo de contrato, el salario, la fecha de ingreso y qué sigue (aceptar la oferta en el enlace, luego enviar documentos).
        2. "clausulas": el contrato laboral, de 8 a 12 cláusulas, cada una con "titulo" ("PRIMERA. Objeto", "SEGUNDA. Duración"…) y "texto".
           Incluye: partes (el empleador y "[TRABAJADOR], identificado con cédula [DOCUMENTO]"), objeto y funciones, duración o tipo de contrato,
           periodo de prueba, salario y forma de pago, auxilio de transporte si aplica, jornada, lugar de trabajo, obligaciones de las partes,
           terminación y tratamiento de datos personales (Ley 1581 de 2012). Si es misión en empresa usuaria, nombra a la empresa usuaria y la labor.

        Reglas:
        - Usa EXACTAMENTE las cifras y fechas de las condiciones (pesos con punto de miles: $1.750.905; fechas dd/mm/aaaa). No inventes bonos,
          beneficios, comisiones ni cláusulas de exclusividad o permanencia que las condiciones no digan.
        - Nunca escribas un nombre ni un número de documento: usa solo [TRABAJADOR] y [DOCUMENTO].
        - Nunca menciones edad, género, estado civil, embarazo, hijos, religión, origen ni salud.
        - "razones": 3 a 5 decisiones de redacción, cada una con "signo" ("+" o "−"), "texto" corto y "fuente" ("Condiciones", "Vacante", "Art. 46 CST", etc.).
        - "alertas": lo que RRHH debe revisar antes de aprobar (por ejemplo, una condición ambigua). Lista vacía si no hay nada.
        """;

    public async Task<Result<ResultadoOferta>> RedactarAsync(Guid empresaId, CondicionesOferta condiciones, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condiciones);
        var mensajes = new List<ChatMessage>
        {
            new(ChatRole.System, Instrucciones),
            new(ChatRole.User, Pregunta(condiciones, DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))),
        };
        // Sin nombre ni documento, pero el salario y las condiciones son de una persona: nunca a modelos gratis
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: true).AplicarA(new ChatOptions { MaxOutputTokens = 6_000 });

        ChatResponse<BorradorOferta> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<BorradorOferta>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Pida de nuevo la redacción desde el proceso.");
        }

        if (!respuesta.TryGetResult(out var borrador) || borrador?.Clausulas is not { Count: > 0 } || string.IsNullOrWhiteSpace(borrador.Carta))
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió una oferta completa. Pida de nuevo la redacción.");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoOferta(Validar(borrador, condiciones, filtro), respuesta.ModelId ?? "", costo);
    }

    internal static string Pesos(decimal valor) => valor.ToString("$#,##0", Colombia);

    internal static string Pregunta(CondicionesOferta c, DateOnly? hoy = null)
    {
        var sb = new StringBuilder();
        if (hoy is { } h)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"FECHA DE HOY: {h:dd/MM/yyyy}.");
            sb.AppendLine();
        }

        sb.AppendLine("CONDICIONES APROBADAS POR RRHH");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Empleador: {c.Empresa}");
        if (string.IsNullOrWhiteSpace(c.EmpresaUsuaria))
        {
            sb.AppendLine("- Vinculación directa con el empleador (no es una misión en empresa usuaria).");
        }
        if (!string.IsNullOrWhiteSpace(c.EmpresaUsuaria))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Empresa usuaria (misión): {c.EmpresaUsuaria}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- Cargo: {c.Cargo}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Ciudad / lugar de trabajo: {c.Ciudad}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Jornada: {c.Jornada}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Tipo de contrato: {c.TipoContrato}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Salario mensual: {Pesos(c.SalarioCop)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Auxilio de transporte: {(c.AuxilioTransporte ? "sí, el legal vigente" : "no aplica")} (ya lo calculó Selvana con el salario mínimo vigente; no lo pongas en duda)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Fecha de ingreso: {c.FechaIngreso:dd/MM/yyyy}");
        var fin = c.FechaFin is { } f ? f.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : c.TipoContrato.Contains("indefinido", StringComparison.OrdinalIgnoreCase) ? "no tiene: es a término indefinido"
            : "no tiene fecha: termina cuando se acabe la obra o labor";
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Fecha de terminación: {fin}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Periodo de prueba: {(c.PeriodoPruebaDias > 0 ? $"{c.PeriodoPruebaDias} días" : "no tiene")}");
        if (!string.IsNullOrWhiteSpace(c.Labor))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Obra o labor contratada: {c.Labor}");
        }

        sb.AppendLine();
        sb.AppendLine("VACANTE (para el objeto y las funciones)");
        sb.AppendLine(JsonSerializer.Serialize(new { c.Descripcion, c.Requisitos }, Json));
        return sb.ToString();
    }

    /// <summary>
    /// Limpia el borrador: quita escapes, recorta tamaños, retira razones con variables protegidas y agrega alertas si el texto
    /// no trae el salario o la fecha de ingreso aprobados.
    /// </summary>
    internal static BorradorOferta Validar(BorradorOferta b, CondicionesOferta c, IFiltroVariablesProtegidas filtro)
    {
        static string Limpio(string? t) => CalificadorAfinidadAgente.SinEscapes(t).Trim();
        static string Recortar(string t, int maximo) => t.Length > maximo ? t[..maximo] : t;

        var clausulas = (b.Clausulas ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Texto))
            .Select(x => new ClausulaRedactada(Recortar(Limpio(x.Titulo), 200) is { Length: > 0 } t ? t : "Cláusula", Recortar(Limpio(x.Texto), 4000)))
            .Take(15).ToList();
        var razones = (b.Razones ?? []).Where(r => !string.IsNullOrWhiteSpace(r.Texto) && !filtro.EsDiscriminatorio(r.Texto))
            .Select(r => new RazonRedaccion(r.Signo is "-" or "−" ? "−" : "+", Limpio(r.Texto), string.IsNullOrWhiteSpace(r.Fuente) ? "Condiciones" : Limpio(r.Fuente)))
            .Take(5).ToList();
        var alertas = (b.Alertas ?? []).Select(Limpio).Where(a => a.Length > 0 && !filtro.EsDiscriminatorio(a)).Take(5).ToList();

        var texto = Limpio(b.Carta) + "\n" + string.Join("\n", clausulas.Select(x => x.Texto));
        var salario = Pesos(c.SalarioCop);
        if (!texto.Contains(salario, StringComparison.Ordinal) && !texto.Contains(salario.Replace(".", ",", StringComparison.Ordinal), StringComparison.Ordinal))
        {
            alertas.Add($"El texto no menciona el salario aprobado ({salario}): revíselo.");
        }

        if (!texto.Contains(c.FechaIngreso.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            alertas.Add($"El texto no trae la fecha de ingreso aprobada ({c.FechaIngreso:dd/MM/yyyy}): revísela.");
        }

        if (!texto.Contains(Trabajador, StringComparison.Ordinal))
        {
            alertas.Add("El contrato no identifica al trabajador: revise la cláusula de las partes.");
        }

        return new BorradorOferta(Limpio(b.Carta), clausulas, razones, alertas);
    }
}

/// <summary>RRHH aprobó la contratación: el agente redacta y el borrador vuelve a Vinculación por el outbox.</summary>
internal sealed class OfertaSolicitadaConsumer(IRedactorOferta agente, Persistence.IaDbContext db, TimeProvider reloj)
    : Selvana.SharedKernel.Messaging.IIntegrationEventConsumer<OfertaSolicitada>
{
    public async Task Handle(OfertaSolicitada e, CancellationToken cancellationToken)
    {
        var r = await agente.RedactarAsync(e.EmpresaId, e.Condiciones, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new OfertaRedactada(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, r.Value.Borrador.Carta, r.Value.Borrador.Clausulas, r.Value.Borrador.Razones,
                r.Value.Borrador.Alertas, r.Value.Modelo, r.Value.CostoUsd, null)
            : new OfertaRedactada(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, "", [], [], [], "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
