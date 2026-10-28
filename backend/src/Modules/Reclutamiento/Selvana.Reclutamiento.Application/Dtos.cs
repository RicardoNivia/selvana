using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application;

public sealed record RazonDto(string Signo, string Texto, string Fuente);

public sealed record ConteoDto(string Nombre, int Total);

public sealed record VacanteResumenDto(
    Guid Id, string Codigo, string Titulo, string Empresa, string Ciudad, int Cupos, string Estado, int Postulantes,
    DateTimeOffset CreadaEn, DateTimeOffset? PublicadaEn);

public sealed record VacanteDto(
    Guid Id, string Codigo, string Frase, string Estado, string Empresa, Guid? EmpresaUsuariaId,
    string Titulo, string Cargo, string Ciudad, int Cupos, string Jornada, string Descripcion,
    decimal SalarioMinimo, decimal SalarioMaximo, string JustificacionSalario,
    IReadOnlyList<string> Requisitos, IReadOnlyList<string> Competencias, IReadOnlyList<string> PreguntasFiltro, IReadOnlyList<string> Supuestos,
    IReadOnlyList<RazonDto> Razones, string? ModeloIa, string? MensajeIa, bool EditadaPorPersona,
    DateTimeOffset CreadaEn, string CreadaPor, DateTimeOffset? PublicadaEn, int Postulantes, IReadOnlyList<ConteoDto> Canales,
    IReadOnlyList<Pruebas.PruebaEnBateriaDto> Pruebas, AprobacionRequisicionDto Aprobacion, Guid? CargoId = null, CargoCatalogoDto? CargoCatalogo = null,
    string? CodigoMunicipio = null, IReadOnlyList<AclaracionDto>? Aclaraciones = null);

/// <summary>Pregunta de la IA antes de redactar, con opciones para elegir.</summary>
public sealed record AclaracionDto(string Pregunta, IReadOnlyList<string> Opciones);

/// <summary>
/// Cargo del catálogo de la empresa. En una vacante, <paramref name="Vinculado"/> dice si la vacante parte de él;
/// si es false, el cargo existe en el catálogo con el mismo nombre pero la vacante se redactó desde cero.
/// </summary>
public sealed record CargoCatalogoDto(Guid Id, string Nombre, string Area, int Empleados, string EstadoPerfil, bool Vinculado = false);

/// <summary>Decisión de una etapa de la requisición.</summary>
public sealed record AprobacionDto(string Etapa, string Decision, string Por, string Rol, string? Motivo, bool Automatica, DateTimeOffset En);

/// <summary>Cómo va la aprobación: etapas del flujo, la pendiente, quién la pidió, por qué la devolvieron y si el usuario puede decidir.</summary>
public sealed record AprobacionRequisicionDto(
    IReadOnlyList<string> Etapas, string? EtapaPendiente, IReadOnlyList<string> QuienesAprueban, string? PedidaPor, string? MotivoDevolucion,
    IReadOnlyList<AprobacionDto> Decisiones, bool PuedeDecidir);

public sealed record PostulanteDto(
    Guid PostulacionId, Guid CandidatoId, string Numero, string Nombre, string Documento, string Celular, string Ciudad, string Canal,
    string Respuestas, int RespuestasNegativas, string Autorizacion, DateTimeOffset RecibidaEn, int OtrasPostulaciones, IReadOnlyList<string> CamposCorregidos,
    string LinkedIn, AfinidadDto? Afinidad = null, string? Retroalimentacion = null, string? NotaRetroalimentacion = null);

/// <summary>
/// Afinidad con la vacante: el puntaje que manda (el de la persona si lo ajustó), el de la IA, sus razones con evidencia,
/// los requisitos que cumple y los que no demuestra.
/// </summary>
public sealed record AfinidadDto(
    string Estado, int? Puntaje, int? PuntajeIa, IReadOnlyList<RazonDto> Razones, IReadOnlyList<string> Cumplidos, IReadOnlyList<string> Faltantes,
    string? Modelo, string? Mensaje, DateTimeOffset? CalculadaEn, bool Ajustado, string? MotivoAjuste, string? AjustadoPor, int RazonesRetiradas = 0);

public sealed record VacantePublicaDto(
    string Codigo, string Titulo, string Empresa, string Ciudad, string Jornada, int Cupos, decimal SalarioMinimo, decimal SalarioMaximo,
    string Resumen, DateTimeOffset PublicadaEn);

public sealed record PortalDto(Pagina<VacantePublicaDto> Vacantes, IReadOnlyList<ConteoDto> Ciudades, IReadOnlyList<ConteoDto> Jornadas, int Empresas);

public sealed record VacantePublicaDetalleDto(
    string Codigo, string Titulo, string Empresa, string Ciudad, string Jornada, int Cupos, decimal SalarioMinimo, decimal SalarioMaximo,
    string Descripcion, IReadOnlyList<string> Requisitos, IReadOnlyList<string> PreguntasFiltro, DateTimeOffset PublicadaEn, bool RecibePostulaciones);

public sealed record ExperienciaDto(string Cargo, string Empresa, string Desde, string Hasta);

public sealed record FormacionDto(string Titulo, string Institucion, string Anio);

public sealed record DatosCandidatoDto(
    string Nombre, string Documento, string Celular, string Correo, string Ciudad,
    IReadOnlyList<ExperienciaDto> Experiencia, IReadOnlyList<FormacionDto> Formacion, IReadOnlyList<string> Cursos, IReadOnlyList<string> Habilidades);

public sealed record BorradorPostulacionDto(
    Guid Id, string Estado, string? Mensaje, string NombreArchivo, DatosCandidatoDto? Datos,
    string VacanteCodigo, string VacanteTitulo, string Empresa, string Ciudad, IReadOnlyList<string> PreguntasFiltro,
    string VersionAutorizacion, string TextoAutorizacion);

/// <summary>Lado de lectura (CQRS) del módulo: consultas sin tracking y paginadas que implementa Infrastructure.</summary>
public interface ILecturaReclutamiento
{
    Task<Pagina<VacanteResumenDto>> VacantesAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, Guid? empresaUsuariaId, PaginaSolicitada pagina, string? buscar, string? estado, CancellationToken cancellationToken);

    Task<VacanteDto?> VacanteAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken);

    Task<Pagina<PostulanteDto>> PostulantesAsync(Guid empresaId, Guid vacanteId, PaginaSolicitada pagina, string? buscar, string? canal, CancellationToken cancellationToken);

    Task<PortalDto> PortalAsync(PaginaSolicitada pagina, string? buscar, string? ciudad, string? jornada, CancellationToken cancellationToken);

    Task<VacantePublicaDetalleDto?> VacantePublicaAsync(string codigo, CancellationToken cancellationToken);

    Task<BorradorPostulacionDto?> BorradorAsync(Guid borradorId, CancellationToken cancellationToken);

    Task<Pagina<Privacidad.AutorizacionDto>> AutorizacionesAsync(Guid empresaId, PaginaSolicitada pagina, string? buscar, string? estado, CancellationToken cancellationToken);

    /// <summary>Candidatos con autorización vigente y revocadas en el año.</summary>
    Task<(int Vigentes, int RevocadasEnElAnio)> ConteoAutorizacionesAsync(Guid empresaId, int anio, CancellationToken cancellationToken);
}
