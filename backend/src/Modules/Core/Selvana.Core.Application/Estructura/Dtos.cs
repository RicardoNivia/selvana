using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura;

public sealed record PropuestaDto(Guid Id, string Tipo, string Origen, string Titulo, string Porque, string Evidencia, bool Incluida);

public sealed record ConteosImportacionDto(int Sedes, int Areas, int CentrosCosto, int Cargos, int CargosTrasUnir, int Empleados, IReadOnlyList<string> NombresSedes);

public sealed record ImportacionDto(
    Guid Id, string Fuente, DateTimeOffset LeidaEn, string Estado, ConteosImportacionDto Conteos,
    IReadOnlyList<PropuestaDto> Propuestas, IReadOnlyList<string> Errores, string? MensajeIa, string? ModeloIa,
    DateTimeOffset? AplicadaEn, string? AplicadaPor);

public sealed record MaestroDto(Guid Id, string Nombre, string NombreNomina, int Empleados, bool Activo, bool EditadoManualmente);

public sealed record CentroCostoDto(Guid Id, string Codigo, string Nombre, string Sede, int Empleados, bool Activo, bool EditadoManualmente);

public sealed record RazonDto(string Signo, string Texto, string Fuente);

public sealed record PerfilDto(string Mision, IReadOnlyList<string> Funciones, IReadOnlyList<string> Requisitos, string RiesgoArl);

public sealed record CargoDto(
    Guid Id, string Nombre, string Area, int Empleados, IReadOnlyList<string> CodigosNomina, IReadOnlyList<string> NombresNomina,
    string EstadoPerfil, PerfilDto? Perfil, IReadOnlyList<RazonDto> Razones, string? ModeloIa, string? AprobadoPor, DateTimeOffset? AprobadoEn,
    bool Activo, bool EditadoManualmente, bool RedactandoPerfil);

/// <summary>Lo que la pantalla necesita sin traer las listas: totales, avance de la IA y opciones para los selectores.</summary>
public sealed record ResumenEstructuraDto(
    int Sedes, int Areas, int CentrosCosto, int Cargos, int PerfilesPorRevisar, int PerfilesRedactando, int PerfilesSinRedactar,
    DateTimeOffset? ActualizadaEn, string? Fuente, IReadOnlyList<string> NombresSedes, IReadOnlyList<string> NombresAreas);

/// <summary>Filtro de los listados de estructura.</summary>
public sealed record FiltroListado(PaginaSolicitada Pagina, string? Buscar);

/// <summary>Lado de lectura (CQRS): proyecciones sin tracking y paginadas que implementa Infrastructure.</summary>
public interface ILecturaEstructura
{
    Task<ImportacionDto?> ImportacionActualAsync(Guid empresaId, CancellationToken cancellationToken);

    Task<ResumenEstructuraDto> ResumenAsync(Guid empresaId, CancellationToken cancellationToken);

    Task<Pagina<CargoDto>> CargosAsync(Guid empresaId, FiltroListado filtro, string? estadoPerfil, CancellationToken cancellationToken);

    Task<Pagina<MaestroDto>> SedesAsync(Guid empresaId, FiltroListado filtro, CancellationToken cancellationToken);

    Task<Pagina<MaestroDto>> AreasAsync(Guid empresaId, FiltroListado filtro, CancellationToken cancellationToken);

    Task<Pagina<CentroCostoDto>> CentrosCostoAsync(Guid empresaId, FiltroListado filtro, CancellationToken cancellationToken);
}
