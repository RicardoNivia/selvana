using Selvana.SharedKernel.Messaging;

namespace Selvana.Reclutamiento.Contracts;

/// <summary>Perfil del cargo del catálogo del que parte la vacante. Aprobado = RRHH lo aprobó; si no, es un borrador de la IA.</summary>
public sealed record PerfilCargoVacante(
    string Cargo, string Area, bool Aprobado, string Mision, IReadOnlyList<string> Funciones, IReadOnlyList<string> Requisitos, string RiesgoArl);

/// <summary>
/// Una persona pidió una vacante con una frase; el agente redactor debe preparar el borrador.
/// Si eligió un cargo del catálogo, trae su perfil y el borrador parte de él; si no, se redacta desde cero.
/// </summary>
public sealed record VacanteSolicitada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid VacanteId, string Frase, string? EmpresaUsuaria, PerfilCargoVacante? Perfil = null) : IIntegrationEvent;

/// <summary>Un candidato subió su hoja de vida en el portal; el lector debe extraer sus datos. Lleva datos personales.</summary>
public sealed record HojaDeVidaRecibida(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid BorradorId, Guid ArchivoId, string NombreArchivo, string TipoContenido,
    string VacanteTitulo) : IIntegrationEvent;

/// <summary>Lo que la vacante pide, para calificar la afinidad.</summary>
public sealed record RequisitosVacante(
    string Titulo, string Cargo, string Ciudad, string Jornada, string Descripcion, IReadOnlyList<string> Requisitos, IReadOnlyList<string> Competencias);

public sealed record ExperienciaAnonima(string Cargo, string Empresa, string Desde, string Hasta);

/// <summary><paramref name="Valor"/>: la cifra en preguntas de número o de pesos (años de experiencia, aspiración salarial).</summary>
public sealed record RespuestaFiltroAnonima(string Pregunta, bool Respuesta, decimal? Valor = null);

/// <summary>
/// El candidato sin nada que lo identifique ni variables protegidas: sin nombre, documento, contacto, LinkedIn,
/// edad, género, estado civil, foto ni dirección. Solo lo que sirve para el puesto.
/// </summary>
public sealed record PerfilAnonimo(
    string Ciudad, IReadOnlyList<ExperienciaAnonima> Experiencia, IReadOnlyList<string> Formacion, IReadOnlyList<string> Cursos,
    IReadOnlyList<string> Habilidades, IReadOnlyList<RespuestaFiltroAnonima> Respuestas);

/// <summary>
/// Una decisión anterior de la empresa sobre un candidato parecido (sin datos que lo identifiquen): «buen candidato», «no encaja»
/// o un ajuste del puntaje con su motivo. Con estas decisiones el agente se calibra a los criterios de cada empresa.
/// </summary>
public sealed record CriterioEmpresa(string Cargo, string Perfil, int? PuntajeIa, string Decision, string? Motivo);

/// <summary>Una postulación llegó (o se pidió recalcular): el agente de ranking califica su afinidad con la vacante.</summary>
public sealed record AfinidadSolicitada(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid PostulacionId, Guid VacanteId, RequisitosVacante Vacante, PerfilAnonimo Perfil,
    IReadOnlyList<CriterioEmpresa>? Calibracion = null) : IIntegrationEvent;

/// <summary>Lo que el agente de preselección sabe de la vacante para responder dudas sin inventar.</summary>
public sealed record VacantePreseleccion(
    string Titulo, string Empresa, string Ciudad, string Jornada, int Cupos, decimal SalarioMinimo, decimal SalarioMaximo, string Descripcion,
    IReadOnlyList<string> Requisitos, IReadOnlyList<string> PreguntasTecnicas);

/// <summary>Un mensaje de la conversación: Agente, Candidato o Persona (de la empresa).</summary>
public sealed record MensajePrevio(string Autor, string Texto);

/// <summary>El candidato escribió en la conversación de preselección: el agente responde y actualiza el resumen. Lleva datos personales.</summary>
public sealed record MensajePreseleccionRecibido(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ConversacionId, string NombreCandidato, VacantePreseleccion Vacante,
    IReadOnlyList<MensajePrevio> Mensajes) : IIntegrationEvent;
