using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Analitica;

/// <summary>Una vacante tal como sale de la base para el tablero (sin tracking).</summary>
public sealed record VacanteAnalitica(
    Guid Id, string Codigo, string Titulo, string Ciudad, EstadoVacante Estado, Guid? EmpresaUsuariaId, int Cupos, DateTimeOffset CreadaEn,
    DateTimeOffset? PublicadaEn, DateTimeOffset? CerradaEn, decimal CostoIaUsd);

/// <summary>Una postulación tal como sale de la base para el tablero: etapa, canal, cuándo llegó, sus cambios de etapa y su costo de IA.</summary>
public sealed record PostulacionAnalitica(
    Guid VacanteId, EtapaPostulacion Etapa, CanalPostulacion Canal, DateTimeOffset RecibidaEn, IReadOnlyList<CambioEtapa> Historial, decimal CostoIaUsd,
    bool Contratada);

public interface ILecturaAnalitica
{
    Task<IReadOnlyList<VacanteAnalitica>> VacantesAsync(Guid empresaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PostulacionAnalitica>> PostulacionesAsync(Guid empresaId, CancellationToken cancellationToken);

    /// <summary>Costo de IA de la preselección por vacante (suma de sus conversaciones).</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> CostoPreseleccionAsync(Guid empresaId, CancellationToken cancellationToken);
}

public sealed record IndicadoresSeleccionDto(
    int VacantesAbiertas, int VacantesEnPreparacion, int VacantesCerradas, int Postulaciones, int EnProceso, int EnTerna, int Contratados,
    decimal? DiasPromedioATerna, decimal CostoIaUsd, decimal? CostoIaPorContratadoUsd);

/// <summary>Cuántos días pasa una persona en promedio en la etapa (solo con las que ya salieron de ella) y cuántas están hoy.</summary>
public sealed record TiempoEtapaDto(string Etapa, decimal? DiasPromedio, int Muestras, int Hoy);

/// <summary>Una fuente (canal): cuántas postulaciones trae y qué tan lejos llegan.</summary>
public sealed record FuenteDto(string Canal, int Postulaciones, int Preseleccionadas, int EnTerna, int Contratados);

public sealed record VacanteTableroDto(
    Guid Id, string Codigo, string Titulo, string Ciudad, string Estado, int DiasAbierta, int Cupos, int Postulaciones, int EnProceso, int EnTerna,
    int Contratados, decimal? DiasPromedioATerna, decimal CostoIaUsd, string? FuentePrincipal);

public sealed record TableroSeleccionDto(
    IndicadoresSeleccionDto Indicadores, IReadOnlyList<TiempoEtapaDto> Tiempos, IReadOnlyList<FuenteDto> Fuentes, Pagina<VacanteTableroDto> Vacantes);

/// <summary>
/// Sprint 10 · Tablero de selección: vacantes abiertas, tiempo por etapa, fuentes y costo de IA por vacante. «Dias» limita las postulaciones al periodo
/// (30, 90, 365; null = todo); «Estado» filtra la tabla de vacantes (Abiertas por defecto, Cerradas o Todas). Paginado en el servidor.
/// </summary>
public sealed record ObtenerTableroSeleccionQuery(int? Dias, string? Estado, int? Pagina, int? Tamano) : IQuery<TableroSeleccionDto>;

internal sealed class ObtenerTableroSeleccionHandler(IUsuarioActual usuario, ILecturaAnalitica lectura, TimeProvider reloj)
    : IQueryHandler<ObtenerTableroSeleccionQuery, TableroSeleccionDto>
{
    private static readonly EtapaPostulacion[] Flujo = [EtapaPostulacion.Recibida, EtapaPostulacion.Preseleccion, EtapaPostulacion.Pruebas, EtapaPostulacion.Terna];

    public async Task<Result<TableroSeleccionDto>> Handle(ObtenerTableroSeleccionQuery q, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var vacantes = (await lectura.VacantesAsync(usuario.EmpresaId, ct)).Where(v => usuario.Ve(v.EmpresaUsuariaId)).ToList();
        var visibles = vacantes.Select(v => v.Id).ToHashSet();
        var desde = q.Dias is > 0 and var d ? ahora.AddDays(-d) : DateTimeOffset.MinValue;
        var todas = (await lectura.PostulacionesAsync(usuario.EmpresaId, ct)).Where(p => visibles.Contains(p.VacanteId)).ToList();
        var periodo = todas.Where(p => p.RecibidaEn >= desde).ToList();
        var preseleccion = await lectura.CostoPreseleccionAsync(usuario.EmpresaId, ct);
        var porVacante = todas.ToLookup(p => p.VacanteId);

        decimal Costo(VacanteAnalitica v) => v.CostoIaUsd + porVacante[v.Id].Sum(p => p.CostoIaUsd) + preseleccion.GetValueOrDefault(v.Id);

        var aTerna = periodo.Select(DiasATerna).OfType<decimal>().ToList();
        var contratados = periodo.Count(p => p.Contratada);
        var costoTotal = vacantes.Where(v => (v.PublicadaEn ?? v.CreadaEn) >= desde || porVacante[v.Id].Any(p => p.RecibidaEn >= desde)).Sum(Costo);
        var indicadores = new IndicadoresSeleccionDto(
            vacantes.Count(v => v.Estado == EstadoVacante.Publicada),
            vacantes.Count(v => v.Estado is EstadoVacante.Redactando or EstadoVacante.Borrador or EstadoVacante.EnAprobacion),
            vacantes.Count(v => v.Estado == EstadoVacante.Cerrada && v.CerradaEn >= desde),
            periodo.Count, periodo.Count(p => p.Etapa is EtapaPostulacion.Preseleccion or EtapaPostulacion.Pruebas),
            periodo.Count(p => p.Etapa == EtapaPostulacion.Terna), contratados, Promedio(aTerna), Math.Round(costoTotal, 4),
            contratados > 0 ? Math.Round(costoTotal / contratados, 4) : null);

        var tiempos = Flujo.Select(e =>
        {
            var duraciones = periodo.SelectMany(p => Intervalos(p).Where(i => i.Etapa == e && i.Hasta is not null)
                .Select(i => (decimal)(i.Hasta!.Value - i.Desde).TotalDays)).ToList();
            return new TiempoEtapaDto(e.ToString(), Promedio(duraciones), duraciones.Count, todas.Count(p => p.Etapa == e));
        }).ToList();

        var fuentes = periodo.GroupBy(p => p.Canal).Select(g => new FuenteDto(g.Key.ToString(), g.Count(),
                g.Count(p => Alcanzo(p) >= EtapaPostulacion.Preseleccion), g.Count(p => Alcanzo(p) >= EtapaPostulacion.Terna), g.Count(p => p.Contratada)))
            .OrderByDescending(f => f.Postulaciones).ToList();

        var estado = (q.Estado ?? "Abiertas").ToUpperInvariant();
        var filas = vacantes
            .Where(v => estado switch
            {
                "CERRADAS" => v.Estado == EstadoVacante.Cerrada,
                "TODAS" => true,
                _ => v.Estado == EstadoVacante.Publicada,
            })
            .OrderByDescending(v => v.PublicadaEn ?? v.CreadaEn)
            .Select(v =>
            {
                var ps = porVacante[v.Id].ToList();
                var inicio = v.PublicadaEn ?? v.CreadaEn;
                var fin = v.CerradaEn ?? ahora;
                return new VacanteTableroDto(v.Id, v.Codigo, v.Titulo, v.Ciudad, v.Estado.ToString(), Math.Max(0, (int)(fin - inicio).TotalDays), v.Cupos, ps.Count,
                    ps.Count(p => p.Etapa is EtapaPostulacion.Recibida or EtapaPostulacion.Preseleccion or EtapaPostulacion.Pruebas),
                    ps.Count(p => p.Etapa == EtapaPostulacion.Terna), ps.Count(p => p.Contratada), Promedio([.. ps.Select(DiasATerna).OfType<decimal>()]),
                    Math.Round(Costo(v), 4), ps.Count == 0 ? null : ps.GroupBy(p => p.Canal).OrderByDescending(g => g.Count()).First().Key.ToString());
            }).ToList();

        var pagina = new PaginaSolicitada(q.Pagina, q.Tamano);
        return new TableroSeleccionDto(indicadores, tiempos, fuentes,
            new Pagina<VacanteTableroDto>([.. filas.Skip(pagina.Omitir).Take(pagina.Tamano)], pagina.Numero, pagina.Tamano, filas.Count));
    }

    private static decimal? Promedio(List<decimal> valores) => valores.Count == 0 ? null : Math.Round(valores.Average(), 1);

    /// <summary>Los tramos que pasó en cada etapa: desde que entró hasta que salió (null si sigue ahí).</summary>
    private static IEnumerable<(EtapaPostulacion Etapa, DateTimeOffset Desde, DateTimeOffset? Hasta)> Intervalos(PostulacionAnalitica p)
    {
        var etapa = EtapaPostulacion.Recibida;
        var desde = p.RecibidaEn;
        foreach (var c in p.Historial.OrderBy(h => h.En))
        {
            yield return (etapa, desde, c.En);
            etapa = c.A;
            desde = c.En;
        }

        yield return (etapa, desde, null);
    }

    private static decimal? DiasATerna(PostulacionAnalitica p) =>
        p.Historial.OrderBy(h => h.En).FirstOrDefault(h => h.A == EtapaPostulacion.Terna) is { } t ? (decimal)(t.En - p.RecibidaEn).TotalDays : null;

    private static EtapaPostulacion Alcanzo(PostulacionAnalitica p) =>
        p.Historial.SelectMany(h => new[] { h.De, h.A }).Append(p.Etapa).Where(e => e != EtapaPostulacion.Descartada).DefaultIfEmpty(EtapaPostulacion.Recibida).Max();
}
