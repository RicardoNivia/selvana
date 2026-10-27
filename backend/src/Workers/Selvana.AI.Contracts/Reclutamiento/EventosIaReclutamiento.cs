using Selvana.AI.Contracts.Estructura;
using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Contracts.Reclutamiento;

/// <summary>
/// Borrador de vacante del agente redactor. Si falló, trae <see cref="Error"/> y los demás campos vacíos.
/// Si la IA necesita aclarar algo antes de redactar, trae <see cref="Aclaraciones"/> y los demás campos vacíos.
/// </summary>
public sealed record BorradorVacanteRedactado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid VacanteId,
    string Titulo, string Cargo, string Ciudad, int Cupos, string Jornada, string Descripcion,
    IReadOnlyList<string> Requisitos, IReadOnlyList<string> Competencias, IReadOnlyList<string> PreguntasFiltro,
    decimal SalarioMinimo, decimal SalarioMaximo, string JustificacionSalario,
    IReadOnlyList<RazonIa> Razones, IReadOnlyList<string> Supuestos, IReadOnlyList<string> RetiradosPorSesgo,
    string Modelo, string? Error, IReadOnlyList<string>? Aclaraciones = null) : IIntegrationEvent;

public sealed record ExperienciaHv(string Cargo, string Empresa, string Desde, string Hasta);

public sealed record FormacionHv(string Titulo, string Institucion, string Anio);

/// <summary>Lo que la IA leyó de una hoja de vida. Nunca incluye edad, fecha de nacimiento, género, estado civil ni foto.</summary>
public sealed record DatosHojaDeVida(
    string Nombre, string Documento, string Celular, string Correo, string Ciudad,
    IReadOnlyList<ExperienciaHv> Experiencia, IReadOnlyList<FormacionHv> Formacion,
    IReadOnlyList<string> Cursos, IReadOnlyList<string> Habilidades, string? LinkedIn = null);

/// <summary>Respuesta del lector de hojas de vida. Si no pudo leerla, trae <see cref="Error"/> con un mensaje para el candidato.</summary>
public sealed record HojaDeVidaLeida(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid BorradorId, DatosHojaDeVida? Datos, string Modelo, string? Error) : IIntegrationEvent;

/// <summary>Una razón del puntaje con su signo y la evidencia de la hoja de vida («HV · experiencia 1», «Preguntas filtro»).</summary>
public sealed record RazonAfinidad(string Signo, string Texto, string Evidencia);

/// <summary>Respuesta del agente de ranking. Si falló, trae <see cref="Error"/> y puntaje 0.</summary>
public sealed record AfinidadCalculada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid PostulacionId, int Puntaje, IReadOnlyList<RazonAfinidad> Razones,
    IReadOnlyList<string> RequisitosCumplidos, IReadOnlyList<string> RequisitosFaltantes, string Modelo, decimal CostoUsd, string? Error,
    int RazonesRetiradas = 0) : IIntegrationEvent;

public sealed record PreguntaRespondida(string Pregunta, string Respuesta);

/// <summary>Lo que el agente entendió de la conversación. Nunca edad, estado civil, hijos ni otras variables protegidas.</summary>
public sealed record ResumenConversacion(
    bool? Interesado, decimal? AspiracionSalarialCop, string Disponibilidad, string Ubicacion, IReadOnlyList<PreguntaRespondida> Respuestas,
    IReadOnlyList<string> Dudas, string Texto);

/// <summary>Respuesta del agente de preselección. Si falló, trae <see cref="Error"/> y la conversación pasa a una persona.</summary>
public sealed record RespuestaPreseleccion(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ConversacionId, string Mensaje, ResumenConversacion? Resumen, bool Completa,
    string? EscalarPorque, string Modelo, decimal CostoUsd, string? Error) : IIntegrationEvent;
