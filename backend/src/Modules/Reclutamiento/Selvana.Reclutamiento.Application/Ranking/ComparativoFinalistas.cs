using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Candidatos;
using Selvana.Reclutamiento.Application.Preseleccion;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Ranking;

public sealed record HojaDeVidaFinalistaDto(string UltimoCargo, int MesesExperiencia, string Formacion, IReadOnlyList<string> Habilidades, IReadOnlyList<string> Alertas, bool PosibleDuplicado);

public sealed record PreseleccionFinalistaDto(string Estado, bool? Interesado, decimal? AspiracionSalarialCop, bool? AspiracionEnRango, string Disponibilidad, string Ubicacion,
    int Dudas, string Resumen);

public sealed record PruebaFinalistaDto(string Nombre, string Estado, int? Puntaje, int? Corte, bool? PasaCorte, bool Sensible, bool Validada);

public sealed record PruebasFinalistaDto(string Estado, int? Total, bool Provisional, IReadOnlyList<PruebaFinalistaDto> Pruebas);

public sealed record FinalistaDto(
    Guid PostulacionId, Guid CandidatoId, string Nombre, string Ciudad, string Etapa, int? Puntaje, int? PuntajeIa, bool Ajustado,
    IReadOnlyList<RazonDto> AFavor, IReadOnlyList<RazonDto> EnContra, IReadOnlyList<string> Cumplidos, IReadOnlyList<string> Faltantes,
    string? Retroalimentacion, string? NotaRetroalimentacion, HojaDeVidaFinalistaDto HojaDeVida, PreseleccionFinalistaDto? Preseleccion, PruebasFinalistaDto? Pruebas);

public sealed record OpcionFinalistaDto(Guid PostulacionId, string Nombre, int? Puntaje, string Etapa);

/// <summary>Informe comparativo: los finalistas lado a lado (hoja de vida, afinidad, preselección y pruebas) para que el equipo decida.</summary>
public sealed record ComparativoFinalistasDto(
    string Vacante, string Codigo, int Cupos, decimal SalarioMinimo, decimal SalarioMaximo, IReadOnlyList<FinalistaDto> Finalistas, IReadOnlyList<OpcionFinalistaDto> Disponibles);

/// <summary>Sin <paramref name="Postulaciones"/>: la terna, luego quienes están en pruebas y luego los de mayor afinidad, hasta 4.</summary>
public sealed record ObtenerComparativoFinalistasQuery(Guid VacanteId, IReadOnlyList<Guid>? Postulaciones) : IQuery<ComparativoFinalistasDto>;

internal sealed class ObtenerComparativoFinalistasHandler(
    IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, IConversaciones conversaciones, ILecturaPruebas pruebas, TimeProvider reloj)
    : IQueryHandler<ObtenerComparativoFinalistasQuery, ComparativoFinalistasDto>
{
    public const int Maximo = 4;

    private static readonly Dictionary<EtapaPostulacion, int> Prioridad = new()
    {
        [EtapaPostulacion.Terna] = 0, [EtapaPostulacion.Pruebas] = 1, [EtapaPostulacion.Preseleccion] = 2, [EtapaPostulacion.Recibida] = 3, [EtapaPostulacion.Descartada] = 4,
    };

    public async Task<Result<ComparativoFinalistasDto>> Handle(ObtenerComparativoFinalistasQuery q, CancellationToken ct)
    {
        var vacante = await vacantes.ObtenerAsync(q.VacanteId, ct);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var todas = (await candidatos.ConPostulacionesAAsync(vacante.EmpresaId, vacante.Id, ct)).Where(c => !c.Suprimido)
            .SelectMany(c => c.Postulaciones.Where(p => p.VacanteId == vacante.Id).Select(p => (Candidato: c, Postulacion: p))).ToList();

        var elegidas = q.Postulaciones is { Count: > 0 } ids
            ? todas.Where(x => ids.Contains(x.Postulacion.Id)).ToList()
            : [.. todas.Where(x => x.Postulacion.Etapa != EtapaPostulacion.Descartada)
                .OrderBy(x => Prioridad[x.Postulacion.Etapa]).ThenByDescending(x => x.Postulacion.Afinidad?.Puntaje ?? -1)];
        elegidas = [.. elegidas.Take(Maximo)];

        var resultados = (await pruebas.ResultadosAsync(vacante.EmpresaId, vacante.Id, ahora, ct)).ToDictionary(r => r.PostulacionId);
        var duplicados = todas.GroupBy(x => new string([.. x.Candidato.Datos.Celular.Where(char.IsDigit)])).Where(g => g.Key.Length >= 7 && g.Count() > 1)
            .SelectMany(g => g.Select(x => x.Postulacion.Id)).ToHashSet();

        var finalistas = new List<FinalistaDto>();
        foreach (var (c, p) in elegidas)
        {
            var conversacion = await conversaciones.PorPostulacionAsync(vacante.EmpresaId, p.Id, ct);
            var a = p.Afinidad;
            var d = c.Datos;
            var hv = new HojaDeVidaFinalistaDto(d.Experiencia.FirstOrDefault()?.Cargo ?? "", RevisionHojaDeVida.MesesDeExperiencia(d, hoy),
                d.Formacion.FirstOrDefault()?.Titulo ?? "", [.. ConjuntosHabilidades.Tecnicas(d).Take(5)],
                [.. RevisionHojaDeVida.Revisar(d, hoy).Select(x => x.Texto)], duplicados.Contains(p.Id));
            PreseleccionFinalistaDto? presel = null;
            if (conversacion is not null)
            {
                var r = conversacion.Resumen;
                bool? enRango = r.AspiracionSalarialCop is { } asp && vacante.SalarioMaximo > 0 ? asp <= vacante.SalarioMaximo : null;
                presel = new PreseleccionFinalistaDto(conversacion.Estado.ToString(), r.Interesado, r.AspiracionSalarialCop, enRango, r.Disponibilidad, r.Ubicacion, r.Dudas.Count, r.Texto);
            }

            PruebasFinalistaDto? prueba = null;
            if (resultados.TryGetValue(p.Id, out var rp))
            {
                // Los resultados sensibles (personalidad) no se muestran en el comparativo: se ven con su rol en la vacante
                prueba = new PruebasFinalistaDto(rp.Estado, rp.Total, rp.Provisional, [.. rp.Pruebas.Select(x =>
                    new PruebaFinalistaDto(x.Nombre, x.Estado, x.Sensible ? null : x.Puntaje, x.Corte, x.Sensible ? null : x.PasaCorte, x.Sensible, x.Validada))]);
            }

            finalistas.Add(new FinalistaDto(p.Id, c.Id, d.Nombre, d.Ciudad, p.Etapa.ToString(), a?.Estado == EstadoAfinidad.Lista ? a.Puntaje : null, a?.PuntajeIa,
                a?.PuntajeAjustado is not null,
                [.. (a?.Razones ?? []).Where(r => r.Signo == "+").Take(3).Select(r => new RazonDto(r.Signo, r.Texto, r.Evidencia))],
                [.. (a?.Razones ?? []).Where(r => r.Signo != "+").Take(3).Select(r => new RazonDto(r.Signo, r.Texto, r.Evidencia))],
                a?.Cumplidos ?? [], a?.Faltantes ?? [], p.Retroalimentacion?.Valor.ToString(), p.Retroalimentacion?.Nota, hv, presel, prueba));
        }

        return new ComparativoFinalistasDto(vacante.Titulo, vacante.Codigo, vacante.Cupos, vacante.SalarioMinimo, vacante.SalarioMaximo, finalistas,
            [.. todas.OrderBy(x => Prioridad[x.Postulacion.Etapa]).ThenByDescending(x => x.Postulacion.Afinidad?.Puntaje ?? -1)
                .Select(x => new OpcionFinalistaDto(x.Postulacion.Id, x.Candidato.Datos.Nombre, x.Postulacion.Afinidad?.Puntaje, x.Postulacion.Etapa.ToString()))]);
    }
}
