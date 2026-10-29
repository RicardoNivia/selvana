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

/// <summary>Lo que devuelve el modelo; se valida (pesos que suman 100, sin variables protegidas) antes de volver a Desempeño.</summary>
public sealed record BorradorFormulario(IReadOnlyList<CompetenciaPropuesta> Competencias, IReadOnlyList<ObjetivoPropuesto> Objetivos, IReadOnlyList<RazonRedaccion> Razones);

public sealed record ResultadoFormulario(BorradorFormulario Formulario, string Modelo, decimal CostoUsd);

public interface IDisenadorEvaluaciones
{
    Task<Result<ResultadoFormulario>> DisenarAsync(FormularioCargoSolicitado solicitud, CancellationToken cancellationToken);
}

/// <summary>
/// D1 y D2 · Diseñador de evaluaciones: con el cargo propone las competencias (con su peso) y los objetivos medibles del periodo.
/// No recibe datos de personas. Es una propuesta: RRHH la ajusta y la aprueba.
/// </summary>
internal sealed class DisenadorEvaluacionesAgente(IChatClient ia, IFiltroVariablesProtegidas filtro) : IDisenadorEvaluaciones
{
    public const string Nombre = "disenador-evaluaciones";

    private const string Instrucciones = """
        Eres el diseñador de evaluaciones de desempeño de Selvana, un software de talento humano colombiano.
        Con el CARGO y el TIPO DE CICLO propones el formulario de evaluación, en español de Colombia.

        "competencias": de 4 a 6. Cada una con "nombre" (2 a 4 palabras), "descripcion" (una frase con la conducta observable en este cargo)
        y "peso" (entero). Los pesos suman EXACTAMENTE 100. Incluye las técnicas del cargo y las de comportamiento (responsabilidad, trabajo en equipo…).
        "objetivos": de 2 a 4, medibles en el periodo. Cada uno con "descripcion" (empieza con verbo), "meta" (con número y unidad: "≥ 95 % mensual",
        "0 accidentes") y "peso" (entero). Los pesos suman EXACTAMENTE 100.
        Si el ciclo es de periodo de prueba, los objetivos son de aprendizaje del cargo; si es de fin de misión, del resultado de la misión.
        "razones": 3 a 5 decisiones del formulario, con "signo" ("+" o "−"), "texto" corto y "fuente" ("Cargo", "Tipo de ciclo", "Buenas prácticas").

        Reglas:
        - Nunca menciones edad, género, estado civil, embarazo, hijos, religión, origen ni salud.
        - Nada genérico si el cargo permite algo concreto.
        """;

    public async Task<Result<ResultadoFormulario>> DisenarAsync(FormularioCargoSolicitado s, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(s);
        var mensajes = new List<ChatMessage>
        {
            new(ChatRole.System, Instrucciones),
            new(ChatRole.User, $"CARGO: {s.Cargo}\nTIPO DE CICLO: {s.TipoCiclo}\nPESO DE LOS OBJETIVOS EN EL PUNTAJE: {s.PesoObjetivos} %"),
        };
        // Sin datos de personas: puede ir a los modelos gratuitos de la cadena (ADR 0007)
        var opciones = new ContextoIa(s.EmpresaId, Nombre, ContieneDatosPersonales: false).AplicarA(new ChatOptions { MaxOutputTokens = 3_000 });

        ChatResponse<BorradorFormulario> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<BorradorFormulario>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Use la plantilla base o arme el formulario a mano.");
        }

        if (!respuesta.TryGetResult(out var b) || b?.Competencias is not { Count: >= 3 })
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió un formulario completo. Pídalo de nuevo o use la plantilla base.");
        }

        var limpio = Validar(b, filtro);
        if (limpio.Competencias.Count < 3)
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió competencias válidas. Use la plantilla base o arme el formulario a mano.");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoFormulario(limpio, respuesta.ModelId ?? "", costo);
    }

    /// <summary>Limpia textos, descarta lo que tenga variables protegidas y ajusta los pesos para que sumen 100.</summary>
    internal static BorradorFormulario Validar(BorradorFormulario b, IFiltroVariablesProtegidas filtro)
    {
        static string L(string? t, int maximo) => CalificadorAfinidadAgente.SinEscapes(t).Trim() is var x && x.Length > maximo ? x[..maximo] : CalificadorAfinidadAgente.SinEscapes(t).Trim();
        var competencias = (b.Competencias ?? []).Select(c => new CompetenciaPropuesta(L(c.Nombre, 120), L(c.Descripcion, 400), Math.Max(1, c.Peso)))
            .Where(c => c.Nombre.Length > 0 && !filtro.EsDiscriminatorio(c.Nombre + " " + c.Descripcion)).Take(6).ToList();
        var objetivos = (b.Objetivos ?? []).Select(o => new ObjetivoPropuesto(L(o.Descripcion, 300), L(o.Meta, 200), Math.Max(1, o.Peso)))
            .Where(o => o.Descripcion.Length > 0 && !filtro.EsDiscriminatorio(o.Descripcion)).Take(4).ToList();
        var razones = (b.Razones ?? []).Where(r => !string.IsNullOrWhiteSpace(r.Texto) && !filtro.EsDiscriminatorio(r.Texto))
            .Select(r => new RazonRedaccion(r.Signo is "-" or "−" ? "−" : "+", L(r.Texto, 300), string.IsNullOrWhiteSpace(r.Fuente) ? "Cargo" : L(r.Fuente, 60))).Take(5).ToList();
        return new BorradorFormulario(Normalizar(competencias, c => c.Peso, (c, p) => c with { Peso = p }),
            Normalizar(objetivos, o => o.Peso, (o, p) => o with { Peso = p }), razones);
    }

    /// <summary>Reparte los pesos en proporción para que sumen exactamente 100 (el último absorbe el redondeo).</summary>
    internal static List<T> Normalizar<T>(List<T> items, Func<T, int> peso, Func<T, int, T> conPeso)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var total = items.Sum(peso);
        var nuevos = items.Select(i => conPeso(i, Math.Max(1, (int)Math.Round(peso(i) * 100.0 / total)))).ToList();
        var diferencia = 100 - nuevos.Sum(peso);
        nuevos[^1] = conPeso(nuevos[^1], Math.Max(1, peso(nuevos[^1]) + diferencia));
        return nuevos;
    }
}

/// <summary>Se pidió el formulario de un cargo: la propuesta vuelve a Desempeño por el outbox.</summary>
internal sealed class FormularioCargoSolicitadoConsumer(IDisenadorEvaluaciones agente, Persistence.IaDbContext db, TimeProvider reloj)
    : Selvana.SharedKernel.Messaging.IIntegrationEventConsumer<FormularioCargoSolicitado>
{
    public async Task Handle(FormularioCargoSolicitado e, CancellationToken cancellationToken)
    {
        var r = await agente.DisenarAsync(e, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new FormularioCargoGenerado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.CicloId, e.Cargo, r.Value.Formulario.Competencias, r.Value.Formulario.Objetivos,
                r.Value.Formulario.Razones, r.Value.Modelo, r.Value.CostoUsd, null)
            : new FormularioCargoGenerado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.CicloId, e.Cargo, [], [], [], "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
