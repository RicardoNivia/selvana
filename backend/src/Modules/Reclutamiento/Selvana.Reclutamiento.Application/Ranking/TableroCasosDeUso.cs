using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Seleccion;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Filtros;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Ranking;

public sealed record TarjetaDto(
    Guid PostulacionId, Guid CandidatoId, string Numero, string Nombre, string Ciudad, int? Puntaje, string EstadoAfinidad, bool Ajustado,
    string? RazonPrincipal, int RespuestasNegativas, DateTimeOffset RecibidaEn, string? MotivoDescarte, Guid? ProcesoVinculacionId = null, string ResultadoFiltros = "SinFiltros", string? MotivoFiltro = null);

public sealed record ColumnaDto(string Etapa, int Total, IReadOnlyList<TarjetaDto> Tarjetas);

public sealed record TableroDto(IReadOnlyList<ColumnaDto> Columnas);

/// <summary>Fila del tablero tal como sale de la base.</summary>
public sealed record FilaTablero(
    Guid PostulacionId, Guid CandidatoId, string Numero, string Nombre, string Ciudad, EtapaPostulacion Etapa, AfinidadPostulacion? Afinidad,
    int RespuestasNegativas, DateTimeOffset RecibidaEn, string? MotivoDescarte, bool Suprimido, Guid? ProcesoVinculacionId = null,
    DatosCandidato? Datos = null, IReadOnlyList<RespuestaFiltro>? Respuestas = null, string? RescatadaPor = null, IReadOnlyList<CambioEtapa>? Historial = null);

public interface ILecturaTablero
{
    Task<IReadOnlyList<FilaTablero>> PostulacionesAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken);
}

/// <summary>Tablero de la vacante: una columna por etapa, ordenada por afinidad, con tope de tarjetas por columna (paginado en el servidor).</summary>
public sealed record ObtenerTableroQuery(Guid VacanteId, int? PorColumna) : IQuery<TableroDto>;

public sealed record MoverPostulacionCommand(Guid PostulacionId, string Etapa, string? Motivo) : ICommand;

/// <summary>El agente arma la terna con los mejores puntajes y la deja en la bandeja: una persona la aprueba.</summary>
public sealed record ProponerTernaCommand(Guid VacanteId) : ICommand<int>;

public sealed record AprobarTernaCommand(Guid VacanteId, IReadOnlyList<Guid> PostulacionIds) : ICommand;

public static class TableroErrors
{
    public static readonly Error EtapaInvalida = Error.Validation("Tablero.Etapa", "Esa etapa no existe.");
    public static readonly Error SinPuntajes = Error.Conflict("Terna.SinPuntajes", "Aún no hay candidatos con puntaje de afinidad: pida a la IA que los califique.");
    public static readonly Error TernaVacia = Error.Validation("Terna.Vacia", "La terna necesita al menos un candidato de esta vacante.");
}

internal static class Tablero
{
    public const string TipoTerna = "Terna";

    public static int? Puntaje(AfinidadPostulacion? a) => a?.Estado == EstadoAfinidad.Lista ? a.Puntaje : null;

    /// <summary>Filtros ATS: el resultado de la postulación y, si no cumple, el motivo (rescatada = una persona decidió seguir).</summary>
    public static (string Resultado, string? Motivo) Filtro(Vacante vacante, FilaTablero f, DateOnly hoy)
    {
        if (f.Datos is null || vacante.Filtros is null)
        {
            return (nameof(ResultadoFiltros.SinFiltros), null);
        }

        var e = EvaluadorFiltros.Evaluar(vacante.Filtros, f.Datos, f.Respuestas ?? [], f.Afinidad, hoy);
        return e.Resultado == ResultadoFiltros.NoCumple && f.RescatadaPor is not null ? ("Rescatada", e.PrimerNoCumple?.Filtro)
            : (e.Resultado.ToString(), e.PrimerNoCumple is { } n ? $"{n.Filtro}: {n.Detalle}" : e.Chequeos.FirstOrDefault(c => c.Estado == EstadoChequeo.SinEvidencia)?.Filtro);
    }
}

internal sealed class MoverPostulacionValidator : AbstractValidator<MoverPostulacionCommand>
{
    public MoverPostulacionValidator() =>
        RuleFor(c => c.Motivo).MaximumLength(500).WithMessage("El motivo puede tener hasta 500 caracteres.");
}

internal sealed class ObtenerTableroHandler(IUsuarioActual usuario, IVacantes vacantes, ILecturaTablero lectura, TimeProvider reloj)
    : IQueryHandler<ObtenerTableroQuery, TableroDto>
{
    public async Task<Result<TableroDto>> Handle(ObtenerTableroQuery q, CancellationToken ct)
    {
        var vacante = await vacantes.ObtenerAsync(q.VacanteId, ct);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        var tope = Math.Clamp(q.PorColumna ?? 20, 1, 100);
        var filas = await lectura.PostulacionesAsync(vacante.EmpresaId, vacante.Id, ct);
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var columnas = Enum.GetValues<EtapaPostulacion>().Select(etapa =>
        {
            var deLaEtapa = filas.Where(f => f.Etapa == etapa).OrderByDescending(f => Tablero.Puntaje(f.Afinidad) ?? -1).ThenByDescending(f => f.RecibidaEn).ToList();
            return new ColumnaDto(etapa.ToString(), deLaEtapa.Count, [.. deLaEtapa.Take(tope).Select(f => new TarjetaDto(
                f.PostulacionId, f.CandidatoId, f.Numero, f.Nombre, f.Ciudad, Tablero.Puntaje(f.Afinidad), f.Afinidad?.Estado.ToString() ?? nameof(EstadoAfinidad.SinCalcular),
                f.Afinidad?.PuntajeAjustado is not null, f.Afinidad?.Razones.FirstOrDefault(r => r.Signo == "+")?.Texto, f.RespuestasNegativas, f.RecibidaEn,
                f.MotivoDescarte, f.ProcesoVinculacionId, Tablero.Filtro(vacante, f, hoy).Resultado, Tablero.Filtro(vacante, f, hoy).Motivo))]);
        }).ToList();
        return new TableroDto(columnas);
    }
}

/// <summary>Mover en el tablero es lo mismo que avanzar en el proceso: la etapa hace su trabajo (invitar al chat, enviar pruebas).</summary>
internal sealed class MoverPostulacionHandler(ICommandHandler<AvanzarPostulacionCommand, AvanceDto> avanzar) : ICommandHandler<MoverPostulacionCommand>
{
    public async Task<Result> Handle(MoverPostulacionCommand c, CancellationToken cancellationToken)
    {
        var r = await avanzar.Handle(new AvanzarPostulacionCommand(c.PostulacionId, c.Etapa, c.Motivo), cancellationToken);
        return r.IsSuccess ? Result.Success() : Result.Failure(r.Error);
    }
}

internal sealed class ProponerTernaHandler(IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<ProponerTernaCommand, int>
{
    public async Task<Result<int>> Handle(ProponerTernaCommand c, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        var disponibles = (await candidatos.ConPostulacionesAAsync(vacante.EmpresaId, vacante.Id, cancellationToken))
            .Where(x => !x.Suprimido)
            .SelectMany(x => x.Postulaciones.Where(p => p.VacanteId == vacante.Id && p.Etapa != EtapaPostulacion.Descartada && Tablero.Puntaje(p.Afinidad) is not null)
                .Select(p => (Candidato: x, Postulacion: p)))
            .OrderByDescending(x => Tablero.Puntaje(x.Postulacion.Afinidad)).ThenBy(x => x.Postulacion.RecibidaEn).ToList();
        if (disponibles.Count == 0)
        {
            return TableroErrors.SinPuntajes;
        }

        var terna = disponibles.Take(3).ToList();
        var ahora = reloj.GetUtcNow();
        var ids = string.Join(',', terna.Select(t => t.Postulacion.Id));
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, vacante.EmpresaId, vacante.EmpresaUsuariaId, "Reclutamiento", Tablero.TipoTerna, vacante.Id,
            $"Terna propuesta · {vacante.Titulo}",
            $"Los {terna.Count} de mayor afinidad entre {disponibles.Count} postulantes no descartados · {vacante.Ciudad} · {vacante.Cupos} {(vacante.Cupos == 1 ? "cupo" : "cupos")}",
            "Agente de ranking", terna[0].Postulacion.Afinidad?.Modelo,
            [.. terna.Select(t => new RazonPropuesta("+",
                $"{t.Candidato.Datos.Nombre} · {Tablero.Puntaje(t.Postulacion.Afinidad)}/100{(t.Postulacion.Afinidad!.PuntajeAjustado is not null ? " (ajustado por una persona)" : "")} · {t.Postulacion.Afinidad.Razones.FirstOrDefault(r => r.Signo == "+")?.Texto ?? "Sin razón a favor"}",
                t.Postulacion.Numero))],
            [new CampoPropuesta("Criterio", "Mayor puntaje de afinidad; los descartados no cuentan"),
             new CampoPropuesta("Siguiente", "Al aprobar, los candidatos pasan a la columna Terna del tablero")],
            new AccionPropuesta("Aprobar terna", "POST", $"/api/v1/reclutamiento/vacantes/{vacante.Id}/terna?postulaciones={ids}"),
            $"/consola/vacantes/{vacante.Id}", true, null));
        bitacora.Registrar(vacante.EmpresaId, TipoActor.Ia, "Agente de ranking", $"Propuso la terna de la vacante {vacante.Codigo}",
            string.Join(" · ", terna.Select(t => $"{t.Postulacion.Numero} ({Tablero.Puntaje(t.Postulacion.Afinidad)})")), $"La pidió {usuario.Nombre}; espera aprobación en la bandeja",
            "Vacante", vacante.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return terna.Count;
    }
}

internal sealed class AprobarTernaHandler(IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<AprobarTernaCommand>
{
    public async Task<Result> Handle(AprobarTernaCommand c, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var movidas = new List<string>();
        foreach (var candidato in await candidatos.ConPostulacionesAAsync(vacante.EmpresaId, vacante.Id, cancellationToken))
        {
            foreach (var p in candidato.Postulaciones.Where(p => p.VacanteId == vacante.Id && c.PostulacionIds.Contains(p.Id)).ToList())
            {
                if (candidato.MoverPostulacion(p.Id, EtapaPostulacion.Terna, null, usuario.Nombre, ahora).IsSuccess)
                {
                    movidas.Add(p.Numero);
                }
            }
        }

        if (movidas.Count == 0)
        {
            return Result.Failure(TableroErrors.TernaVacia);
        }

        bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó la terna de la vacante {vacante.Codigo}", string.Join(" · ", movidas),
            "Propuesta por el agente de ranking", "Vacante", vacante.Id);
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, vacante.EmpresaId, Tablero.TipoTerna, vacante.Id, $"Terna aprobada: {string.Join(", ", movidas)}", usuario.Nombre));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
