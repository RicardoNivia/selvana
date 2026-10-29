using Selvana.AI.Contracts.Vinculacion;
using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Contracts.Desempeno;

public sealed record CompetenciaPropuesta(string Nombre, string Descripcion, int Peso);

public sealed record ObjetivoPropuesto(string Descripcion, string Meta, int Peso);

/// <summary>D1 · Formulario de un cargo que propone la IA (competencias con peso y objetivos medibles). Si falló, trae <see cref="Error"/>.</summary>
public sealed record FormularioCargoGenerado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid CicloId, string Cargo, IReadOnlyList<CompetenciaPropuesta> Competencias,
    IReadOnlyList<ObjetivoPropuesto> Objetivos, IReadOnlyList<RazonRedaccion> Razones, string Modelo, decimal CostoUsd, string? Error) : IIntegrationEvent;

/// <summary>D3 · Borrador del copiloto del evaluador: fortalezas, oportunidades y un comentario. Nunca trae calificación.</summary>
public sealed record BorradorEvaluacionGenerado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid EvaluacionId, IReadOnlyList<string> Fortalezas, IReadOnlyList<string> Oportunidades, string Comentario,
    IReadOnlyList<RazonRedaccion> Razones, string Modelo, decimal CostoUsd, string? Error) : IIntegrationEvent;
