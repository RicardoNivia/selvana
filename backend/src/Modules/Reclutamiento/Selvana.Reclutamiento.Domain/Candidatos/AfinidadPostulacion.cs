using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Candidatos;

public enum EstadoAfinidad
{
    SinCalcular,
    Calculando,
    Lista,
    Fallo,
}

public sealed class RazonPuntaje
{
    public string Signo { get; init; } = "+";
    public string Texto { get; init; } = "";
    public string Evidencia { get; init; } = "";
}

/// <summary>
/// Afinidad de la postulación con su vacante: lo que propuso el agente de ranking (con sus razones) y, si una persona lo ajustó,
/// el puntaje que manda, con su motivo. El de la IA no se borra: queda para medir el agente.
/// </summary>
public sealed class AfinidadPostulacion
{
    public EstadoAfinidad Estado { get; set; }
    public int? PuntajeIa { get; set; }
    public List<RazonPuntaje> Razones { get; set; } = [];
    public List<string> Cumplidos { get; set; } = [];
    public List<string> Faltantes { get; set; } = [];
    public string? Modelo { get; set; }
    public string? Mensaje { get; set; }
    public DateTimeOffset? CalculadaEn { get; set; }

    /// <summary>Razones que el filtro retiró por mencionar variables protegidas (para el reporte de sesgo).</summary>
    public int RazonesRetiradas { get; set; }
    public int? PuntajeAjustado { get; set; }
    public string? MotivoAjuste { get; set; }
    public string? AjustadoPor { get; set; }
    public DateTimeOffset? AjustadoEn { get; set; }

    public int? Puntaje => PuntajeAjustado ?? PuntajeIa;
}

public static class AfinidadErrors
{
    public static readonly Error NoExiste = Error.NotFound("Postulacion.NoExiste", "Esa postulación no existe.");
    public static readonly Error SinCalcular = Error.Conflict("Afinidad.SinCalcular", "La IA todavía no ha calculado la afinidad de esta postulación.");
    public static readonly Error PuntajeInvalido = Error.Validation("Afinidad.Puntaje", "El puntaje va de 0 a 100.");
    public static readonly Error SinMotivo = Error.Validation("Afinidad.SinMotivo", "Diga por qué ajusta el puntaje: queda en la auditoría y así la IA aprende.");
}
