using Selvana.Desempeno.Domain.Ciclos;
using Selvana.Desempeno.Domain.Evaluaciones;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Application.Abstracciones;

/// <summary>Quién hace la petición en la consola y en qué empresa trabaja.</summary>
public interface IUsuarioActual
{
    Guid EmpresaId { get; }
    string Nombre { get; }

    /// <summary>Empresas usuarias que ve; null = todas.</summary>
    IReadOnlyCollection<Guid>? EmpresasUsuarias { get; }
}

public static class AlcanceUsuario
{
    public static bool Ve(this IUsuarioActual usuario, Guid? empresaUsuariaId) =>
        empresaUsuariaId is not { } id || usuario.EmpresasUsuarias is null || usuario.EmpresasUsuarias.Contains(id);
}

/// <summary>Unidad de trabajo del módulo (esquema "desempeno").</summary>
public interface IUnidadDesempeno
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICiclos
{
    Task<Ciclo?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    void Agregar(Ciclo ciclo);
}

public interface IEvaluaciones
{
    Task<Evaluacion?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    /// <summary>Para las respuestas de la IA, que llegan sin usuario.</summary>
    Task<Evaluacion?> PorIdInternoAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HayDelCicloAsync(Guid cicloId, CancellationToken cancellationToken);

    /// <summary>La evaluación solo si la cédula y el correo coinciden con los del empleado (portal).</summary>
    Task<Evaluacion?> DeLaPersonaAsync(Guid id, string documento, string correo, CancellationToken cancellationToken);

    void Agregar(Evaluacion evaluacion);
}

public interface IEventosDesempeno
{
    void Publicar(IIntegrationEvent evento);
}

/// <summary>Deja una acción en la bitácora de la empresa (Core), por el outbox del módulo y en la misma transacción.</summary>
public interface IBitacora
{
    void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false);
}

public sealed record FilaCicloDto(
    Guid Id, string Nombre, string Tipo, string Alcance, DateOnly Desde, DateOnly Hasta, string Estado, int Formularios, int FormulariosAprobados,
    int Evaluaciones, int Calificadas);

public sealed record FilaEvaluacionDto(
    Guid Id, Guid EmpleadoId, string Codigo, string Nombre, string Cargo, string Area, string Sede, string Estado, bool Autoevaluada, decimal? Puntaje,
    string? Potencial, int? Caja, bool Calibrada, string? CalificadaPor);

/// <summary>Lo mínimo de cada evaluación calificada para el análisis de sesgos y la matriz 9-box.</summary>
public sealed record CalificadaDto(Guid Id, string Nombre, string Cargo, string Area, string Evaluador, decimal Puntaje, int? Caja, bool Calibrada);

public interface ILecturaDesempeno
{
    Task<Pagina<FilaCicloDto>> CiclosAsync(Guid empresaId, PaginaSolicitada pagina, CancellationToken cancellationToken);

    Task<Pagina<FilaEvaluacionDto>> EvaluacionesAsync(Guid empresaId, Guid cicloId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina, string? buscar,
        string? estado, CancellationToken cancellationToken);

    Task<IReadOnlyList<CalificadaDto>> CalificadasAsync(Guid empresaId, Guid cicloId, IReadOnlyCollection<Guid>? alcance, CancellationToken cancellationToken);

    /// <summary>Conteo por estado de las evaluaciones del ciclo.</summary>
    Task<IReadOnlyDictionary<string, int>> ConteoAsync(Guid empresaId, Guid cicloId, IReadOnlyCollection<Guid>? alcance, CancellationToken cancellationToken);
}
