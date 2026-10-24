using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Filtros;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Filtros;

/// <summary>Una postulación con lo justo para pasarla por los filtros (sin tracking).</summary>
public sealed record PostulacionFiltrable(
    Guid PostulacionId, Guid CandidatoId, string Numero, string Nombre, EtapaPostulacion Etapa, DatosCandidato Datos, IReadOnlyList<RespuestaFiltro> Respuestas,
    AfinidadPostulacion? Afinidad, string? RescatadaPor, bool Suprimido);

public interface ILecturaFiltros
{
    Task<IReadOnlyList<PostulacionFiltrable>> PostulacionesAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken);
}

public sealed record OpcionFiltroDto(string Texto, bool Excluyente, string? Sugerencia);

/// <summary>Los filtros de la vacante: cada requisito y pregunta con si es excluyente y, si no se han configurado, la sugerencia con su razón.</summary>
public sealed record FiltrosDto(
    bool Configurados, string? ConfiguradoPor, DateTimeOffset? ConfiguradoEn, IReadOnlyList<OpcionFiltroDto> Requisitos, IReadOnlyList<OpcionFiltroDto> Preguntas,
    IReadOnlyList<string> Ciudades, string CiudadVacante, int? ExperienciaMinimaMeses, IReadOnlyDictionary<string, int> PorResultado, int Sugeridos);

public sealed record ChequeoDto(string Tipo, string Filtro, string Estado, string Detalle, string Evidencia);

public sealed record EvaluacionPostulanteDto(
    Guid PostulacionId, Guid CandidatoId, string Numero, string Nombre, string Ciudad, string Etapa, string Resultado, IReadOnlyList<ChequeoDto> Chequeos,
    string? RescatadaPor, bool DescarteSugerido);

public sealed record ObtenerFiltrosQuery(Guid VacanteId) : IQuery<FiltrosDto>;

public sealed record GuardarFiltrosCommand(Guid VacanteId, IReadOnlyList<string> Requisitos, IReadOnlyList<string> Preguntas, IReadOnlyList<string> Ciudades,
    int? ExperienciaMinimaMeses) : ICommand;

public sealed record ListarEvaluacionFiltrosQuery(Guid VacanteId, string? Resultado, int? Pagina, int? Tamano) : IQuery<Pagina<EvaluacionPostulanteDto>>;

/// <summary>Deja en la bandeja los descartes sugeridos (los que no pasan un excluyente). Devuelve cuántos.</summary>
public sealed record ProponerDescartesCommand(Guid VacanteId) : ICommand<int>;

/// <summary>Decisión humana: descarta las postulaciones elegidas (null = todas las sugeridas) con el motivo del filtro. Devuelve cuántas.</summary>
public sealed record AprobarDescartesCommand(Guid VacanteId, IReadOnlyList<Guid>? PostulacionIds) : ICommand<int>;

/// <summary>Una persona decide seguir con un candidato que no pasó un filtro.</summary>
public sealed record RescatarPostulacionCommand(Guid VacanteId, Guid PostulacionId) : ICommand;

public static class FiltrosAts
{
    public const string TipoBandeja = "DescartesFiltros";

    private static readonly EtapaPostulacion[] Descartables = [EtapaPostulacion.Recibida, EtapaPostulacion.Preseleccion, EtapaPostulacion.Pruebas];

    public static EvaluacionFiltros Evaluar(Vacante v, PostulacionFiltrable p, DateOnly hoy) => EvaluadorFiltros.Evaluar(v.Filtros, p.Datos, p.Respuestas, p.Afinidad, hoy);

    /// <summary>Se sugiere descartar a quien no cumple, sigue activo, no está suprimido y nadie rescató.</summary>
    public static bool SeSugiereDescartar(PostulacionFiltrable p, EvaluacionFiltros e) =>
        e.Resultado == ResultadoFiltros.NoCumple && p.RescatadaPor is null && !p.Suprimido && Descartables.Contains(p.Etapa);

    /// <summary>Cómo se muestra: los ya descartados aparte; los que no cumplen y alguien rescató, como rescatados.</summary>
    public static string Clase(PostulacionFiltrable p, EvaluacionFiltros e) =>
        p.Etapa == EtapaPostulacion.Descartada ? "Descartada"
        : e.Resultado == ResultadoFiltros.NoCumple && p.RescatadaPor is not null ? "Rescatada"
        : e.Resultado.ToString();

    public static ChequeoDto Dto(ChequeoFiltro c) => new(c.Tipo, c.Filtro, c.Estado.ToString(), c.Detalle, c.Evidencia);

    public static async Task<Vacante?> VacanteVisibleAsync(IVacantes vacantes, IUsuarioActual usuario, Guid id, CancellationToken ct)
    {
        var v = await vacantes.ObtenerAsync(id, ct);
        return v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId) ? null : v;
    }
}

internal sealed class GuardarFiltrosValidator : AbstractValidator<GuardarFiltrosCommand>
{
    public GuardarFiltrosValidator()
    {
        RuleFor(c => c.Requisitos).NotNull().Must(x => x is null || x.Count <= 40);
        RuleFor(c => c.Preguntas).NotNull().Must(x => x is null || x.Count <= 40);
        RuleFor(c => c.Ciudades).NotNull();
        RuleForEach(c => c.Ciudades).MaximumLength(100);
    }
}

internal sealed class ObtenerFiltrosHandler(IUsuarioActual usuario, IVacantes vacantes, ILecturaFiltros lectura, TimeProvider reloj) : IQueryHandler<ObtenerFiltrosQuery, FiltrosDto>
{
    public async Task<Result<FiltrosDto>> Handle(ObtenerFiltrosQuery q, CancellationToken ct)
    {
        if (await FiltrosAts.VacanteVisibleAsync(vacantes, usuario, q.VacanteId, ct) is not { } v)
        {
            return VacanteErrors.NoExiste;
        }

        var f = v.Filtros;
        var sugeridos = f is null ? EvaluadorFiltros.Sugerir(v) : [];
        string? Sugerencia(string tipo, string texto) => sugeridos.FirstOrDefault(s => s.Tipo == tipo && s.Filtro == texto)?.Razon;
        bool Marcado(IEnumerable<string>? lista, string tipo, string texto) =>
            f is null ? Sugerencia(tipo, texto) is not null : lista?.Contains(texto, StringComparer.OrdinalIgnoreCase) == true;

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var postulaciones = await lectura.PostulacionesAsync(v.EmpresaId, v.Id, ct);
        var evaluaciones = postulaciones.Where(p => !p.Suprimido).Select(p => (p, e: FiltrosAts.Evaluar(v, p, hoy))).ToList();
        var porResultado = evaluaciones.GroupBy(x => FiltrosAts.Clase(x.p, x.e)).ToDictionary(g => g.Key, g => g.Count());

        return new FiltrosDto(f is not null, f?.ConfiguradoPor, f?.ConfiguradoEn,
            [.. v.Requisitos.Select(r => new OpcionFiltroDto(r, Marcado(f?.RequisitosExcluyentes, EvaluadorFiltros.TipoRequisito, r), Sugerencia(EvaluadorFiltros.TipoRequisito, r)))],
            [.. v.PreguntasFiltro.Select(p => new OpcionFiltroDto(p, Marcado(f?.PreguntasExcluyentes, EvaluadorFiltros.TipoPregunta, p), Sugerencia(EvaluadorFiltros.TipoPregunta, p)))],
            f is null ? [.. sugeridos.Where(s => s.Tipo == EvaluadorFiltros.TipoCiudad).Select(s => s.Filtro)] : f.Ciudades, v.Ciudad,
            f is null ? null : f.ExperienciaMinimaMeses, porResultado, evaluaciones.Count(x => FiltrosAts.SeSugiereDescartar(x.p, x.e)));
    }
}

internal sealed class GuardarFiltrosHandler(IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<GuardarFiltrosCommand>
{
    public async Task<Result> Handle(GuardarFiltrosCommand c, CancellationToken cancellationToken)
    {
        if (await FiltrosAts.VacanteVisibleAsync(vacantes, usuario, c.VacanteId, cancellationToken) is not { } v)
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var r = v.ConfigurarFiltros(c.Requisitos, c.Preguntas, c.Ciudades, c.ExperienciaMinimaMeses, usuario.Nombre, reloj.GetUtcNow());
        if (r.IsFailure)
        {
            return r;
        }

        var f = v.Filtros!;
        bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, "Configuró los filtros ATS de la vacante",
            $"{v.Codigo} · {f.RequisitosExcluyentes.Count} requisitos y {f.PreguntasExcluyentes.Count} preguntas excluyentes" +
            (f.Ciudades.Count > 0 ? $" · ciudades: {string.Join(", ", f.Ciudades)}" : "") + (f.ExperienciaMinimaMeses is { } m ? $" · experiencia mínima {m} meses" : ""),
            "Los filtros no descartan solos: proponen y una persona decide", "Vacante", v.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ListarEvaluacionFiltrosHandler(IUsuarioActual usuario, IVacantes vacantes, ILecturaFiltros lectura, TimeProvider reloj)
    : IQueryHandler<ListarEvaluacionFiltrosQuery, Pagina<EvaluacionPostulanteDto>>
{
    private static readonly Dictionary<ResultadoFiltros, int> Orden = new()
    {
        [ResultadoFiltros.NoCumple] = 0, [ResultadoFiltros.PorRevisar] = 1, [ResultadoFiltros.Cumple] = 2, [ResultadoFiltros.SinFiltros] = 3,
    };

    public async Task<Result<Pagina<EvaluacionPostulanteDto>>> Handle(ListarEvaluacionFiltrosQuery q, CancellationToken ct)
    {
        if (await FiltrosAts.VacanteVisibleAsync(vacantes, usuario, q.VacanteId, ct) is not { } v)
        {
            return VacanteErrors.NoExiste;
        }

        var pagina = new PaginaSolicitada(q.Pagina, q.Tamano);
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var filas = (await lectura.PostulacionesAsync(v.EmpresaId, v.Id, ct)).Where(p => !p.Suprimido)
            .Select(p => (p, e: FiltrosAts.Evaluar(v, p, hoy)))
            .Where(x => string.IsNullOrWhiteSpace(q.Resultado) || FiltrosAts.Clase(x.p, x.e) == q.Resultado)
            .OrderBy(x => x.p.Etapa == EtapaPostulacion.Descartada ? 9 : Orden[x.e.Resultado]).ThenBy(x => x.p.Nombre, StringComparer.CurrentCulture).ToList();

        return new Pagina<EvaluacionPostulanteDto>([.. filas.Skip(pagina.Omitir).Take(pagina.Tamano).Select(x => new EvaluacionPostulanteDto(
            x.p.PostulacionId, x.p.CandidatoId, x.p.Numero, x.p.Nombre, x.p.Datos.Ciudad, x.p.Etapa.ToString(), FiltrosAts.Clase(x.p, x.e),
            [.. x.e.Chequeos.Select(FiltrosAts.Dto)], x.p.RescatadaPor, FiltrosAts.SeSugiereDescartar(x.p, x.e)))], pagina.Numero, pagina.Tamano, filas.Count);
    }
}

internal sealed class ProponerDescartesHandler(IUsuarioActual usuario, IVacantes vacantes, ILecturaFiltros lectura, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<ProponerDescartesCommand, int>
{
    public async Task<Result<int>> Handle(ProponerDescartesCommand c, CancellationToken cancellationToken)
    {
        if (await FiltrosAts.VacanteVisibleAsync(vacantes, usuario, c.VacanteId, cancellationToken) is not { } v)
        {
            return VacanteErrors.NoExiste;
        }

        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var sugeridos = (await lectura.PostulacionesAsync(v.EmpresaId, v.Id, cancellationToken))
            .Select(p => (p, e: FiltrosAts.Evaluar(v, p, hoy))).Where(x => FiltrosAts.SeSugiereDescartar(x.p, x.e)).ToList();
        if (sugeridos.Count == 0)
        {
            return 0;
        }

        // Razones: cuántos descarta cada filtro, con la evidencia de dónde sale
        var porFiltro = sugeridos.GroupBy(x => x.e.PrimerNoCumple!.Filtro).OrderByDescending(g => g.Count()).Take(5)
            .Select(g => new RazonPropuesta("−", $"{g.Count()} no {(g.Count() == 1 ? "cumple" : "cumplen")}: {g.Key}", g.First().e.PrimerNoCumple!.Evidencia)).ToList();
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, v.EmpresaId, v.EmpresaUsuariaId, "Reclutamiento", FiltrosAts.TipoBandeja, v.Id,
            $"Descartes sugeridos por filtros · {v.Titulo}",
            $"{sugeridos.Count} {(sugeridos.Count == 1 ? "postulante no pasa" : "postulantes no pasan")} un filtro excluyente. Revise la evidencia, rescate a quien quiera y apruebe los descartes.",
            "Filtros ATS (reglas)", null, porFiltro,
            [new CampoPropuesta("Postulantes", string.Join(", ", sugeridos.Take(5).Select(x => x.p.Nombre)) + (sugeridos.Count > 5 ? $" y {sugeridos.Count - 5} más" : "")),
             new CampoPropuesta("Siguiente", "Al aprobar, pasan a Descartadas con el motivo del filtro")],
            new AccionPropuesta($"Descartar a {sugeridos.Count}", "POST", $"/api/v1/reclutamiento/vacantes/{v.Id}/filtros/descartes/aprobar"),
            $"/consola/vacantes/{v.Id}?pestana=filtros", true, null));
        bitacora.Registrar(v.EmpresaId, TipoActor.Sistema, "Filtros ATS", $"Sugirió descartar a {sugeridos.Count} postulantes", v.Codigo,
            "Espera aprobación de una persona", "Vacante", v.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return sugeridos.Count;
    }
}

internal sealed class AprobarDescartesHandler(IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, ILecturaFiltros lectura, IEventosReclutamiento eventos,
    IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<AprobarDescartesCommand, int>
{
    public async Task<Result<int>> Handle(AprobarDescartesCommand c, CancellationToken cancellationToken)
    {
        if (await FiltrosAts.VacanteVisibleAsync(vacantes, usuario, c.VacanteId, cancellationToken) is not { } v)
        {
            return VacanteErrors.NoExiste;
        }

        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var elegidos = c.PostulacionIds is null ? null : c.PostulacionIds.ToHashSet();
        // Se vuelve a evaluar: solo se descarta a quien sigue sin cumplir hoy
        var sugeridos = (await lectura.PostulacionesAsync(v.EmpresaId, v.Id, cancellationToken))
            .Select(p => (p, e: FiltrosAts.Evaluar(v, p, hoy)))
            .Where(x => FiltrosAts.SeSugiereDescartar(x.p, x.e) && (elegidos is null || elegidos.Contains(x.p.PostulacionId))).ToList();

        var descartadas = 0;
        foreach (var (p, e) in sugeridos)
        {
            var candidato = await candidatos.ObtenerPorPostulacionAsync(v.EmpresaId, p.PostulacionId, cancellationToken);
            if (candidato?.MoverPostulacion(p.PostulacionId, EtapaPostulacion.Descartada, e.MotivoDescarte, usuario.Nombre, ahora).IsSuccess == true)
            {
                descartadas++;
            }
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, v.EmpresaId, FiltrosAts.TipoBandeja, v.Id,
            $"Descartó a {descartadas} por filtros ATS", usuario.Nombre));
        if (descartadas > 0)
        {
            bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó {descartadas} descartes sugeridos por los filtros ATS", v.Codigo,
                "Cada postulación quedó con el motivo del filtro que no pasó", "Vacante", v.Id);
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return descartadas;
    }
}

internal sealed class RescatarPostulacionHandler(IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, IBitacora bitacora, IUnidadReclutamiento unidad,
    TimeProvider reloj) : ICommandHandler<RescatarPostulacionCommand>
{
    public async Task<Result> Handle(RescatarPostulacionCommand c, CancellationToken cancellationToken)
    {
        if (await FiltrosAts.VacanteVisibleAsync(vacantes, usuario, c.VacanteId, cancellationToken) is not { } v)
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var candidato = await candidatos.ObtenerPorPostulacionAsync(v.EmpresaId, c.PostulacionId, cancellationToken);
        var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == c.PostulacionId);
        if (candidato is null || postulacion is null || postulacion.VacanteId != v.Id || !candidato.RescatarDeFiltros(c.PostulacionId, usuario.Nombre, reloj.GetUtcNow()))
        {
            return Result.Failure(AfinidadErrors.NoExiste);
        }

        bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Rescató la postulación {postulacion.Numero} de un descarte sugerido", v.Codigo,
            "Sigue en el proceso aunque no pasó un filtro ATS", "Postulacion", postulacion.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
