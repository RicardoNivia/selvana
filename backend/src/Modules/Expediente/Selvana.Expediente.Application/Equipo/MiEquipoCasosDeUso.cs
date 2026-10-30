using FluentValidation;
using Selvana.Clima.Contracts;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Empresas;
using Selvana.Desempeno.Contracts;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Application.Empleados;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Expediente.Domain.Equipo;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Application.Equipo;

public sealed record EvaluacionMiembroDto(Guid Id, string Ciclo, DateOnly Hasta, string Estado, decimal? Puntaje);

public sealed record SenalMiembroDto(string Texto, string Evidencia);

public sealed record RiesgoMiembroDto(string Nivel, int Puntos, IReadOnlyList<SenalMiembroDto> Senales, IReadOnlyList<string> Acciones);

public sealed record MiembroEquipoDto(
    Guid Id, string Codigo, string Nombre, string Cargo, string Area, string Sede, string? EmpresaUsuaria, DateOnly FechaIngreso, int DiasEnLaEmpresa,
    DateOnly? FechaFinContrato, IReadOnlyList<AlertaDto> Alertas, EvaluacionMiembroDto? Evaluacion, RiesgoMiembroDto? Riesgo);

/// <summary>Lo que el líder debería atender, por reglas: nivel (Urgente, EstaSemana, Acompanar), qué hacer y su evidencia.</summary>
public sealed record PrioridadDto(Guid EmpleadoId, string Nombre, string Nivel, string Texto, string Evidencia);

public sealed record ResumenEquipoDto(int Personas, int ConAlertas, int DocumentosVencidos, int PorEvaluar, int RiesgoAlto, int ContratosPorTerminar, int Nuevos);

public sealed record LiderEquipoDto(Guid Id, string Nombre, int Personas);

public sealed record MiEquipoDto(
    LiderEquipoDto Lider, bool PuedeElegirLider, IReadOnlyList<LiderEquipoDto> Lideres, ResumenEquipoDto Resumen, IReadOnlyList<PrioridadDto> Prioridades,
    string Filtro, IReadOnlyList<string> Entendido, Pagina<MiembroEquipoDto> Miembros);

/// <summary>
/// E4 · «Mi equipo»: las personas a cargo del líder con sus alertas, su evaluación abierta y su riesgo de rotación, lo que hay que atender esta semana y
/// preguntas en lenguaje natural. El líder ve solo su equipo; RRHH puede ver el de cualquier líder.
/// </summary>
public sealed record ObtenerMiEquipoQuery(Guid? LiderId, string? Pregunta, string? Filtro, int? Pagina, int? Tamano) : IQuery<MiEquipoDto>;

public sealed record UsuarioLiderDto(Guid Id, string Nombre, string Rol, string? Cargo, int Personas);

public sealed record AreaEquipoDto(string Nombre, int Personas, int SinLider);

public sealed record OpcionesEquipoDto(IReadOnlyList<UsuarioLiderDto> Usuarios, IReadOnlyList<AreaEquipoDto> Areas);

/// <summary>Usuarios de la consola que pueden ser líderes y las áreas de la planta, para armar los equipos.</summary>
public sealed record ObtenerOpcionesEquipoQuery : IQuery<OpcionesEquipoDto>;

/// <summary>RRHH asigna el líder directo a unas personas o a toda un área (activos). LiderId null = quitar el líder.</summary>
public sealed record AsignarLiderCommand(Guid? LiderId, IReadOnlyList<Guid>? EmpleadoIds, string? Area) : ICommand<int>;

public static class EquipoErrors
{
    public static readonly Error SinUsuario = Error.Validation("Equipo.SinUsuario", "No se reconoce su usuario: vuelva a iniciar sesión.");
    public static readonly Error LiderNoExiste = Error.Validation("Equipo.LiderNoExiste", "Ese líder no es un usuario activo de la consola.");
    public static readonly Error SinPersonas = Error.Validation("Equipo.SinPersonas", "Elija las personas o el área a las que les asigna el líder.");
}

internal sealed class AsignarLiderValidator : AbstractValidator<AsignarLiderCommand>
{
    public AsignarLiderValidator() =>
        RuleFor(c => c).Must(c => (c.EmpleadoIds?.Count ?? 0) > 0 || !string.IsNullOrWhiteSpace(c.Area))
            .WithMessage("Elija las personas o el área a las que les asigna el líder.");
}

internal sealed class ObtenerMiEquipoHandler(IUsuarioActual usuario, ILecturaEmpleados lectura, IEvaluacionesDelEquipo evaluaciones, IRiesgoDelEquipo riesgos,
    TimeProvider reloj) : IQueryHandler<ObtenerMiEquipoQuery, MiEquipoDto>
{
    public async Task<Result<MiEquipoDto>> Handle(ObtenerMiEquipoQuery q, CancellationToken ct)
    {
        if (usuario.UsuarioId is not { } yo)
        {
            return EquipoErrors.SinUsuario;
        }

        // El líder solo ve su equipo; RRHH y los analistas pueden mirar el de otro líder
        var esLider = usuario.Rol == "Lider";
        var lideres = esLider ? [] : await lectura.LideresAsync(usuario.EmpresaId, ct);
        var liderId = esLider ? yo : q.LiderId ?? yo;
        var nombreLider = liderId == yo ? usuario.Nombre : lideres.FirstOrDefault(l => l.Id == liderId)?.Nombre ?? "Líder";

        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var equipo = (await lectura.EquipoAsync(usuario.EmpresaId, liderId, ct)).Where(e => esLider || usuario.Ve(e.EmpresaUsuariaId)).ToList();
        var ids = equipo.Select(e => e.Id).ToList();
        var evals = (await evaluaciones.AbiertasAsync(usuario.EmpresaId, ids, ct)).GroupBy(e => e.EmpleadoId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Hasta).First());
        var riesgo = (await riesgos.CalcularAsync(usuario.EmpresaId, ids, ct)).ToDictionary(r => r.EmpleadoId);

        var miembros = equipo.Select(e => new Miembro(e, e.Alertas(hoy), evals.GetValueOrDefault(e.Id), riesgo.GetValueOrDefault(e.Id), hoy.DayNumber - e.FechaIngreso.DayNumber))
            .OrderBy(m => m.Empleado.Nombre).ToList();

        // Pregunta en lenguaje natural (reglas) o atajo elegido a mano; el atajo manda sobre lo que se entendió
        var interpretacion = PreguntaEquipo.Interpretar(q.Pregunta);
        var filtro = Enum.TryParse<FiltroRapido>(q.Filtro, ignoreCase: true, out var f) && f != FiltroRapido.Todos ? f : interpretacion.Filtro;
        var filtrados = miembros.Where(m => Cumple(m, filtro, hoy) && interpretacion.Terminos.All(t => Menciona(m.Empleado, t))).ToList();

        var pagina = new PaginaSolicitada(q.Pagina, q.Tamano);
        var resumen = new ResumenEquipoDto(miembros.Count, miembros.Count(m => m.Alertas.Count > 0), miembros.Count(m => Cumple(m, FiltroRapido.DocumentosVencidos, hoy)),
            miembros.Count(m => Cumple(m, FiltroRapido.PorEvaluar, hoy)), miembros.Count(m => m.Riesgo?.Nivel == "Alto"),
            miembros.Count(m => Cumple(m, FiltroRapido.ContratoPorTerminar, hoy)), miembros.Count(m => Cumple(m, FiltroRapido.Nuevos, hoy)));
        return new MiEquipoDto(new LiderEquipoDto(liderId, nombreLider, miembros.Count), !esLider,
            [.. lideres.Select(l => new LiderEquipoDto(l.Id, l.Nombre, l.Personas))], resumen, Prioridades(miembros, hoy), filtro.ToString(), interpretacion.Entendido,
            new Pagina<MiembroEquipoDto>([.. filtrados.Skip(pagina.Omitir).Take(pagina.Tamano).Select(Dto)], pagina.Numero, pagina.Tamano, filtrados.Count));
    }

    private sealed record Miembro(Empleado Empleado, IReadOnlyList<AlertaEmpleado> Alertas, EvaluacionDelEquipo? Evaluacion, RiesgoDelEquipo? Riesgo, int Dias);

    private static bool Pendiente(EvaluacionDelEquipo? e) => e is not null && e.Estado != "Calificada";

    private static bool Cumple(Miembro m, FiltroRapido filtro, DateOnly hoy) => filtro switch
    {
        FiltroRapido.ConAlertas => m.Alertas.Count > 0,
        FiltroRapido.DocumentosVencidos => m.Alertas.Any(a => a.Fuente == "Carpeta" && a.Nivel == NivelAlertaEmpleado.Vencido),
        FiltroRapido.DocumentosPorVencer => m.Alertas.Any(a => a.Fuente == "Carpeta" && a.Nivel == NivelAlertaEmpleado.PorVencer),
        FiltroRapido.ContratoPorTerminar => m.Empleado.FechaFinContrato is { } fin && fin <= hoy.AddDays(45),
        FiltroRapido.Afiliaciones => m.Alertas.Any(a => a.Fuente == "Afiliaciones"),
        FiltroRapido.PorEvaluar => Pendiente(m.Evaluacion),
        FiltroRapido.RiesgoRotacion => m.Riesgo?.Nivel is "Alto" or "Medio",
        FiltroRapido.Nuevos => m.Dias <= 90,
        _ => true,
    };

    /// <summary>El término aparece en un documento de la carpeta, el nombre, el cargo, el área o la sede (sin tildes).</summary>
    private static bool Menciona(Empleado e, string termino) =>
        e.Documentos.Select(d => $"{d.Tipo} {d.Nombre}").Append($"{e.Nombre} {e.Cargo} {e.Area} {e.Sede}")
            .Any(x => PreguntaEquipo.Simplificar(x).Contains(termino, StringComparison.Ordinal));

    /// <summary>Lo que el líder debería atender, de lo más urgente a lo que puede esperar (reglas, con su evidencia). Máximo seis.</summary>
    private static List<PrioridadDto> Prioridades(IReadOnlyList<Miembro> miembros, DateOnly hoy)
    {
        var lista = new List<(int Orden, PrioridadDto Prioridad)>();
        foreach (var m in miembros)
        {
            var e = m.Empleado;
            var primer = e.Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? e.Nombre;
            foreach (var a in m.Alertas.Where(a => a.Nivel == NivelAlertaEmpleado.Vencido))
            {
                lista.Add((0, new(e.Id, e.Nombre, "Urgente", a.Fuente == "Carpeta" ? $"Pídale a {primer} el documento al día" : $"Defina la situación del contrato de {primer}",
                    $"{a.Fuente} · {a.Texto}")));
            }

            if (m.Riesgo?.Nivel == "Alto")
            {
                lista.Add((1, new(e.Id, e.Nombre, "EstaSemana", m.Riesgo.Acciones.Count > 0 ? $"{primer}: {m.Riesgo.Acciones[0]}" : $"Converse con {primer} esta semana",
                    m.Riesgo.Senales.Count > 0 ? $"Riesgo alto · {m.Riesgo.Senales[0].Texto} · {m.Riesgo.Senales[0].Evidencia}" : "Riesgo de rotación alto")));
            }

            if (Pendiente(m.Evaluacion) && m.Evaluacion!.Hasta <= hoy.AddDays(15))
            {
                lista.Add((2, new(e.Id, e.Nombre, "EstaSemana", $"Califique a {primer} en «{m.Evaluacion.Ciclo}»",
                    m.Evaluacion.Hasta < hoy ? $"El ciclo cerró el {m.Evaluacion.Hasta:dd/MM/yyyy}" : $"El ciclo cierra el {m.Evaluacion.Hasta:dd/MM/yyyy}")));
            }

            if (e.FechaFinContrato is { } fin && fin >= hoy && fin <= hoy.AddDays(45))
            {
                lista.Add((3, new(e.Id, e.Nombre, "EstaSemana", $"Decida si renueva el contrato de {primer} y avísele con tiempo",
                    $"Termina el {fin:dd/MM/yyyy} · preaviso de 30 días (art. 46 CST)")));
            }

            if (m.Dias <= 30)
            {
                lista.Add((4, new(e.Id, e.Nombre, "Acompanar", $"{primer} lleva {m.Dias} {(m.Dias == 1 ? "día" : "días")}: revise cómo va su inducción",
                    $"Ingresó el {e.FechaIngreso:dd/MM/yyyy}")));
            }
        }

        return [.. lista.OrderBy(x => x.Orden).ThenBy(x => x.Prioridad.Nombre).Take(6).Select(x => x.Prioridad)];
    }

    private static MiembroEquipoDto Dto(Miembro m)
    {
        var e = m.Empleado;
        return new MiembroEquipoDto(e.Id, e.Codigo, e.Nombre, e.Cargo, e.Area, e.Sede, e.EmpresaUsuaria, e.FechaIngreso, m.Dias, e.FechaFinContrato,
            [.. m.Alertas.Select(a => new AlertaDto(a.Nivel.ToString(), a.Texto, a.Fuente))],
            m.Evaluacion is { } ev ? new EvaluacionMiembroDto(ev.EvaluacionId, ev.Ciclo, ev.Hasta, ev.Estado, ev.Puntaje) : null,
            m.Riesgo is { } r ? new RiesgoMiembroDto(r.Nivel, r.Puntos, [.. r.Senales.Select(s => new SenalMiembroDto(s.Texto, s.Evidencia))], r.Acciones) : null);
    }
}

internal sealed class ObtenerOpcionesEquipoHandler(IUsuarioActual usuario, IDirectorioUsuarios directorio, ILecturaEmpleados lectura)
    : IQueryHandler<ObtenerOpcionesEquipoQuery, OpcionesEquipoDto>
{
    public async Task<Result<OpcionesEquipoDto>> Handle(ObtenerOpcionesEquipoQuery q, CancellationToken ct)
    {
        var personas = (await lectura.LideresAsync(usuario.EmpresaId, ct)).ToDictionary(l => l.Id, l => l.Personas);
        var usuarios = (await directorio.ConsolaAsync(usuario.EmpresaId, ct)).Where(u => u.Rol is not ("Empleado" or "Candidato"))
            .Select(u => new UsuarioLiderDto(u.Id, u.Nombre, u.Rol, u.Cargo, personas.GetValueOrDefault(u.Id)))
            .OrderByDescending(u => u.Rol == "Lider").ThenBy(u => u.Nombre).ToList();
        var areas = (await lectura.AreasAsync(usuario.EmpresaId, ct)).Select(a => new AreaEquipoDto(a.Area, a.Personas, a.SinLider)).ToList();
        return new OpcionesEquipoDto(usuarios, areas);
    }
}

internal sealed class AsignarLiderHandler(IUsuarioActual usuario, IEmpleados empleados, IDirectorioUsuarios directorio, IBitacora bitacora, IUnidadExpediente unidad,
    TimeProvider reloj) : ICommandHandler<AsignarLiderCommand, int>
{
    public async Task<Result<int>> Handle(AsignarLiderCommand c, CancellationToken cancellationToken)
    {
        UsuarioConsola? lider = null;
        if (c.LiderId is { } id)
        {
            lider = (await directorio.ConsolaAsync(usuario.EmpresaId, cancellationToken)).FirstOrDefault(u => u.Id == id);
            if (lider is null)
            {
                return EquipoErrors.LiderNoExiste;
            }
        }

        var ids = c.EmpleadoIds?.ToHashSet() ?? [];
        var area = c.Area?.Trim();
        var elegidos = (await empleados.ActivosAsync(usuario.EmpresaId, cancellationToken))
            .Where(e => usuario.Ve(e.EmpresaUsuariaId) && (ids.Contains(e.Id) || (!string.IsNullOrEmpty(area) && e.Area.Equals(area, StringComparison.OrdinalIgnoreCase))))
            .ToList();
        if (elegidos.Count == 0)
        {
            return EquipoErrors.SinPersonas;
        }

        var ahora = reloj.GetUtcNow();
        foreach (var e in elegidos)
        {
            e.AsignarLider(lider?.Id, lider?.Nombre, ahora);
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre,
            lider is null ? $"Quitó el líder a {elegidos.Count} {(elegidos.Count == 1 ? "persona" : "personas")}" : $"Asignó a {lider.Nombre} como líder de {elegidos.Count} {(elegidos.Count == 1 ? "persona" : "personas")}",
            string.IsNullOrEmpty(area) ? string.Join(", ", elegidos.Take(5).Select(e => e.Nombre)) + (elegidos.Count > 5 ? "…" : "") : $"Área {area}", null, "Empleado",
            elegidos.Count == 1 ? elegidos[0].Id : null);
        await unidad.SaveChangesAsync(cancellationToken);
        return elegidos.Count;
    }
}
