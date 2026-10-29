using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Preseleccion;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Application.Ranking;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Seleccion;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Seleccion;

public sealed record CriterioDto(string Texto, bool? Cumple, string Evidencia);

public sealed record SugerenciaDto(string Accion, string? Destino, string Titulo, IReadOnlyList<CriterioDto> Criterios, bool Automatica);

public sealed record CambioEtapaDto(string De, string A, string Por, string? Motivo, DateTimeOffset En);

public sealed record PreseleccionResumenDto(string Estado, bool? Interesado, decimal? Aspiracion, string Disponibilidad, string Texto);

public sealed record PruebasResumenDto(string Estado, int? Promedio, bool? CumpleCortes, int Terminadas, int Total);

public sealed record PersonaProcesoDto(
    Guid PostulacionId, Guid CandidatoId, string Numero, string Nombre, string Ciudad, string Etapa, int? Puntaje, string EstadoAfinidad, bool Ajustado,
    string? RazonPrincipal, string ResultadoFiltros, string? MotivoFiltro, PreseleccionResumenDto? Preseleccion, PruebasResumenDto? Pruebas, int DiasEnEtapa,
    DateTimeOffset RecibidaEn, string? MotivoDescarte, Guid? ProcesoVinculacionId, IReadOnlyList<CambioEtapaDto> Historial, SugerenciaDto Sugerencia);

/// <summary>Una etapa del embudo: cuántos están hoy, cuántos llegaron alguna vez, cuántos esperan una decisión y cuántos esperan al candidato.</summary>
public sealed record EtapaProcesoDto(string Etapa, int Total, int Alcanzaron, int PorDecidir, int Esperando);

public sealed record ReglasAvanceDto(string Modo, int UmbralAfinidad, int ToleranciaSalarial);

public sealed record ProcesoDto(
    IReadOnlyList<EtapaProcesoDto> Etapas, ReglasAvanceDto Reglas, bool TieneBateria, int Contratados, int SugerenciasListas, Pagina<PersonaProcesoDto> Personas);

/// <summary>El proceso de selección de la vacante: embudo, reglas de paso y cada persona con su siguiente paso sugerido (paginado en el servidor).</summary>
public sealed record ObtenerProcesoQuery(Guid VacanteId, string? Etapa, bool SoloPorDecidir, string? Buscar, int Pagina = 1, int Tamano = 10) : IQuery<ProcesoDto>;

/// <summary>Una persona pasa a alguien de etapa; la etapa hace su trabajo (preselección invita al chat, pruebas envía el enlace). Misma etapa = reenviar.</summary>
public sealed record AvanzarPostulacionCommand(Guid PostulacionId, string Destino, string? Motivo) : ICommand<AvanceDto>;

/// <summary>Enlace de un solo uso que generó el paso (para copiarlo mientras llegan WhatsApp y correo).</summary>
public sealed record AvanceDto(string Etapa, string? TipoEnlace, string? Token);

/// <summary>Una persona acepta en bloque los pasos que sugiere la IA. Solo avanzar, invitar y enviar pruebas: descartar va uno por uno, con su motivo.</summary>
public sealed record AplicarSugerenciasCommand(Guid VacanteId, IReadOnlyList<Guid> PostulacionIds) : ICommand<int>;

public sealed record ConfigurarReglasAvanceCommand(Guid VacanteId, string Modo, int UmbralAfinidad, int ToleranciaSalarial) : ICommand;

/// <summary>El agente de selección aplica los pasos de bajo riesgo en las vacantes con modo automático (lo dispara el servicio de fondo).</summary>
public sealed record AvanzarAutomaticoCommand(Guid VacanteId) : ICommand<int>;

public static class SeleccionErrors
{
    public static readonly Error ModoInvalido = Error.Validation("Seleccion.Modo", "El modo es Sugerir o Automatico.");
    public static readonly Error SinBateria = Error.Validation("Seleccion.SinBateria", "Esta vacante no tiene pruebas: elija la batería en Configuración o pase a la persona directo a la terna.");
}

/// <summary>Las señales de cada postulación (filtros, chat, pruebas) y su sugerencia; lo comparten la consulta, el bloque y el modo automático.</summary>
public sealed class LecturaSeleccion(ILecturaTablero lectura, IConversaciones conversaciones, IAplicacionesPruebas aplicaciones, TimeProvider reloj)
{
    internal static readonly string[] Accionables = ["Avanzar", "Descartar", "Invitar", "EnviarPruebas", "Revisar"];

    public async Task<IReadOnlyList<Evaluada>> EvaluarAsync(Vacante v, CancellationToken ct)
    {
        var filas = await lectura.PostulacionesAsync(v.EmpresaId, v.Id, ct);
        var chats = (await conversaciones.DeVacanteAsync(v.EmpresaId, v.Id, ct)).ToDictionary(c => c.PostulacionId);
        var pruebas = (await aplicaciones.DeVacanteAsync(v.EmpresaId, v.Id, ct)).ToDictionary(a => a.PostulacionId);
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var reglas = v.Reglas;
        return [.. filas.Select(f =>
        {
            var (resultado, motivo) = Tablero.Filtro(v, f, hoy);
            chats.TryGetValue(f.PostulacionId, out var chat);
            pruebas.TryGetValue(f.PostulacionId, out var app);
            var conPuntaje = app?.Pruebas.Where(p => p.Puntaje is not null).ToList() ?? [];
            int? promedio = conPuntaje.Count == 0 ? null : (int)Math.Round(conPuntaje.Sum(p => p.Puntaje!.Value * p.Peso) / (double)conPuntaje.Sum(p => p.Peso));
            bool? cortes = app?.Estado == EstadoAplicacion.Terminada ? app.Pruebas.All(p => p.Corte is null || p.Puntaje is null || p.Puntaje >= p.Corte) : null;
            var senales = new SenalesPostulacion(f.Etapa, Tablero.Puntaje(f.Afinidad), resultado, motivo, chat?.Estado.ToString(), chat?.Resumen.Interesado,
                chat?.Resumen.AspiracionSalarialCop, app?.Estado.ToString(), promedio, cortes, v.Pruebas.Count > 0, v.SalarioMaximo);
            var sugerencia = f.Suprimido ? new SugerenciaAvance("Ninguna", null, "El titular pidió suprimir sus datos", [], false)
                : f.ProcesoVinculacionId is not null ? new SugerenciaAvance("Ninguna", null, "En vinculación", [new("Contratación aprobada", true, "Vinculación")], false)
                : EvaluadorAvance.Evaluar(senales, reglas);
            var resumenChat = chat is null ? null
                : new PreseleccionResumenDto(chat.Estado.ToString(), chat.Resumen.Interesado, chat.Resumen.AspiracionSalarialCop, chat.Resumen.Disponibilidad, chat.Resumen.Texto);
            var resumenPruebas = app is null ? null
                : new PruebasResumenDto(app.Estado.ToString(), promedio, cortes, app.Pruebas.Count(p => p.Estado == EstadoPrueba.Terminada), app.Pruebas.Count);
            return new Evaluada(f, resultado, motivo, resumenChat, resumenPruebas, sugerencia);
        })];
    }

    public sealed record Evaluada(FilaTablero Fila, string ResultadoFiltros, string? MotivoFiltro, PreseleccionResumenDto? Chat, PruebasResumenDto? Pruebas, SugerenciaAvance Sugerencia)
    {
        public bool PorDecidir => Accionables.Contains(Sugerencia.Accion);
    }
}

/// <summary>Cambiar de etapa y hacer el trabajo de la etapa: un solo camino para el tablero, el proceso, el bloque y el agente.</summary>
public sealed class TransicionesSeleccion(IConversaciones conversaciones, IAplicacionesPruebas aplicaciones, ITokensPruebas tokens, IBitacora bitacora)
{
    public static readonly IReadOnlyDictionary<EtapaPostulacion, string> Nombres = new Dictionary<EtapaPostulacion, string>
    {
        [EtapaPostulacion.Recibida] = "Recibidas", [EtapaPostulacion.Preseleccion] = "Preselección", [EtapaPostulacion.Pruebas] = "Pruebas",
        [EtapaPostulacion.Terna] = "Terna", [EtapaPostulacion.Descartada] = "Descartadas",
    };

    public async Task<Result<AvanceDto>> AplicarAsync(Vacante v, Candidato c, Postulacion p, EtapaPostulacion destino, string? motivo, string tipoActor, string por,
        DateTimeOffset ahora, CancellationToken ct)
    {
        if (destino == EtapaPostulacion.Pruebas && v.Pruebas.Count == 0)
        {
            return SeleccionErrors.SinBateria;
        }

        var antes = p.Etapa;
        var movida = c.MoverPostulacion(p.Id, destino, motivo, por, ahora);
        if (movida.IsFailure)
        {
            return movida.Error;
        }

        string? tipo = null, token = null;
        if (destino == EtapaPostulacion.Preseleccion)
        {
            var chat = await conversaciones.PorPostulacionAsync(v.EmpresaId, p.Id, ct);
            if (chat is null || antes == destino)
            {
                (token, var huella) = tokens.Generar();
                tipo = "Preseleccion";
                if (chat is null)
                {
                    var saludo = $"Hola{(Preseleccion.Preseleccion.PrimerNombre(c.Datos.Nombre) is { Length: > 0 } n ? $", {n}" : "")}. Soy el asistente de selección de {v.EmpresaVisible}. " +
                        $"Vi tu postulación a «{v.Titulo}» en {v.Ciudad}. ¿Te puedo hacer unas preguntas rápidas? Son unos 3 minutos.";
                    conversaciones.Agregar(ConversacionPreseleccion.Invitar(v.EmpresaId, v.EmpresaUsuariaId, v.Id, p.Id, c.Id, huella, saludo, por, ahora));
                }
                else
                {
                    chat.RenovarEnlace(huella, ahora);
                }
            }
        }
        else if (destino == EtapaPostulacion.Pruebas)
        {
            var app = await aplicaciones.ObtenerPorPostulacionAsync(v.EmpresaId, p.Id, ct);
            if (app is null || antes == destino)
            {
                (token, var huella) = tokens.Generar();
                tipo = "Pruebas";
                if (app is null)
                {
                    aplicaciones.Agregar(AplicacionPruebas.Enviar(v.EmpresaId, v.Id, p.Id, c.Id, c.Datos.Nombre, v.Pruebas, huella, por, ahora));
                }
                else
                {
                    app.Reenviar(huella, por, ahora);
                }
            }
        }

        var trabajo = tipo switch
        {
            "Preseleccion" => antes == destino ? "Renovó el enlace del chat" : "Invitada al chat de preselección",
            "Pruebas" => antes == destino ? "Reenvió el enlace de las pruebas" : "Pruebas enviadas (enlace de 72 horas)",
            _ => null,
        };
        if (antes != destino || trabajo is not null)
        {
            var accion = antes == destino ? $"{trabajo} · postulación {p.Numero}"
                : destino == EtapaPostulacion.Descartada ? $"Descartó la postulación {p.Numero}" : $"Movió la postulación {p.Numero} a {Nombres[destino]}";
            bitacora.Registrar(v.EmpresaId, tipoActor, por, accion, $"{v.Codigo} · {Nombres[antes]} → {Nombres[destino]}",
                destino == EtapaPostulacion.Descartada ? $"Motivo: {p.MotivoDescarte}" : antes != destino ? trabajo ?? motivo : null, "Postulacion", p.Id);
        }

        return new AvanceDto(destino.ToString(), tipo, token);
    }
}

internal sealed class ConfigurarReglasAvanceValidator : AbstractValidator<ConfigurarReglasAvanceCommand>
{
    public ConfigurarReglasAvanceValidator()
    {
        RuleFor(c => c.UmbralAfinidad).InclusiveBetween(0, 100).WithMessage("El umbral de afinidad va de 0 a 100.");
        RuleFor(c => c.ToleranciaSalarial).InclusiveBetween(0, 100).WithMessage("La tolerancia salarial va de 0 a 100 %.");
    }
}

internal sealed class AvanzarPostulacionValidator : AbstractValidator<AvanzarPostulacionCommand>
{
    public AvanzarPostulacionValidator() =>
        RuleFor(c => c.Motivo).MaximumLength(500).WithMessage("El motivo puede tener hasta 500 caracteres.");
}

internal sealed class ObtenerProcesoHandler(IUsuarioActual usuario, IVacantes vacantes, LecturaSeleccion lectura, TimeProvider reloj) : IQueryHandler<ObtenerProcesoQuery, ProcesoDto>
{
    public async Task<Result<ProcesoDto>> Handle(ObtenerProcesoQuery q, CancellationToken ct)
    {
        var v = await vacantes.ObtenerAsync(q.VacanteId, ct);
        if (v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        var todas = await lectura.EvaluarAsync(v, ct);
        var etapas = Enum.GetValues<EtapaPostulacion>().Select(e => new EtapaProcesoDto(e.ToString(),
            todas.Count(x => x.Fila.Etapa == e),
            e == EtapaPostulacion.Descartada ? todas.Count(x => x.Fila.Etapa == e) : todas.Count(x => Alcanzo(x.Fila) >= e),
            todas.Count(x => x.Fila.Etapa == e && x.PorDecidir),
            todas.Count(x => x.Fila.Etapa == e && x.Sugerencia.Accion == "Esperar"))).ToList();

        EtapaPostulacion? etapa = Enum.TryParse<EtapaPostulacion>(q.Etapa, ignoreCase: true, out var e1) ? e1 : null;
        var buscar = q.Buscar?.Trim();
        var filtradas = todas
            .Where(x => etapa is { } et ? x.Fila.Etapa == et : x.Fila.Etapa != EtapaPostulacion.Descartada)
            .Where(x => !q.SoloPorDecidir || x.PorDecidir)
            .Where(x => string.IsNullOrEmpty(buscar) || x.Fila.Nombre.Contains(buscar, StringComparison.OrdinalIgnoreCase) || x.Fila.Numero.Contains(buscar, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.PorDecidir).ThenByDescending(x => Tablero.Puntaje(x.Fila.Afinidad) ?? -1).ThenBy(x => x.Fila.RecibidaEn).ToList();
        var tamano = Math.Clamp(q.Tamano, 1, 100);
        var pagina = Math.Max(1, q.Pagina);
        var hoy = reloj.GetUtcNow();
        var items = filtradas.Skip((pagina - 1) * tamano).Take(tamano).Select(x => Dto(x, hoy)).ToList();
        var r = v.Reglas;
        return new ProcesoDto(etapas, new ReglasAvanceDto(r.Modo.ToString(), r.UmbralAfinidad, r.ToleranciaSalarial), v.Pruebas.Count > 0,
            todas.Count(x => x.Fila.ProcesoVinculacionId is not null), todas.Count(x => x.Sugerencia.Accion is "Avanzar" or "Invitar" or "EnviarPruebas" && x.Sugerencia.Destino != EtapaPostulacion.Descartada),
            new Pagina<PersonaProcesoDto>(items, pagina, tamano, filtradas.Count));
    }

    /// <summary>La etapa más avanzada a la que llegó (aunque después la hayan descartado).</summary>
    private static EtapaPostulacion Alcanzo(FilaTablero f) =>
        (f.Historial ?? []).SelectMany(h => new[] { h.De, h.A }).Append(f.Etapa).Where(e => e != EtapaPostulacion.Descartada).DefaultIfEmpty(EtapaPostulacion.Recibida).Max();

    private static PersonaProcesoDto Dto(LecturaSeleccion.Evaluada x, DateTimeOffset ahora)
    {
        var f = x.Fila;
        var desde = (f.Historial ?? []).LastOrDefault(h => h.A == f.Etapa)?.En ?? f.RecibidaEn;
        var s = x.Sugerencia;
        return new PersonaProcesoDto(f.PostulacionId, f.CandidatoId, f.Numero, f.Nombre, f.Ciudad, f.Etapa.ToString(), Tablero.Puntaje(f.Afinidad),
            f.Afinidad?.Estado.ToString() ?? nameof(EstadoAfinidad.SinCalcular), f.Afinidad?.PuntajeAjustado is not null, f.Afinidad?.Razones.FirstOrDefault(r => r.Signo == "+")?.Texto,
            x.ResultadoFiltros, x.MotivoFiltro, x.Chat, x.Pruebas, Math.Max(0, (int)(ahora - desde).TotalDays), f.RecibidaEn, f.MotivoDescarte, f.ProcesoVinculacionId,
            [.. (f.Historial ?? []).Select(h => new CambioEtapaDto(h.De.ToString(), h.A.ToString(), h.Por, h.Motivo, h.En))],
            new SugerenciaDto(s.Accion, s.Destino?.ToString(), s.Titulo, [.. s.Criterios.Select(c => new CriterioDto(c.Texto, c.Cumple, c.Evidencia))], s.Automatica));
    }
}

internal sealed class AvanzarPostulacionHandler(IUsuarioActual usuario, ICandidatos candidatos, IVacantes vacantes, TransicionesSeleccion transiciones,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<AvanzarPostulacionCommand, AvanceDto>
{
    public async Task<Result<AvanceDto>> Handle(AvanzarPostulacionCommand c, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EtapaPostulacion>(c.Destino, ignoreCase: true, out var destino))
        {
            return TableroErrors.EtapaInvalida;
        }

        var candidato = await candidatos.ObtenerPorPostulacionAsync(usuario.EmpresaId, c.PostulacionId, cancellationToken);
        var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == c.PostulacionId);
        var vacante = postulacion is null ? null : await vacantes.ObtenerAsync(postulacion.VacanteId, cancellationToken);
        if (candidato is null || vacante is null || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return AfinidadErrors.NoExiste;
        }

        var r = await transiciones.AplicarAsync(vacante, candidato, postulacion!, destino, c.Motivo, TipoActor.Persona, usuario.Nombre, reloj.GetUtcNow(), cancellationToken);
        if (r.IsSuccess)
        {
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return r;
    }
}

/// <summary>Aplica los pasos sugeridos a las postulaciones elegidas; lo comparten el bloque (una persona) y el modo automático (el agente).</summary>
internal static class AplicarPasos
{
    public static async Task<int> AplicarAsync(Vacante v, IEnumerable<LecturaSeleccion.Evaluada> elegidas, ICandidatos candidatos, TransicionesSeleccion transiciones,
        string tipoActor, string por, DateTimeOffset ahora, CancellationToken ct)
    {
        var hechas = 0;
        foreach (var x in elegidas)
        {
            var destino = x.Sugerencia.Accion switch
            {
                "Avanzar" when x.Sugerencia.Destino is { } d && d != EtapaPostulacion.Descartada => d,
                "Invitar" => EtapaPostulacion.Preseleccion,
                "EnviarPruebas" => EtapaPostulacion.Pruebas,
                _ => (EtapaPostulacion?)null,
            };
            var candidato = destino is null ? null : await candidatos.ObtenerPorPostulacionAsync(v.EmpresaId, x.Fila.PostulacionId, ct);
            var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == x.Fila.PostulacionId);
            if (candidato is null || postulacion is null)
            {
                continue;
            }

            var motivo = $"Sugerencia: {x.Sugerencia.Titulo}";
            if ((await transiciones.AplicarAsync(v, candidato, postulacion, destino!.Value, motivo, tipoActor, por, ahora, ct)).IsSuccess)
            {
                hechas++;
            }
        }

        return hechas;
    }
}

internal sealed class AplicarSugerenciasHandler(IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, LecturaSeleccion lectura,
    TransicionesSeleccion transiciones, IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<AplicarSugerenciasCommand, int>
{
    public async Task<Result<int>> Handle(AplicarSugerenciasCommand c, CancellationToken cancellationToken)
    {
        var v = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        var ids = c.PostulacionIds.ToHashSet();
        var elegidas = (await lectura.EvaluarAsync(v, cancellationToken)).Where(x => ids.Contains(x.Fila.PostulacionId));
        var hechas = await AplicarPasos.AplicarAsync(v, elegidas, candidatos, transiciones, TipoActor.Persona, usuario.Nombre, reloj.GetUtcNow(), cancellationToken);
        if (hechas > 0)
        {
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return hechas;
    }
}

internal sealed class ConfigurarReglasAvanceHandler(IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IUnidadReclutamiento unidad)
    : ICommandHandler<ConfigurarReglasAvanceCommand>
{
    public async Task<Result> Handle(ConfigurarReglasAvanceCommand c, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ModoAvance>(c.Modo, ignoreCase: true, out var modo))
        {
            return Result.Failure(SeleccionErrors.ModoInvalido);
        }

        var v = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var r = v.ConfigurarReglasAvance(modo, c.UmbralAfinidad, c.ToleranciaSalarial);
        if (r.IsFailure)
        {
            return r;
        }

        bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Ajustó las reglas de avance de la vacante {v.Codigo}",
            $"{(modo == ModoAvance.Automatico ? "Automático" : "Sugerir")} · afinidad {c.UmbralAfinidad} · tolerancia salarial {c.ToleranciaSalarial} %", null, "Vacante", v.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Sin usuario: solo invita a la preselección y envía pruebas (pasos de bajo riesgo). Descartar y la terna siempre los decide una persona.</summary>
internal sealed class AvanzarAutomaticoHandler(IVacantes vacantes, ICandidatos candidatos, LecturaSeleccion lectura, TransicionesSeleccion transiciones,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<AvanzarAutomaticoCommand, int>
{
    private const int TopePorVuelta = 50;

    public async Task<Result<int>> Handle(AvanzarAutomaticoCommand c, CancellationToken cancellationToken)
    {
        var v = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (v is null || v.Estado != EstadoVacante.Publicada || v.Reglas.Modo != ModoAvance.Automatico)
        {
            return 0;
        }

        var listas = (await lectura.EvaluarAsync(v, cancellationToken)).Where(x => x.Sugerencia.Automatica).Take(TopePorVuelta);
        var hechas = await AplicarPasos.AplicarAsync(v, listas, candidatos, transiciones, TipoActor.Ia, "Agente de selección", reloj.GetUtcNow(), cancellationToken);
        if (hechas > 0)
        {
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return hechas;
    }
}
