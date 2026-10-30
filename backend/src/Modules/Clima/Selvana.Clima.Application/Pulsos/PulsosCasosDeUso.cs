using Selvana.Clima.Contracts;
using FluentValidation;
using Selvana.Clima.Application.Abstracciones;
using Selvana.Clima.Domain.Encuestas;
using Selvana.Clima.Domain.Pulsos;
using Selvana.Clima.Domain.Rotacion;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Desempeno.Contracts;
using Selvana.Expediente.Contracts;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Application.Pulsos;

/* ---------- Pulsos de ingreso (C3) ---------- */

public sealed record ListarPulsosQuery(int? Pagina, int? Tamano, string? Filtro) : IQuery<Pagina<FilaPulsoDto>>;

/// <summary>Programa los pulsos de 30, 60 y 90 días de quienes llegaron al hito. Lo corre un proceso en segundo plano y también se puede pedir a mano.</summary>
public sealed record ProgramarPulsosCommand(Guid EmpresaId) : ICommand<int>;

/// <summary>RRHH registra la conversación con la persona después de una alerta.</summary>
public sealed record RegistrarSeguimientoPulsoCommand(Guid Id, string Nota) : ICommand;

/// <summary>Desde el portal: el empleado (cédula y correo) responde su pulso.</summary>
public sealed record ResponderPulsoCommand(Guid Id, string Documento, string Correo, int Satisfaccion, int Claridad, int Apoyo, int Recomendaria, string? Comentario)
    : ICommand;

internal sealed class SeguimientoValidator : AbstractValidator<RegistrarSeguimientoPulsoCommand>
{
    public SeguimientoValidator() =>
        RuleFor(c => c.Nota).NotEmpty().WithMessage("Escriba qué conversó con la persona o qué acordaron.").MaximumLength(1000);
}

internal sealed class ListarPulsosHandler(IUsuarioActual usuario, ILecturaClima lectura) : IQueryHandler<ListarPulsosQuery, Pagina<FilaPulsoDto>>
{
    public async Task<Result<Pagina<FilaPulsoDto>>> Handle(ListarPulsosQuery q, CancellationToken ct) =>
        await lectura.PulsosAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, new PaginaSolicitada(q.Pagina, q.Tamano), q.Filtro, ct);
}

internal sealed class ProgramarPulsosHandler(IPulsos pulsos, IPlantaActiva planta, IUnidadClima unidad, TimeProvider reloj) : ICommandHandler<ProgramarPulsosCommand, int>
{
    public async Task<Result<int>> Handle(ProgramarPulsosCommand c, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var programados = await pulsos.ProgramadosAsync(c.EmpresaId, ct);
        var nuevos = 0;
        foreach (var e in await planta.ListarAsync(c.EmpresaId, ct))
        {
            if (Pulso.HitoVigente(e.FechaIngreso, hoy) is { } hito && !programados.Contains((e.EmpleadoId, hito)))
            {
                pulsos.Agregar(Pulso.Programar(c.EmpresaId, e.EmpleadoId, e.Nombre, e.Documento, e.Correo, e.Cargo, e.Area, e.EmpresaUsuariaId, e.FechaIngreso, hito));
                nuevos++;
            }
        }

        if (nuevos > 0)
        {
            await unidad.SaveChangesAsync(ct);
        }

        return nuevos;
    }
}

internal sealed class RegistrarSeguimientoPulsoHandler(IUsuarioActual usuario, IPulsos pulsos, IEventosClima eventos, IBitacora bitacora, IUnidadClima unidad,
    TimeProvider reloj) : ICommandHandler<RegistrarSeguimientoPulsoCommand>
{
    public async Task<Result> Handle(RegistrarSeguimientoPulsoCommand c, CancellationToken ct)
    {
        var p = await pulsos.PorIdAsync(usuario.EmpresaId, c.Id, ct);
        if (p is null || !usuario.Ve(p.EmpresaUsuariaId))
        {
            return Result.Failure(PulsoErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var hecho = p.RegistrarSeguimiento(c.Nota, usuario.Nombre, ahora);
        if (hecho.IsFailure)
        {
            return hecho;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Pulsos.TipoBandeja, p.Id, "Conversación registrada", usuario.Nombre));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Registró el seguimiento del pulso de {p.Hito} días de {p.Nombre}", p.Cargo, null, "Pulso", p.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal static class Pulsos
{
    public const string TipoBandeja = "AlertaRetiroTemprano";
}

internal sealed class ResponderPulsoHandler(IPulsos pulsos, IEventosClima eventos, IUnidadClima unidad, TimeProvider reloj) : ICommandHandler<ResponderPulsoCommand>
{
    public async Task<Result> Handle(ResponderPulsoCommand c, CancellationToken ct)
    {
        var p = await pulsos.PorIdInternoAsync(c.Id, ct);
        if (p is null || p.Documento != c.Documento || !string.Equals(p.Correo.Trim(), c.Correo.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(PulsoErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var hecho = p.Responder(c.Satisfaccion, c.Claridad, c.Apoyo, c.Recomendaria, c.Comentario, ahora);
        if (hecho.IsFailure)
        {
            return hecho;
        }

        // Alerta temprana: una persona conversa con quien respondió bajo (no es una decisión sobre la persona)
        if (p.Alerta)
        {
            eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Clima", Pulsos.TipoBandeja, p.Id,
                $"Alerta de retiro temprano · {p.Nombre}",
                $"Su pulso de {p.Hito} días salió bajo: {p.Promedio:0.0} de 5 y recomendaría la empresa con {p.Respuesta!.Recomendaria} de 10. Converse con la persona.",
                null, null,
                [new RazonPropuesta("−", $"Promedio del pulso {p.Promedio:0.0} de 5", $"Pulso de {p.Hito} días"),
                 new RazonPropuesta("−", $"Recomendaría la empresa con {p.Respuesta.Recomendaria} de 10", $"Pulso de {p.Hito} días")],
                [new CampoPropuesta("Cargo", p.Cargo), new CampoPropuesta("Área", p.Area), new CampoPropuesta("Comentario", p.Respuesta.Comentario.Length > 0 ? p.Respuesta.Comentario : "—")],
                null, "/consola/clima?pestana=pulsos", false, DateOnly.FromDateTime(ahora.UtcDateTime).AddDays(7)));
        }

        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/* ---------- Riesgo de rotación (C4) ---------- */

public sealed record SenalDto(string Texto, string Evidencia, int Puntos);

public sealed record RiesgoDto(
    Guid EmpleadoId, string Nombre, string Cargo, string Area, string Sede, string Nivel, int Puntos, IReadOnlyList<SenalDto> Senales, IReadOnlyList<string> Acciones);

public sealed record RiesgoRotacionDto(Pagina<RiesgoDto> Personas, int Altos, int Medios, int Bajos);

public sealed record ObtenerRiesgoRotacionQuery(int? Pagina, int? Tamano, string? Nivel) : IQuery<RiesgoRotacionDto>;

/// <summary>El cálculo del riesgo (C4) de la planta de una empresa; lo usan la pantalla de Clima y «Mi equipo» del líder (E4).</summary>
public sealed class CalculoRiesgoRotacion(IPlantaActiva planta, IPulsos pulsos, IEncuestas encuestas, ILecturaClima lectura, IDesempenoReciente desempeno,
    TimeProvider reloj) : IRiesgoDelEquipo
{
    public async Task<IReadOnlyList<(EmpleadoActivo Empleado, RiesgoPersona Riesgo)>> CalcularAsync(Guid empresaId, Func<EmpleadoActivo, bool> incluir,
        CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var empleados = (await planta.ListarAsync(empresaId, ct)).Where(incluir).ToList();
        if (empleados.Count == 0)
        {
            return [];
        }

        var ultimosPulsos = (await pulsos.UltimosRespondidosAsync(empresaId, ct)).ToDictionary(p => p.EmpleadoId);
        var calificaciones = (await desempeno.UltimosAsync(empresaId, ct)).ToDictionary(d => d.EmpleadoId);

        var climaArea = new Dictionary<string, (decimal? Promedio, int? Enps)>(StringComparer.OrdinalIgnoreCase);
        if (await lectura.UltimaConRespuestasAsync(empresaId, ct) is { } encuesta)
        {
            var resultados = CalculoResultados.Calcular(encuesta, await encuestas.RespuestasAsync(encuesta.Id, ct));
            foreach (var g in resultados.Grupos.Where(g => g.Tipo == "Área"))
            {
                climaArea[g.Nombre] = (g.Promedio, g.Enps);
            }
        }

        return [.. empleados.Select(e =>
        {
            var p = ultimosPulsos.GetValueOrDefault(e.EmpleadoId);
            var d = calificaciones.GetValueOrDefault(e.EmpleadoId);
            var clima = climaArea.GetValueOrDefault(e.Area);
            return (e, RiesgoRotacion.Calcular(new DatosRiesgo(e.FechaIngreso, e.FechaFinContrato, p?.Promedio, p?.Respuesta?.Recomendaria, p?.Hito, clima.Promedio,
                clima.Enps, d?.Puntaje, d?.Potencial), hoy));
        })];
    }

    async Task<IReadOnlyList<RiesgoDelEquipo>> IRiesgoDelEquipo.CalcularAsync(Guid empresaId, IReadOnlyCollection<Guid> empleados, CancellationToken cancellationToken) =>
        [.. (await CalcularAsync(empresaId, e => empleados.Contains(e.EmpleadoId), cancellationToken))
            .Select(x => new RiesgoDelEquipo(x.Empleado.EmpleadoId, x.Riesgo.Nivel, x.Riesgo.Puntos, [.. x.Riesgo.Senales.Select(s => (s.Texto, s.Evidencia))], x.Riesgo.Acciones))];
}

/// <summary>
/// Señales explicadas por persona: pulso de ingreso, antigüedad, fin de contrato, clima de su área (solo con 5 o más respuestas) y desempeño.
/// Se calculan en el servidor y se paginan; ordenadas de mayor a menor riesgo.
/// </summary>
internal sealed class ObtenerRiesgoRotacionHandler(IUsuarioActual usuario, CalculoRiesgoRotacion calculo) : IQueryHandler<ObtenerRiesgoRotacionQuery, RiesgoRotacionDto>
{
    public async Task<Result<RiesgoRotacionDto>> Handle(ObtenerRiesgoRotacionQuery q, CancellationToken ct)
    {
        var todos = (await calculo.CalcularAsync(usuario.EmpresaId, e => usuario.Ve(e.EmpresaUsuariaId), ct)).Select(x =>
            new RiesgoDto(x.Empleado.EmpleadoId, x.Empleado.Nombre, x.Empleado.Cargo, x.Empleado.Area, x.Empleado.Sede, x.Riesgo.Nivel, x.Riesgo.Puntos,
                [.. x.Riesgo.Senales.Select(s => new SenalDto(s.Texto, s.Evidencia, s.Puntos))], x.Riesgo.Acciones))
            .OrderByDescending(x => x.Puntos).ThenBy(x => x.Nombre).ToList();

        var filtrados = string.IsNullOrWhiteSpace(q.Nivel) ? todos : [.. todos.Where(x => x.Nivel.Equals(q.Nivel, StringComparison.OrdinalIgnoreCase))];
        var pagina = new PaginaSolicitada(q.Pagina, q.Tamano);
        return new RiesgoRotacionDto(new Pagina<RiesgoDto>([.. filtrados.Skip(pagina.Omitir).Take(pagina.Tamano)], pagina.Numero, pagina.Tamano, filtrados.Count),
            todos.Count(x => x.Nivel == "Alto"), todos.Count(x => x.Nivel == "Medio"), todos.Count(x => x.Nivel == "Bajo"));
    }
}
