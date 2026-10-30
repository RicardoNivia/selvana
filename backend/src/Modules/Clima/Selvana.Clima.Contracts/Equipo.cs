namespace Selvana.Clima.Contracts;

/// <summary>E4 · El riesgo de rotación de una persona del equipo: nivel, señales con evidencia y acciones sugeridas (reglas explicadas).</summary>
public sealed record RiesgoDelEquipo(Guid EmpleadoId, string Nivel, int Puntos, IReadOnlyList<(string Texto, string Evidencia)> Senales, IReadOnlyList<string> Acciones);

public interface IRiesgoDelEquipo
{
    Task<IReadOnlyList<RiesgoDelEquipo>> CalcularAsync(Guid empresaId, IReadOnlyCollection<Guid> empleados, CancellationToken cancellationToken);
}
