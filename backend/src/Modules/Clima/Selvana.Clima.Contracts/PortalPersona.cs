using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Contracts;

/// <summary>C3 · Un pulso de ingreso pendiente o respondido, tal como lo ve el empleado en su portal.</summary>
public sealed record PulsoDeLaPersona(Guid PulsoId, int Hito, DateOnly PendienteDesde, bool Respondido);

/// <summary>Lo que el empleado responde en su pulso.</summary>
public sealed record RespuestaPulsoDeLaPersona(int Satisfaccion, int Claridad, int Apoyo, int Recomendaria, string? Comentario);

public interface IPulsosDeLaPersona
{
    /// <summary>Sus pulsos, por cédula y correo a la vez.</summary>
    Task<IReadOnlyList<PulsoDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken);

    Task<Result> ResponderAsync(Guid pulsoId, string documento, string correo, RespuestaPulsoDeLaPersona respuesta, CancellationToken cancellationToken);
}
