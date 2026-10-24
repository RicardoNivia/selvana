using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Contracts.Bandeja;

/// <summary>Una razón de la propuesta con su evidencia («+», «3 años en contact center», «HV p.1»).</summary>
public sealed record RazonPropuesta(string Signo, string Texto, string Fuente);

/// <summary>Un dato que la persona ve para decidir sin abrir otra pantalla («Rango salarial», «$1.750.905 – $1.900.000»).</summary>
public sealed record CampoPropuesta(string Etiqueta, string Valor);

/// <summary>Lo que hace el botón principal: el módulo dueño ejecuta la decisión en su propio endpoint.</summary>
public sealed record AccionPropuesta(string Etiqueta, string Metodo, string Ruta);

/// <summary>
/// Un módulo deja una decisión en la bandeja de aprobaciones. Si ya había una pendiente del mismo tipo y entidad, se reemplaza.
/// </summary>
/// <param name="Tipo">VacanteRedactada, PerfilCargo, UnionesCargos, SolicitudTitular…</param>
/// <param name="Revisar">Ruta de la consola para ajustar antes de decidir.</param>
/// <param name="AfectaPersona">Descartar, contratar, sancionar o calificar: siempre requiere aprobación humana.</param>
/// <param name="ParaRoles">Solo la ven estos roles (por ejemplo, la etapa de gerencia de una requisición); null o vacío = todos los que aprueban.</param>
public sealed record PropuestaPublicada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid? EmpresaUsuariaId, string Modulo, string Tipo, Guid EntidadId,
    string Titulo, string Descripcion, string? Agente, string? Modelo, IReadOnlyList<RazonPropuesta> Razones, IReadOnlyList<CampoPropuesta> Campos,
    AccionPropuesta? Accion, string Revisar, bool AfectaPersona, DateOnly? VenceEl, IReadOnlyList<string>? ParaRoles = null) : IIntegrationEvent;

/// <summary>El módulo dueño ya decidió (publicó, aprobó, respondió…): la propuesta sale de la bandeja.</summary>
public sealed record PropuestaResuelta(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, string Tipo, Guid EntidadId, string Decision, string Por) : IIntegrationEvent;
