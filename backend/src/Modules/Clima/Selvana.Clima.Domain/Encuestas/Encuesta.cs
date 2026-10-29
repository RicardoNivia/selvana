using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Domain.Encuestas;

public enum EstadoEncuesta
{
    Borrador,
    Abierta,
    Cerrada,
}

/// <summary>Una afirmación que se responde de 1 (muy en desacuerdo) a 5 (muy de acuerdo), agrupada por dimensión.</summary>
public sealed class PreguntaClima
{
    public string Id { get; init; } = "";
    public string Dimension { get; init; } = "";
    public string Texto { get; init; } = "";
}

/// <summary>Un tema de los comentarios abiertos: cuántos lo mencionan, con qué tono, frases de ejemplo y una acción sugerida.</summary>
public sealed class TemaClima
{
    public string Nombre { get; init; } = "";

    /// <summary>Positivo, Negativo o Mixto.</summary>
    public string Sentimiento { get; init; } = "";
    public int Cantidad { get; init; }
    public List<string> Citas { get; init; } = [];
    public string Accion { get; init; } = "";
}

/// <summary>C2 · Análisis de los comentarios: de la IA (violeta hasta que alguien lo revise) o por reglas de palabras clave.</summary>
public sealed class AnalisisComentarios
{
    /// <summary>Preparando, Listo o Fallido.</summary>
    public string Estado { get; set; } = "";

    /// <summary>«IA» o «Reglas».</summary>
    public string Origen { get; set; } = "";
    public List<TemaClima> Temas { get; set; } = [];
    public string Resumen { get; set; } = "";
    public string? Modelo { get; set; }
    public decimal CostoUsd { get; set; }
    public string? Error { get; set; }
    public int Comentarios { get; set; }
    public string? RevisadoPor { get; set; }
    public DateTimeOffset En { get; set; }
}

public static class EncuestaErrors
{
    public static readonly Error NoExiste = Error.NotFound("Clima.EncuestaNoExiste", "Esa encuesta no existe en su empresa.");
    public static readonly Error EnlaceNoExiste = Error.NotFound("Clima.EnlaceNoExiste", "Este enlace de la encuesta ya no sirve. Pide uno nuevo a Recursos Humanos.");
    public static readonly Error NoEditable = Error.Conflict("Clima.NoEditable", "La encuesta ya está abierta: las preguntas no se cambian.");
    public static readonly Error Cerrada = Error.Conflict("Clima.Cerrada", "Esta encuesta ya cerró. Gracias por tu interés.");
    public static readonly Error SinPreguntas = Error.Validation("Clima.SinPreguntas", "La encuesta necesita al menos tres preguntas.");
    public static readonly Error Estado = Error.Conflict("Clima.Estado", "La encuesta no está en la etapa para hacer eso.");
    public static readonly Error PocasRespuestas = Error.Validation("Clima.PocasRespuestas",
        "Hacen falta al menos 5 comentarios para analizarlos sin exponer a nadie.");
    public static readonly Error Respuesta = Error.Validation("Clima.Respuesta", "Responde todas las preguntas de 1 a 5 antes de enviar.");
}

/// <summary>
/// C1 · Encuesta de clima anónima por enlace: afirmaciones de 1 a 5 por dimensión, eNPS opcional y una pregunta abierta. Quien responde
/// elige su área y su sede; los resultados se muestran solo para grupos de al menos <see cref="MinimoGrupo"/> respuestas.
/// </summary>
public sealed class Encuesta : AggregateRoot<Guid>
{
    public const int MinimoGrupo = 5;

    private Encuesta(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string Titulo { get; private set; } = "";
    public List<PreguntaClima> Preguntas { get; private set; } = [];
    public bool IncluyeEnps { get; private set; }
    public string PreguntaAbierta { get; private set; } = "";
    public List<string> Areas { get; private set; } = [];
    public List<string> Sedes { get; private set; } = [];
    public EstadoEncuesta Estado { get; private set; }
    public string? TokenHash { get; private set; }
    public DateTimeOffset? AbiertaEn { get; private set; }
    public DateTimeOffset? CerradaEn { get; private set; }
    public AnalisisComentarios? Analisis { get; private set; }
    public string CreadaPor { get; private init; } = "";
    public DateTimeOffset CreadaEn { get; private init; }

    public static Encuesta Crear(Guid empresaId, string titulo, IEnumerable<PreguntaClima> preguntas, bool incluyeEnps, string preguntaAbierta,
        IEnumerable<string> areas, IEnumerable<string> sedes, string por, DateTimeOffset ahora) => new(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            Titulo = titulo.Trim(),
            Preguntas = [.. preguntas],
            IncluyeEnps = incluyeEnps,
            PreguntaAbierta = preguntaAbierta.Trim(),
            Areas = Limpias(areas),
            Sedes = Limpias(sedes),
            Estado = EstadoEncuesta.Borrador,
            CreadaPor = por,
            CreadaEn = ahora,
        };

    public Result Editar(string titulo, IReadOnlyList<PreguntaClima> preguntas, bool incluyeEnps, string preguntaAbierta)
    {
        if (Estado != EstadoEncuesta.Borrador)
        {
            return Result.Failure(EncuestaErrors.NoEditable);
        }

        if (preguntas.Count < 3)
        {
            return Result.Failure(EncuestaErrors.SinPreguntas);
        }

        Titulo = titulo.Trim();
        Preguntas = [.. preguntas];
        IncluyeEnps = incluyeEnps;
        PreguntaAbierta = (preguntaAbierta ?? "").Trim();
        return Result.Success();
    }

    /// <summary>Abre la encuesta con un enlace nuevo (se guarda solo su huella). Si ya estaba abierta, el enlace anterior deja de servir.</summary>
    public Result Abrir(string tokenHash, DateTimeOffset ahora)
    {
        if (Estado == EstadoEncuesta.Cerrada)
        {
            return Result.Failure(EncuestaErrors.Estado);
        }

        if (Preguntas.Count < 3)
        {
            return Result.Failure(EncuestaErrors.SinPreguntas);
        }

        TokenHash = tokenHash;
        Estado = EstadoEncuesta.Abierta;
        AbiertaEn ??= ahora;
        return Result.Success();
    }

    public Result Cerrar(DateTimeOffset ahora)
    {
        if (Estado != EstadoEncuesta.Abierta)
        {
            return Result.Failure(EncuestaErrors.Estado);
        }

        Estado = EstadoEncuesta.Cerrada;
        CerradaEn = ahora;
        TokenHash = null;
        return Result.Success();
    }

    /// <summary>Valida una respuesta antes de guardarla: todas las preguntas de 1 a 5 y el eNPS de 0 a 10 si aplica.</summary>
    public Result<RespuestaClima> Responder(string area, string sede, IReadOnlyDictionary<string, int> puntajes, int? enps, string? comentario, DateTimeOffset ahora)
    {
        if (Estado != EstadoEncuesta.Abierta)
        {
            return EncuestaErrors.Cerrada;
        }

        if (Preguntas.Any(p => !puntajes.TryGetValue(p.Id, out var v) || v is < 1 or > 5) || (IncluyeEnps && enps is not (>= 0 and <= 10)))
        {
            return EncuestaErrors.Respuesta;
        }

        return RespuestaClima.Crear(this, Areas.Contains(area) ? area : "", Sedes.Contains(sede) ? sede : "",
            Preguntas.ToDictionary(p => p.Id, p => puntajes[p.Id]), IncluyeEnps ? enps : null, comentario, ahora);
    }

    public Result PedirAnalisis(int comentarios, DateTimeOffset ahora)
    {
        if (comentarios < MinimoGrupo)
        {
            return Result.Failure(EncuestaErrors.PocasRespuestas);
        }

        Analisis = new AnalisisComentarios { Estado = "Preparando", Origen = "IA", Comentarios = comentarios, En = ahora };
        return Result.Success();
    }

    public void RegistrarAnalisis(string origen, IReadOnlyList<TemaClima> temas, string resumen, int comentarios, string? modelo, decimal costo, string? error,
        DateTimeOffset ahora) =>
        Analisis = new AnalisisComentarios
        {
            Estado = error is null ? "Listo" : "Fallido", Origen = origen, Temas = [.. temas], Resumen = resumen, Comentarios = comentarios, Modelo = modelo,
            CostoUsd = costo, Error = error, En = ahora,
        };

    /// <summary>Una persona revisó el análisis de la IA: deja de ser violeta.</summary>
    public void MarcarAnalisisRevisado(string por)
    {
        if (Analisis is { } a)
        {
            Analisis = new AnalisisComentarios
            {
                Estado = a.Estado, Origen = a.Origen, Temas = a.Temas, Resumen = a.Resumen, Comentarios = a.Comentarios, Modelo = a.Modelo, CostoUsd = a.CostoUsd,
                Error = a.Error, En = a.En, RevisadoPor = por,
            };
        }
    }

    private static List<string> Limpias(IEnumerable<string> valores) =>
        [.. valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Order()];
}

public sealed class PuntajePregunta
{
    public string PreguntaId { get; init; } = "";
    public int Valor { get; init; }
}

/// <summary>Una respuesta anónima: no guarda quién respondió ni la hora exacta, solo el día, el área y la sede que eligió.</summary>
public sealed class RespuestaClima : AggregateRoot<Guid>
{
    private RespuestaClima(Guid id) : base(id)
    {
    }

    public Guid EncuestaId { get; private init; }
    public Guid EmpresaId { get; private init; }
    public string Area { get; private init; } = "";
    public string Sede { get; private init; } = "";
    public List<PuntajePregunta> Puntajes { get; private init; } = [];
    public int? Enps { get; private init; }
    public string Comentario { get; private init; } = "";
    public DateOnly Fecha { get; private init; }

    internal static RespuestaClima Crear(Encuesta e, string area, string sede, IReadOnlyDictionary<string, int> puntajes, int? enps, string? comentario,
        DateTimeOffset ahora) => new(Guid.NewGuid())
        {
            EncuestaId = e.Id,
            EmpresaId = e.EmpresaId,
            Area = area,
            Sede = sede,
            Puntajes = [.. puntajes.Select(p => new PuntajePregunta { PreguntaId = p.Key, Valor = p.Value })],
            Enps = enps,
            Comentario = (comentario ?? "").Trim() is { Length: > 1000 } largo ? largo[..1000] : (comentario ?? "").Trim(),
            Fecha = DateOnly.FromDateTime(ahora.UtcDateTime),
        };
}
