using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Candidatos;

public sealed record CandidatoResumenDto(
    Guid Id, string Nombre, string Documento, string Ciudad, string UltimoCargo, int MesesExperiencia, IReadOnlyList<string> Habilidades,
    int Postulaciones, DateTimeOffset? UltimaPostulacion, int Alertas, bool TieneLinkedIn);

/// <summary>Resultados del banco de talento y lo que se entendió de la búsqueda, para que la persona vea cómo se buscó.</summary>
public sealed record BancoTalentoDto(Pagina<CandidatoResumenDto> Resultados, IReadOnlyList<string> Entendido);

/// <summary>Una experiencia con su duración en meses (null si las fechas no se entienden) y si sigue en ese trabajo.</summary>
public sealed record ExperienciaDto(string Cargo, string CargoNormalizado, string Empresa, string Desde, string Hasta, int? Meses, bool Actual);

/// <summary>Un conjunto de habilidades: Oficio, Herramientas, Certificaciones, Idiomas o Blandas.</summary>
public sealed record ConjuntoHabilidadesDto(string Grupo, IReadOnlyList<HabilidadDeclarada> Habilidades);

public sealed record EstudioDto(string Titulo, string Institucion, string Anio);

public sealed record PostulacionCandidatoDto(
    Guid PostulacionId, string Numero, Guid VacanteId, string VacanteCodigo, string VacanteTitulo, string EstadoVacante, string Canal,
    DateTimeOffset RecibidaEn, int RespuestasNegativas, Guid ArchivoHojaDeVidaId);

public sealed record PosibleDuplicadoDto(Guid Id, string Nombre, string Documento, string Coincide);

/// <summary>Ficha del candidato: lo que leyó la IA y confirmó el candidato, alertas de la hoja de vida, postulaciones y posibles duplicados.</summary>
public sealed record CandidatoDetalleDto(
    Guid Id, string Nombre, string Documento, string Celular, string Correo, string Ciudad, string LinkedIn,
    IReadOnlyList<ExperienciaDto> Experiencia, IReadOnlyList<EstudioDto> Formacion, IReadOnlyList<string> Cursos, IReadOnlyList<string> Habilidades,
    IReadOnlyList<ConjuntoHabilidadesDto> ConjuntosHabilidades, int MesesExperiencia, IReadOnlyList<AlertaHojaDeVida> Alertas, IReadOnlyList<PostulacionCandidatoDto> Postulaciones,
    IReadOnlyList<PosibleDuplicadoDto> PosiblesDuplicados, bool Suprimido, string? Autorizacion, DateTimeOffset CreadoEn, DateTimeOffset ActualizadoEn);

/// <summary>Fila del banco de talento tal como sale de la base (la revisión de la hoja de vida se hace en la aplicación).</summary>
public sealed record FilaCandidato(Guid Id, string Documento, DatosCandidato Datos, int Postulaciones, DateTimeOffset? UltimaPostulacion);

/// <summary>El candidato con sus postulaciones (solo las de vacantes que el usuario ve) y los que comparten celular o correo.</summary>
public sealed record FichaCandidato(
    Guid Id, string Documento, DatosCandidato Datos, bool Suprimido, DateTimeOffset CreadoEn, DateTimeOffset ActualizadoEn,
    IReadOnlyList<PostulacionCandidatoDto> Postulaciones, string? Autorizacion, IReadOnlyList<PosibleDuplicadoDto> PosiblesDuplicados);

public interface ILecturaCandidatos
{
    Task<Pagina<FilaCandidato>> BuscarAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, InterpretacionBusqueda busqueda, string? textoLibre,
        PaginaSolicitada pagina, CancellationToken cancellationToken);

    Task<FichaCandidato?> FichaAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, Guid candidatoId, CancellationToken cancellationToken);
}

/// <summary>Banco de talento buscable en lenguaje natural («montacarguistas con licencia C1 en Mosquera»).</summary>
public sealed record BuscarCandidatosQuery(string? Buscar, int? Pagina, int? Tamano) : IQuery<BancoTalentoDto>;

public sealed record ObtenerCandidatoQuery(Guid CandidatoId) : IQuery<CandidatoDetalleDto>;

public static class CandidatoConsultaErrors
{
    public static readonly Error NoExiste = Error.NotFound("Candidato.NoExiste", "Ese candidato no existe o no está en su alcance.");
}

internal sealed class BuscarCandidatosHandler(IUsuarioActual usuario, ILecturaCandidatos lectura, TimeProvider reloj) : IQueryHandler<BuscarCandidatosQuery, BancoTalentoDto>
{
    public async Task<Result<BancoTalentoDto>> Handle(BuscarCandidatosQuery q, CancellationToken cancellationToken)
    {
        var busqueda = BusquedaTalento.Interpretar(q.Buscar);
        var pagina = await lectura.BuscarAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, busqueda, q.Buscar, new PaginaSolicitada(q.Pagina, q.Tamano), cancellationToken);
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var items = pagina.Items.Select(f => new CandidatoResumenDto(
            f.Id, f.Datos.Nombre, FormatoCandidato.Enmascarar(f.Documento), f.Datos.Ciudad,
            f.Datos.Experiencia.FirstOrDefault()?.Cargo ?? "", RevisionHojaDeVida.MesesDeExperiencia(f.Datos, hoy),
            [.. Destacadas(f.Datos)], f.Postulaciones, f.UltimaPostulacion,
            RevisionHojaDeVida.Revisar(f.Datos, hoy).Count, !string.IsNullOrWhiteSpace(f.Datos.LinkedIn))).ToList();
        return new BancoTalentoDto(new Pagina<CandidatoResumenDto>(items, pagina.Numero, pagina.Tamano, pagina.Total), busqueda.Entendido);
    }

    /// <summary>En la lista se ven primero las habilidades del oficio, herramientas y certificaciones; las blandas solo si no hay otras.</summary>
    private static IEnumerable<string> Destacadas(DatosCandidato d)
    {
        var tecnicas = ConjuntosHabilidades.Tecnicas(d);
        return (tecnicas.Count > 0 ? tecnicas : Normalizacion.HabilidadesDe(d)).Take(4);
    }
}

internal sealed class ObtenerCandidatoHandler(IUsuarioActual usuario, ILecturaCandidatos lectura, TimeProvider reloj) : IQueryHandler<ObtenerCandidatoQuery, CandidatoDetalleDto>
{
    public async Task<Result<CandidatoDetalleDto>> Handle(ObtenerCandidatoQuery q, CancellationToken cancellationToken)
    {
        var f = await lectura.FichaAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, q.CandidatoId, cancellationToken);
        if (f is null)
        {
            return CandidatoConsultaErrors.NoExiste;
        }

        var d = f.Datos;
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        return new CandidatoDetalleDto(f.Id, d.Nombre, FormatoCandidato.Enmascarar(f.Documento), d.Celular, d.Correo, d.Ciudad, d.LinkedIn,
            [.. d.Experiencia.Select(e =>
            {
                var (meses, actual) = RevisionHojaDeVida.Duracion(e, hoy);
                return new ExperienciaDto(e.Cargo, Normalizacion.Cargo(e.Cargo), e.Empresa, e.Desde, e.Hasta, meses, actual);
            })],
            [.. d.Formacion.Select(e => new EstudioDto(e.Titulo, e.Institucion, e.Anio))], d.Cursos, ConjuntosHabilidades.Tecnicas(d),
            [.. ConjuntosHabilidades.Agrupar(d).Select(g => new ConjuntoHabilidadesDto(g.Grupo.ToString(), g.Habilidades))],
            RevisionHojaDeVida.MesesDeExperiencia(d, hoy), f.Suprimido ? [] : RevisionHojaDeVida.Revisar(d, hoy), f.Postulaciones,
            f.PosiblesDuplicados, f.Suprimido, f.Autorizacion, f.CreadoEn, f.ActualizadoEn);
    }
}

public static class FormatoCandidato
{
    /// <summary>La cédula completa solo se ve al abrir la hoja de vida (con motivo y en la bitácora).</summary>
    public static string Enmascarar(string documento) => documento.Length <= 3 ? "CC ***" : $"CC ***.{documento[^3..]}";
}
