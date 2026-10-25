using Selvana.SharedKernel.Messaging;

namespace Selvana.Vinculacion.Contracts.Ia;

/// <summary>
/// Condiciones de la contratación, sin nombre ni documento del trabajador: la IA escribe «[TRABAJADOR]» y «[DOCUMENTO]»
/// y Selvana los reemplaza al mostrar la oferta.
/// </summary>
public sealed record CondicionesOferta(
    string Empresa, string? EmpresaUsuaria, string Cargo, string Ciudad, string Jornada, string TipoContrato, decimal SalarioCop,
    bool AuxilioTransporte, DateOnly FechaIngreso, DateOnly? FechaFin, int PeriodoPruebaDias, string? Labor, string Descripcion,
    IReadOnlyList<string> Requisitos);

/// <summary>RRHH aprobó la contratación (o cambió las condiciones): el agente redactor prepara la carta oferta y el contrato.</summary>
public sealed record OfertaSolicitada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ProcesoId, CondicionesOferta Condiciones) : IIntegrationEvent;

/// <summary>
/// El candidato (o RRHH por él) subió un documento de ingreso: el lector de IA lo lee. Solo viaja el tipo esperado y el archivo
/// (el worker lo descarga con la clave interna); nunca el examen médico ni los antecedentes.
/// </summary>
public sealed record DocumentoIngresoRecibido(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ProcesoId, string Tipo, string TipoEsperado, Guid ArchivoId, string NombreArchivo,
    string TipoContenido) : IIntegrationEvent;

/// <summary>Una tarea que ya está en el plan (las legales): la IA no la repite.</summary>
public sealed record TareaExistente(string Titulo, string Responsable, int Dia);

/// <summary>
/// Lo que el planificador sabe del ingreso: el cargo y sus condiciones, sin nombre, documento ni datos de la persona.
/// </summary>
public sealed record DatosInduccion(
    string Empresa, string? EmpresaUsuaria, string Cargo, string Area, string Sede, string TipoContrato, string Jornada, DateOnly FechaIngreso,
    int PeriodoPruebaDias, int ClaseRiesgoArl, string Descripcion, IReadOnlyList<string> Requisitos, IReadOnlyList<TareaExistente> Existentes);

/// <summary>Se creó el empleado (o RRHH pidió otra versión): el planificador propone las tareas de inducción propias del cargo.</summary>
public sealed record PlanInduccionSolicitado(Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ProcesoId, DatosInduccion Datos) : IIntegrationEvent;
