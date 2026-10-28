using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Contracts.Vinculacion;

public sealed record ClausulaRedactada(string Titulo, string Texto);

/// <summary>Una razón de la redacción con su evidencia («+», «Término fijo de 6 meses: periodo de prueba de 36 días», «Condiciones»).</summary>
public sealed record RazonRedaccion(string Signo, string Texto, string Fuente);

/// <summary>Borrador de la carta oferta y del contrato. Si falló, trae <see cref="Error"/> y RRHH puede pedirlo de nuevo.</summary>
public sealed record OfertaRedactada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ProcesoId, string Carta, IReadOnlyList<ClausulaRedactada> Clausulas,
    IReadOnlyList<RazonRedaccion> Razones, IReadOnlyList<string> Alertas, string Modelo, decimal CostoUsd, string? Error) : IIntegrationEvent;

public sealed record CampoDocumento(string Campo, string Valor);

/// <summary>Lo que leyó la IA de un documento de ingreso. Si falló, trae <see cref="Error"/> y RRHH lo revisa a mano.</summary>
public sealed record DocumentoIngresoLeido(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ProcesoId, string Tipo, Guid ArchivoId, string TipoDetectado, bool Legible, bool TipoCorrecto,
    IReadOnlyList<CampoDocumento> Campos, IReadOnlyList<string> Observaciones, IReadOnlyList<RazonRedaccion> Razones, string Modelo, decimal CostoUsd,
    string? Error) : IIntegrationEvent;

/// <summary>Una tarea del plan de inducción: «Momento» es AntesDelIngreso, PrimerDia, PrimeraSemana, PrimerMes o PeriodoPrueba; «Dia», relativo al ingreso.</summary>
public sealed record TareaInduccionPropuesta(string Titulo, string Detalle, string Responsable, string Momento, int Dia);

/// <summary>Plan de inducción que propone la IA para el cargo. Si falló, trae <see cref="Error"/> y RRHH usa el plan base o lo pide de nuevo.</summary>
public sealed record PlanInduccionGenerado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ProcesoId, IReadOnlyList<TareaInduccionPropuesta> Tareas, IReadOnlyList<RazonRedaccion> Razones,
    IReadOnlyList<string> Alertas, string Modelo, decimal CostoUsd, string? Error) : IIntegrationEvent;
