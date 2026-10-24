using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Candidatos;

/// <summary>Columnas del tablero de la vacante. Contratar es de la Etapa 2 (vinculación): la selección termina en la terna aprobada.</summary>
public enum EtapaPostulacion
{
    Recibida,
    Preseleccion,
    Pruebas,
    Terna,
    Descartada,
}

/// <summary>Un movimiento en el tablero: de dónde a dónde, quién y por qué (el motivo es obligatorio al descartar).</summary>
public sealed class CambioEtapa
{
    public EtapaPostulacion De { get; init; }
    public EtapaPostulacion A { get; init; }
    public string Por { get; init; } = "";
    public string? Motivo { get; init; }
    public DateTimeOffset En { get; init; }
}

public static class EtapaErrors
{
    public static readonly Error SinMotivo = Error.Validation("Postulacion.SinMotivoDescarte", "Diga por qué descarta al candidato: queda en su historial y en la auditoría.");
    public static readonly Error Suprimido = Error.Conflict("Postulacion.Suprimida", "El titular pidió suprimir sus datos: su postulación no se puede mover.");
}
