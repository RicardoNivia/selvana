using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Selvana.AI.Gateway;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.RedactorVacantes;

public interface IRedactorVacantes
{
    Task<Result<ResultadoBorradorVacante>> RedactarAsync(SolicitudBorradorVacante solicitud, CancellationToken cancellationToken);
}

public static class RedactorVacantesErrors
{
    public static readonly Error FraseVacia = Error.Validation("Vacante.FraseVacia", "Describe la vacante en una frase.");
    public static readonly Error SinModelos = Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Intenta de nuevo en unos minutos.");
    public static readonly Error RespuestaInvalida = Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió un borrador válido.");
}

/// <summary>
/// Agente de prueba de la Etapa 0: convierte una frase del líder en un borrador de vacante con explicación.
/// La frase no lleva datos personales, así que puede usar los modelos gratis.
/// </summary>
internal sealed class RedactorVacantesAgente(
    IChatClient ia, IFiltroVariablesProtegidas filtro, IOptions<RedactorVacantesOptions> options) : IRedactorVacantes
{
    public const string Nombre = "redactor-vacantes";

    private const string Instrucciones = """
        Eres el agente redactor de vacantes de Selvana, un software de talento humano para empresas colombianas.
        A partir de la frase de un líder, redacta un borrador de vacante en español de Colombia.

        Reglas:
        - Usa solo lo que dice la frase. Lo que no diga y necesites, complétalo con un valor razonable y anótalo en "supuestos".
        - Nunca pidas ni sugieras requisitos de edad, género, estado civil, hijos, apariencia, foto, religión, barrio, estrato ni dirección exacta.
        - El rango salarial es mensual en pesos colombianos (COP), coherente con el cargo y la ciudad; no puede ser inferior al salario mínimo vigente.
        - Entre 3 y 5 razones que expliquen las decisiones principales. Cada razón cita su evidencia: el fragmento literal de la frase entre comillas, o "Supuesto" si no sale de la frase.
        - Entre 3 y 5 preguntas filtro cortas, para hacer por WhatsApp, que se respondan con sí o no o con un dato corto.
        - Requisitos y competencias concretos y verificables.
        - Una vacante es de UN solo cargo; "cupos" son las posiciones disponibles de ese cargo (si la frase dice "25 auxiliares", cupos = 25).
        - Antes de redactar, revisa si puedes. Si la frase pide dos o más cargos distintos (por ejemplo "un contador y dos mensajeros"),
          o le falta algo imprescindible que no se puede suponer con sensatez (qué cargo es), NO redactes: devuelve en "aclaraciones"
          entre 1 y 3 preguntas cortas para la persona (por ejemplo "¿Cuál cargo quiere crear primero? Cada vacante es de un solo cargo.")
          y deja los demás campos vacíos. Si puedes redactar, "aclaraciones" va vacío y lo que supongas va en "supuestos".
        """;

    public async Task<Result<ResultadoBorradorVacante>> RedactarAsync(SolicitudBorradorVacante solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (string.IsNullOrWhiteSpace(solicitud.Frase))
        {
            return RedactorVacantesErrors.FraseVacia;
        }

        var contexto = new ContextoIa(solicitud.EmpresaId, Nombre, ContieneDatosPersonales: false);
        var minimo = options.Value.SalarioMinimoCop;
        var mensajes = new List<ChatMessage>
        {
            new(ChatRole.System, Instrucciones + $"\n- El salario mínimo mensual vigente es {minimo:N0} COP." + TextoPerfil(solicitud.Perfil)),
            new(ChatRole.User, solicitud.Frase.Trim()),
        };

        ChatResponse<BorradorVacante> respuesta;
        try
        {
            // Sin tope explícito algunos adaptadores cortan en 1.024 tokens y el JSON queda incompleto.
            var opciones = contexto.AplicarA(new ChatOptions { MaxOutputTokens = 16_000 });
            respuesta = await ia.GetResponseAsync<BorradorVacante>(mensajes, opciones, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return RedactorVacantesErrors.SinModelos;
        }

        if (!respuesta.TryGetResult(out var borrador) || borrador is null)
        {
            return RedactorVacantesErrors.RespuestaInvalida;
        }

        var propiedades = respuesta.AdditionalProperties;
        var proveedor = propiedades?.TryGetValue(CadenaModelosChatClient.PropiedadProveedor, out var p) == true ? p?.ToString() ?? "" : "";
        var costoUsd = propiedades?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal costo ? costo : 0m;

        // La IA pregunta antes de redactar (por ejemplo, la frase pide dos cargos): vuelve solo con las aclaraciones
        var aclaraciones = (borrador.Aclaraciones ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Take(3).ToList();
        if (aclaraciones.Count > 0)
        {
            var vacio = new BorradorVacante("", "", "", 0, "", "", [], [], [], new RangoSalarialSugerido(0, 0, ""), [], [], aclaraciones);
            return new ResultadoBorradorVacante(vacio, [], proveedor, respuesta.ModelId ?? "", costoUsd);
        }

        if (string.IsNullOrWhiteSpace(borrador.Titulo))
        {
            return RedactorVacantesErrors.RespuestaInvalida;
        }

        var retirados = borrador.Requisitos.Concat(borrador.PreguntasFiltro).Where(filtro.EsDiscriminatorio).ToList();
        var limpio = AjustarAlMinimo(borrador with
        {
            Requisitos = [.. borrador.Requisitos.Where(r => !filtro.EsDiscriminatorio(r))],
            PreguntasFiltro = [.. borrador.PreguntasFiltro.Where(p => !filtro.EsDiscriminatorio(p))],
        }, minimo);

        return new ResultadoBorradorVacante(limpio, retirados, proveedor, respuesta.ModelId ?? "", costoUsd);
    }

    /// <summary>
    /// Instrucciones cuando la vacante parte de un cargo del catálogo: el perfil es la base, la frase lo ajusta
    /// y las razones citan «Perfil del cargo». Un perfil en borrador se usa con cuidado porque RRHH aún no lo aprobó.
    /// </summary>
    internal static string TextoPerfil(PerfilCargoVacante? perfil)
    {
        if (perfil is null)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"\n\nLa vacante parte del cargo «{perfil.Cargo}» (área: {perfil.Area}) del catálogo de la empresa.");
        sb.Append("\n- En \"cargo\" usa exactamente ese nombre.");
        if (string.IsNullOrWhiteSpace(perfil.Mision) && perfil.Funciones.Count == 0 && perfil.Requisitos.Count == 0)
        {
            sb.Append("\n- El cargo aún no tiene perfil: redacta desde la frase.");
            return sb.ToString();
        }

        sb.Append(perfil.Aprobado
            ? "\n- Su perfil está APROBADO por RRHH: úsalo como base de la descripción y los requisitos. La frase del líder solo ajusta ciudad, cupos, jornada y lo que diga explícitamente."
            : "\n- Su perfil es un BORRADOR de la IA que RRHH no ha aprobado: tómalo como referencia, no como requisito obligatorio.");
        sb.Append("\n- Las razones que salgan del perfil citan como evidencia «Perfil del cargo».");
        sb.Append(CultureInfo.InvariantCulture, $"\nMisión: {perfil.Mision}");
        sb.Append("\nFunciones:");
        foreach (var f in perfil.Funciones.Take(12))
        {
            sb.Append(CultureInfo.InvariantCulture, $"\n  - {f}");
        }

        sb.Append("\nRequisitos del perfil:");
        foreach (var r in perfil.Requisitos.Take(12))
        {
            sb.Append(CultureInfo.InvariantCulture, $"\n  - {r}");
        }

        if (!string.IsNullOrWhiteSpace(perfil.RiesgoArl))
        {
            sb.Append(CultureInfo.InvariantCulture, $"\nRiesgo ARL: {perfil.RiesgoArl}");
        }

        return sb.ToString();
    }

    /// <summary>Un salario por debajo del mínimo legal no se sugiere nunca, diga lo que diga el modelo.</summary>
    internal static BorradorVacante AjustarAlMinimo(BorradorVacante borrador, decimal minimo)
    {
        var rango = borrador.RangoSalarial;
        if (minimo <= 0 || rango.MinimoCop >= minimo)
        {
            return borrador;
        }

        var maximo = Math.Max(rango.MaximoCop, minimo);
        return borrador with
        {
            RangoSalarial = rango with { MinimoCop = minimo, MaximoCop = maximo },
            Supuestos = [.. borrador.Supuestos, $"El rango sugerido quedaba bajo el salario mínimo; se ajustó a {minimo:N0} COP."],
        };
    }
}

public sealed class RedactorVacantesOptions
{
    public const string Section = "Ia:RedactorVacantes";

    /// <summary>Salario mínimo mensual legal vigente en COP. Se actualiza cada año con el decreto del Gobierno.</summary>
    public decimal SalarioMinimoCop { get; init; }
}
