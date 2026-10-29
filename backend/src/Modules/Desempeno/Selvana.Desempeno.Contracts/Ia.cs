using Selvana.SharedKernel.Messaging;

namespace Selvana.Desempeno.Contracts.Ia;

/// <summary>D1 · Que la IA proponga el formulario de un cargo. Sin datos de personas.</summary>
public sealed record FormularioCargoSolicitado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid CicloId, string Cargo, string TipoCiclo, int PesoObjetivos) : IIntegrationEvent;

public sealed record ObjetivoParaCopiloto(string Descripcion, string Meta, int Avance, IReadOnlyList<string> Evidencias);

public sealed record CompetenciaParaCopiloto(string Nombre, string Descripcion, int? Autoevaluacion);

/// <summary>D3 · Que el copiloto redacte el borrador de una evaluación. Va sin nombre ni documento: solo el cargo, los objetivos y las evidencias.</summary>
public sealed record BorradorEvaluacionSolicitado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid EvaluacionId, string Cargo, IReadOnlyList<ObjetivoParaCopiloto> Objetivos,
    IReadOnlyList<CompetenciaParaCopiloto> Competencias, string ComentarioAutoevaluacion) : IIntegrationEvent;
