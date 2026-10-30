using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Contracts;

/// <summary>La última calificación de cada empleado (para las señales de rotación de Clima).</summary>
public sealed record DesempenoReciente(Guid EmpleadoId, decimal Puntaje, string? Potencial, int? Caja, string Ciclo);

public interface IDesempenoReciente
{
    Task<IReadOnlyList<DesempenoReciente>> UltimosAsync(Guid empresaId, CancellationToken cancellationToken);
}

/// <summary>E4 · La evaluación de un ciclo abierto de cada persona del equipo, para «Mi equipo» del líder.</summary>
public sealed record EvaluacionDelEquipo(Guid EmpleadoId, Guid EvaluacionId, string Ciclo, DateOnly Hasta, string Estado, decimal? Puntaje);

public interface IEvaluacionesDelEquipo
{
    Task<IReadOnlyList<EvaluacionDelEquipo>> AbiertasAsync(Guid empresaId, IReadOnlyCollection<Guid> empleados, CancellationToken cancellationToken);
}

public sealed record CompetenciaParaAutoevaluar(string Nombre, string Descripcion, int? MiCalificacion);

public sealed record ObjetivoDelEmpleado(string Descripcion, string Meta, int Avance);

/// <summary>180° y 360° · Lo que el empleado ve de su evaluación en el portal: sus competencias para autoevaluarse y sus objetivos.</summary>
public sealed record AutoevaluacionDeLaPersona(
    Guid EvaluacionId, string Ciclo, string Cargo, DateOnly Hasta, bool Respondida, IReadOnlyList<CompetenciaParaAutoevaluar> Competencias,
    IReadOnlyList<ObjetivoDelEmpleado> Objetivos, string Comentario);

public interface IEvaluacionesDeLaPersona
{
    /// <summary>Las de ciclos abiertos que llevan autoevaluación, por cédula y correo a la vez.</summary>
    Task<IReadOnlyList<AutoevaluacionDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken);

    Task<Result> AutoevaluarAsync(Guid evaluacionId, string documento, string correo, IReadOnlyDictionary<string, int> calificaciones, string comentario,
        CancellationToken cancellationToken);
}
