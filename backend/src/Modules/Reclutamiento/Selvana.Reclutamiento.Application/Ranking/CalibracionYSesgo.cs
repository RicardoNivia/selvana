using Selvana.Core.Contracts.Auditoria;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Ranking;

/// <summary>Decisiones anteriores de la empresa (retroalimentación y ajustes) para calibrar al agente de ranking.</summary>
public interface ICalibracionRanking
{
    Task<IReadOnlyList<CriterioEmpresa>> CriteriosAsync(Guid empresaId, string cargo, Guid excluirVacanteId, CancellationToken cancellationToken);
}

/// <summary>Una decisión de la empresa sin nada que identifique a la persona: el perfil se resume en cargos y años.</summary>
public static class Calibracion
{
    public const int Maximo = 5;

    public static string Perfil(DatosCandidato d, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(d);
        var cargos = d.Experiencia.Take(3).Select(e => e.Cargo).Where(c => !string.IsNullOrWhiteSpace(c));
        var meses = RevisionHojaDeVida.MesesDeExperiencia(d, hoy);
        var habilidades = ConjuntosHabilidades.Tecnicas(d).Take(4);
        return $"{string.Join(", ", cargos)}; {meses / 12} años con fechas; {string.Join(", ", habilidades)}".Trim(' ', ';', ',');
    }

    public static string Decision(Postulacion p) => p.Retroalimentacion?.Valor switch
    {
        ValorRetroalimentacion.Bueno => "buen candidato",
        ValorRetroalimentacion.NoEncaja => "no encaja",
        _ => p.Afinidad?.PuntajeAjustado is { } a ? $"la persona ajustó el puntaje a {a}" : "sin decisión",
    };
}

/// <summary>«Buen candidato» o «No encaja», con nota opcional.</summary>
public sealed record RetroalimentarPostulacionCommand(Guid PostulacionId, string Valor, string? Nota) : ICommand;

internal sealed class RetroalimentarPostulacionHandler(IUsuarioActual usuario, ICandidatos candidatos, IVacantes vacantes, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<RetroalimentarPostulacionCommand>
{
    private static readonly Error ValorInvalido = Error.Validation("Retroalimentacion.Valor", "Elija «Buen candidato» o «No encaja».");

    public async Task<Result> Handle(RetroalimentarPostulacionCommand c, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ValorRetroalimentacion>(c.Valor, ignoreCase: true, out var valor))
        {
            return Result.Failure(ValorInvalido);
        }

        var candidato = await candidatos.ObtenerPorPostulacionAsync(usuario.EmpresaId, c.PostulacionId, cancellationToken);
        var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == c.PostulacionId);
        var vacante = postulacion is null ? null : await vacantes.ObtenerAsync(postulacion.VacanteId, cancellationToken);
        if (candidato is null || vacante is null || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(AfinidadErrors.NoExiste);
        }

        var r = candidato.Retroalimentar(c.PostulacionId, valor, c.Nota, usuario.Nombre, reloj.GetUtcNow());
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre,
            $"Marcó la postulación {postulacion!.Numero} como «{(valor == ValorRetroalimentacion.Bueno ? "buen candidato" : "no encaja")}»",
            $"{vacante.Codigo} · la IA dio {postulacion.Afinidad?.PuntajeIa?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "sin puntaje"}",
            string.IsNullOrWhiteSpace(c.Nota) ? "Calibra el ranking de la empresa" : $"Nota: {c.Nota.Trim()}", "Postulacion", postulacion.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GrupoSesgoDto(string Grupo, int Calificados, double Promedio);

/// <summary>
/// Reporte de sesgo de la vacante. Selvana no recoge género ni edad, así que no se puede medir el impacto por esos grupos:
/// se muestra lo que sí se controla (qué no ve el agente, razones retiradas, motivos humanos) y la distribución por ciudad y canal.
/// </summary>
public sealed record ReporteSesgoDto(
    int Postulaciones, int Calificadas, double? Promedio, IReadOnlyList<string> NoVeElAgente, int RazonesRetiradas,
    IReadOnlyList<GrupoSesgoDto> PorCiudad, double? BrechaCiudad, IReadOnlyList<GrupoSesgoDto> PorCanal, double? BrechaCanal,
    int Ajustes, double? DiferenciaPromedioAjuste, int Buenos, int NoEncajan, int Descartadas, int EnTerna, double? AceptacionTerna,
    IReadOnlyList<string> Observaciones, IReadOnlyList<GrupoSesgoDto>? DescartesPorFiltro = null);

public sealed record ObtenerReporteSesgoQuery(Guid VacanteId) : IQuery<ReporteSesgoDto>;

internal sealed class ObtenerReporteSesgoHandler(IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos) : IQueryHandler<ObtenerReporteSesgoQuery, ReporteSesgoDto>
{
    /// <summary>Un grupo con menos de 3 calificados no se compara: la brecha no diría nada.</summary>
    private const int MinimoPorGrupo = 3;

    private static readonly string[] NoVe = ["Nombre", "Cédula", "Celular y correo", "LinkedIn", "Edad y fecha de nacimiento", "Género", "Estado civil", "Foto", "Dirección exacta"];

    public async Task<Result<ReporteSesgoDto>> Handle(ObtenerReporteSesgoQuery q, CancellationToken ct)
    {
        var vacante = await vacantes.ObtenerAsync(q.VacanteId, ct);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        var filas = (await candidatos.ConPostulacionesAAsync(vacante.EmpresaId, vacante.Id, ct)).Where(c => !c.Suprimido)
            .SelectMany(c => c.Postulaciones.Where(p => p.VacanteId == vacante.Id).Select(p => (Ciudad: Ciudad(c.Datos.Ciudad), Postulacion: p))).ToList();
        var calificadas = filas.Where(f => f.Postulacion.Afinidad is { Estado: EstadoAfinidad.Lista, PuntajeIa: not null }).ToList();

        // Se compara el puntaje de la IA: es el que puede tener sesgo del modelo
        static double Prom(IEnumerable<int> xs) => Math.Round(xs.Average(), 1);
        List<GrupoSesgoDto> Grupos(Func<(string Ciudad, Postulacion Postulacion), string> clave) =>
            [.. calificadas.GroupBy(clave).Select(g => new GrupoSesgoDto(g.Key, g.Count(), Prom(g.Select(x => x.Postulacion.Afinidad!.PuntajeIa!.Value))))
                .OrderByDescending(g => g.Calificados)];
        static double? Brecha(List<GrupoSesgoDto> g)
        {
            var comparables = g.Where(x => x.Calificados >= MinimoPorGrupo).ToList();
            return comparables.Count < 2 ? null : Math.Round(comparables.Max(x => x.Promedio) - comparables.Min(x => x.Promedio), 1);
        }

        var porCiudad = Grupos(f => f.Ciudad);
        var porCanal = Grupos(f => f.Postulacion.Canal.ToString());
        var ajustes = calificadas.Where(f => f.Postulacion.Afinidad!.PuntajeAjustado is not null).ToList();
        var terna = filas.Count(f => f.Postulacion.Etapa == EtapaPostulacion.Terna);
        var ternaBuenos = filas.Count(f => f.Postulacion.Etapa == EtapaPostulacion.Terna && f.Postulacion.Retroalimentacion?.Valor == ValorRetroalimentacion.Bueno);
        var ternaConOpinion = filas.Count(f => f.Postulacion.Etapa == EtapaPostulacion.Terna && f.Postulacion.Retroalimentacion is not null);

        var observaciones = new List<string>();
        var brechaCiudad = Brecha(porCiudad);
        if (brechaCiudad is > 15)
        {
            observaciones.Add($"Entre ciudades hay {brechaCiudad} puntos de diferencia: revise si la vacante exige vivir cerca o si conviene aceptar traslados.");
        }

        if (ajustes.Count >= 3 && ajustes.Average(f => f.Postulacion.Afinidad!.PuntajeAjustado!.Value - f.Postulacion.Afinidad.PuntajeIa!.Value) is var d && Math.Abs(d) > 10)
        {
            observaciones.Add($"Su equipo ajusta en promedio {Math.Round(d, 1)} puntos: la retroalimentación ya se usa para calibrar al agente en las próximas calificaciones.");
        }

        if (calificadas.Count < MinimoPorGrupo * 2)
        {
            observaciones.Add("Aún hay pocos candidatos calificados para comparar grupos: el reporte se vuelve útil desde unas seis postulaciones con puntaje.");
        }

        return new ReporteSesgoDto(filas.Count, calificadas.Count, calificadas.Count == 0 ? null : Prom(calificadas.Select(f => f.Postulacion.Afinidad!.PuntajeIa!.Value)),
            NoVe, filas.Sum(f => f.Postulacion.Afinidad?.RazonesRetiradas ?? 0),
            porCiudad, brechaCiudad, porCanal, Brecha(porCanal),
            ajustes.Count, ajustes.Count == 0 ? null : Math.Round(ajustes.Average(f => f.Postulacion.Afinidad!.PuntajeAjustado!.Value - f.Postulacion.Afinidad.PuntajeIa!.Value), 1),
            filas.Count(f => f.Postulacion.Retroalimentacion?.Valor == ValorRetroalimentacion.Bueno), filas.Count(f => f.Postulacion.Retroalimentacion?.Valor == ValorRetroalimentacion.NoEncaja),
            filas.Count(f => f.Postulacion.Etapa == EtapaPostulacion.Descartada), terna, ternaConOpinion == 0 ? null : Math.Round(100.0 * ternaBuenos / ternaConOpinion, 0),
            observaciones, DescartesPorFiltro(filas.Select(f => f.Postulacion)));
    }

    /// <summary>Cuántos descartó cada filtro ATS (el motivo empieza con «Filtro ATS · {filtro}:»). Calificados = descartados.</summary>
    private static List<GrupoSesgoDto> DescartesPorFiltro(IEnumerable<Postulacion> postulaciones) =>
        [.. postulaciones.Where(p => p.Etapa == EtapaPostulacion.Descartada && p.MotivoDescarte?.StartsWith("Filtro ATS · ", StringComparison.Ordinal) == true)
            .GroupBy(p => p.MotivoDescarte!["Filtro ATS · ".Length..].Split(':', 2)[0].Trim())
            .Select(g => new GrupoSesgoDto(g.Key, g.Count(), 0)).OrderByDescending(g => g.Calificados)];

    /// <summary>«Funza, Cundinamarca» y «Funza» cuentan como la misma ciudad.</summary>
    private static string Ciudad(string ciudad)
    {
        var c = (ciudad ?? "").Split([',', '('], 2)[0].Trim();
        return c.Length == 0 ? "Sin ciudad" : c;
    }
}
