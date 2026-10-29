using FluentValidation;
using Selvana.AI.Contracts.Clima;
using Selvana.Clima.Application.Abstracciones;
using Selvana.Clima.Contracts.Ia;
using Selvana.Clima.Domain.Encuestas;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Expediente.Contracts;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Application.Encuestas;

/* ---------- DTO ---------- */

public sealed record PreguntaDto(string Id, string Dimension, string Texto);

public sealed record TemaDto(string Nombre, string Sentimiento, int Cantidad, IReadOnlyList<string> Citas, string Accion);

public sealed record AnalisisDto(string Estado, string Origen, IReadOnlyList<TemaDto> Temas, string Resumen, string? Modelo, string? Error, int Comentarios,
    string? RevisadoPor, DateTimeOffset En);

public sealed record EncuestaDto(
    Guid Id, string Titulo, string Estado, IReadOnlyList<PreguntaDto> Preguntas, bool IncluyeEnps, string PreguntaAbierta, IReadOnlyList<string> Areas,
    IReadOnlyList<string> Sedes, DateTimeOffset? AbiertaEn, DateTimeOffset? CerradaEn, ResultadosEncuesta Resultados, int Comentarios, AnalisisDto? Analisis,
    string CreadaPor, DateTimeOffset CreadaEn);

/// <summary>Lo que ve quien responde la encuesta por el enlace, sin iniciar sesión.</summary>
public sealed record EncuestaPublicaDto(string Empresa, string Titulo, IReadOnlyList<PreguntaDto> Preguntas, bool IncluyeEnps, string PreguntaAbierta,
    IReadOnlyList<string> Areas, IReadOnlyList<string> Sedes);

/* ---------- Consultas y comandos ---------- */

public sealed record ListarEncuestasQuery(int? Pagina, int? Tamano) : IQuery<Pagina<FilaEncuestaDto>>;

public sealed record ObtenerEncuestaQuery(Guid Id) : IQuery<EncuestaDto>;

/// <summary>Crea la encuesta con la plantilla base y las áreas y sedes de la planta activa; RRHH la edita antes de abrirla.</summary>
public sealed record CrearEncuestaCommand(string Titulo) : ICommand<Guid>;

public sealed record EditarEncuestaCommand(Guid Id, string Titulo, IReadOnlyList<PreguntaDto> Preguntas, bool IncluyeEnps, string PreguntaAbierta) : ICommand;

/// <summary>Abre la encuesta (o renueva su enlace) y devuelve el enlace para compartir.</summary>
public sealed record AbrirEncuestaCommand(Guid Id) : ICommand<string>;

public sealed record CerrarEncuestaCommand(Guid Id) : ICommand;

/// <summary>C2 · La IA agrupa los comentarios por temas (solo el texto, sin área ni sede).</summary>
public sealed record AnalizarComentariosIaCommand(Guid Id) : ICommand;

/// <summary>C2 · Camino sin IA: temas por palabras clave.</summary>
public sealed record AnalizarComentariosReglasCommand(Guid Id) : ICommand;

public sealed record MarcarAnalisisRevisadoCommand(Guid Id) : ICommand;

public sealed record ObtenerEncuestaPublicaQuery(string Token) : IQuery<EncuestaPublicaDto>;

public sealed record ResponderEncuestaCommand(string Token, string Area, string Sede, IReadOnlyDictionary<string, int> Puntajes, int? Enps, string? Comentario) : ICommand;

internal static class Encuestas
{
    public static async Task<Result<Encuesta>> DelUsuarioAsync(IEncuestas encuestas, IUsuarioActual usuario, Guid id, CancellationToken ct) =>
        await encuestas.PorIdAsync(usuario.EmpresaId, id, ct) is { } e ? e : EncuestaErrors.NoExiste;

    public static List<string> Comentarios(IReadOnlyList<RespuestaClima> respuestas) =>
        [.. respuestas.Select(r => r.Comentario).Where(c => !string.IsNullOrWhiteSpace(c))];
}

internal sealed class EditarEncuestaValidator : AbstractValidator<EditarEncuestaCommand>
{
    public EditarEncuestaValidator()
    {
        RuleFor(c => c.Titulo).NotEmpty().WithMessage("Póngale título a la encuesta.").MaximumLength(150);
        RuleFor(c => c.PreguntaAbierta).MaximumLength(300);
        RuleForEach(c => c.Preguntas).ChildRules(x =>
        {
            x.RuleFor(y => y.Texto).NotEmpty().WithMessage("Cada pregunta necesita texto.").MaximumLength(300);
            x.RuleFor(y => y.Dimension).NotEmpty().WithMessage("Cada pregunta necesita su dimensión.").MaximumLength(80);
        });
    }
}

internal sealed class CrearEncuestaValidator : AbstractValidator<CrearEncuestaCommand>
{
    public CrearEncuestaValidator() => RuleFor(c => c.Titulo).NotEmpty().WithMessage("Póngale título a la encuesta.").MaximumLength(150);
}

internal sealed class ResponderEncuestaValidator : AbstractValidator<ResponderEncuestaCommand>
{
    public ResponderEncuestaValidator() => RuleFor(c => c.Comentario).MaximumLength(1000).WithMessage("El comentario puede tener hasta 1.000 caracteres.");
}

/* ---------- Consultas ---------- */

internal sealed class ListarEncuestasHandler(IUsuarioActual usuario, ILecturaClima lectura) : IQueryHandler<ListarEncuestasQuery, Pagina<FilaEncuestaDto>>
{
    public async Task<Result<Pagina<FilaEncuestaDto>>> Handle(ListarEncuestasQuery q, CancellationToken ct) =>
        await lectura.EncuestasAsync(usuario.EmpresaId, new PaginaSolicitada(q.Pagina, q.Tamano), ct);
}

internal sealed class ObtenerEncuestaHandler(IUsuarioActual usuario, IEncuestas encuestas) : IQueryHandler<ObtenerEncuestaQuery, EncuestaDto>
{
    public async Task<Result<EncuestaDto>> Handle(ObtenerEncuestaQuery q, CancellationToken ct)
    {
        var r = await Encuestas.DelUsuarioAsync(encuestas, usuario, q.Id, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var e = r.Value;
        var respuestas = await encuestas.RespuestasAsync(e.Id, ct);
        var a = e.Analisis;
        return new EncuestaDto(e.Id, e.Titulo, e.Estado.ToString(), [.. e.Preguntas.Select(p => new PreguntaDto(p.Id, p.Dimension, p.Texto))], e.IncluyeEnps,
            e.PreguntaAbierta, e.Areas, e.Sedes, e.AbiertaEn, e.CerradaEn, CalculoResultados.Calcular(e, respuestas), Encuestas.Comentarios(respuestas).Count,
            a is null ? null : new AnalisisDto(a.Estado, a.Origen, [.. a.Temas.Select(t => new TemaDto(t.Nombre, t.Sentimiento, t.Cantidad, t.Citas, t.Accion))],
                a.Resumen, a.Modelo, a.Error, a.Comentarios, a.RevisadoPor, a.En),
            e.CreadaPor, e.CreadaEn);
    }
}

internal sealed class ObtenerEncuestaPublicaHandler(IEncuestas encuestas, ITokensEncuesta tokens, Core.Contracts.Empresas.IDirectorioEmpresas directorio)
    : IQueryHandler<ObtenerEncuestaPublicaQuery, EncuestaPublicaDto>
{
    public async Task<Result<EncuestaPublicaDto>> Handle(ObtenerEncuestaPublicaQuery q, CancellationToken ct)
    {
        var e = await encuestas.PorTokenAsync(tokens.Huella(q.Token), ct);
        if (e is null)
        {
            return EncuestaErrors.EnlaceNoExiste;
        }

        var empresa = (await directorio.ObtenerAsync(e.EmpresaId, ct))?.Nombre ?? "";
        return new EncuestaPublicaDto(empresa, e.Titulo, [.. e.Preguntas.Select(p => new PreguntaDto(p.Id, p.Dimension, p.Texto))], e.IncluyeEnps, e.PreguntaAbierta,
            e.Areas, e.Sedes);
    }
}

/* ---------- Comandos ---------- */

internal sealed class CrearEncuestaHandler(IUsuarioActual usuario, IEncuestas encuestas, IPlantaActiva planta, IBitacora bitacora, IUnidadClima unidad, TimeProvider reloj)
    : ICommandHandler<CrearEncuestaCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearEncuestaCommand c, CancellationToken ct)
    {
        var empleados = await planta.ListarAsync(usuario.EmpresaId, ct);
        var e = Encuesta.Crear(usuario.EmpresaId, c.Titulo, PlantillaClima.Preguntas(), true, PlantillaClima.PreguntaAbierta, empleados.Select(x => x.Area),
            empleados.Select(x => x.Sede), usuario.Nombre, reloj.GetUtcNow());
        encuestas.Agregar(e);
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Creó la encuesta de clima «{e.Titulo}»", $"{e.Preguntas.Count} preguntas",
            "Plantilla base", "Encuesta", e.Id);
        await unidad.SaveChangesAsync(ct);
        return e.Id;
    }
}

internal sealed class EditarEncuestaHandler(IUsuarioActual usuario, IEncuestas encuestas, IUnidadClima unidad) : ICommandHandler<EditarEncuestaCommand>
{
    public async Task<Result> Handle(EditarEncuestaCommand c, CancellationToken ct)
    {
        var r = await Encuestas.DelUsuarioAsync(encuestas, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var preguntas = c.Preguntas.Select((p, i) => new PreguntaClima
        {
            Id = string.IsNullOrWhiteSpace(p.Id) ? $"p{i + 1}" : p.Id.Trim(), Dimension = p.Dimension.Trim(), Texto = p.Texto.Trim(),
        }).ToList();
        if (preguntas.Select(p => p.Id).Distinct().Count() != preguntas.Count)
        {
            preguntas = [.. preguntas.Select((p, i) => new PreguntaClima { Id = $"p{i + 1}", Dimension = p.Dimension, Texto = p.Texto })];
        }

        var hecho = r.Value.Editar(c.Titulo, preguntas, c.IncluyeEnps, c.PreguntaAbierta);
        if (hecho.IsFailure)
        {
            return hecho;
        }

        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class AbrirEncuestaHandler(IUsuarioActual usuario, IEncuestas encuestas, ITokensEncuesta tokens, IBitacora bitacora, IUnidadClima unidad,
    TimeProvider reloj) : ICommandHandler<AbrirEncuestaCommand, string>
{
    public async Task<Result<string>> Handle(AbrirEncuestaCommand c, CancellationToken ct)
    {
        var r = await Encuestas.DelUsuarioAsync(encuestas, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var (token, huella) = tokens.Generar();
        var hecho = r.Value.Abrir(huella, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho.Error;
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Abrió la encuesta de clima «{r.Value.Titulo}»", "Enlace anónimo nuevo", null,
            "Encuesta", r.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return $"/clima/{token}";
    }
}

internal sealed class CerrarEncuestaHandler(IUsuarioActual usuario, IEncuestas encuestas, IBitacora bitacora, IUnidadClima unidad, TimeProvider reloj)
    : ICommandHandler<CerrarEncuestaCommand>
{
    public async Task<Result> Handle(CerrarEncuestaCommand c, CancellationToken ct)
    {
        var r = await Encuestas.DelUsuarioAsync(encuestas, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var hecho = r.Value.Cerrar(reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Cerró la encuesta de clima «{r.Value.Titulo}»", r.Value.Titulo, null, "Encuesta",
            r.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class AnalizarComentariosIaHandler(IUsuarioActual usuario, IEncuestas encuestas, IEventosClima eventos, IUnidadClima unidad, TimeProvider reloj)
    : ICommandHandler<AnalizarComentariosIaCommand>
{
    public async Task<Result> Handle(AnalizarComentariosIaCommand c, CancellationToken ct)
    {
        var r = await Encuestas.DelUsuarioAsync(encuestas, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var comentarios = Encuestas.Comentarios(await encuestas.RespuestasAsync(r.Value.Id, ct));
        var ahora = reloj.GetUtcNow();
        var hecho = r.Value.PedirAnalisis(comentarios.Count, ahora);
        if (hecho.IsFailure)
        {
            return hecho;
        }

        // Solo el texto, en desorden y sin área, sede ni fecha: la IA no puede saber de quién es cada comentario
        eventos.Publicar(new ComentariosClimaSolicitados(Guid.CreateVersion7(), ahora, r.Value.EmpresaId, r.Value.Id,
            [.. comentarios.OrderBy(x => x.GetHashCode()).Take(300)]));
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class AnalizarComentariosReglasHandler(IUsuarioActual usuario, IEncuestas encuestas, IUnidadClima unidad, TimeProvider reloj)
    : ICommandHandler<AnalizarComentariosReglasCommand>
{
    public async Task<Result> Handle(AnalizarComentariosReglasCommand c, CancellationToken ct)
    {
        var r = await Encuestas.DelUsuarioAsync(encuestas, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var comentarios = Encuestas.Comentarios(await encuestas.RespuestasAsync(r.Value.Id, ct));
        if (comentarios.Count < Encuesta.MinimoGrupo)
        {
            return Result.Failure(EncuestaErrors.PocasRespuestas);
        }

        var (temas, resumen) = TemasPorReglas.Analizar(comentarios);
        r.Value.RegistrarAnalisis("Reglas", temas, resumen, comentarios.Count, null, 0, null, reloj.GetUtcNow());
        r.Value.MarcarAnalisisRevisado(usuario.Nombre);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class MarcarAnalisisRevisadoHandler(IUsuarioActual usuario, IEncuestas encuestas, IBitacora bitacora, IUnidadClima unidad)
    : ICommandHandler<MarcarAnalisisRevisadoCommand>
{
    public async Task<Result> Handle(MarcarAnalisisRevisadoCommand c, CancellationToken ct)
    {
        var r = await Encuestas.DelUsuarioAsync(encuestas, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        r.Value.MarcarAnalisisRevisado(usuario.Nombre);
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Revisó el análisis de comentarios de «{r.Value.Titulo}»", r.Value.Titulo,
            "Decisión humana sobre la propuesta de la IA", "Encuesta", r.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Una respuesta anónima por el enlace: se guarda sin quién respondió y sin la hora exacta.</summary>
internal sealed class ResponderEncuestaHandler(IEncuestas encuestas, ITokensEncuesta tokens, IUnidadClima unidad, TimeProvider reloj)
    : ICommandHandler<ResponderEncuestaCommand>
{
    public async Task<Result> Handle(ResponderEncuestaCommand c, CancellationToken ct)
    {
        var e = await encuestas.PorTokenAsync(tokens.Huella(c.Token), ct);
        if (e is null)
        {
            return Result.Failure(EncuestaErrors.EnlaceNoExiste);
        }

        var respuesta = e.Responder(c.Area ?? "", c.Sede ?? "", c.Puntajes, c.Enps, c.Comentario, reloj.GetUtcNow());
        if (respuesta.IsFailure)
        {
            return Result.Failure(respuesta.Error);
        }

        encuestas.AgregarRespuesta(respuesta.Value);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Llegaron los temas de la IA: quedan en violeta hasta que alguien los revise.</summary>
internal sealed class TemasClimaGeneradosConsumer(IEncuestas encuestas, IBitacora bitacora, IUnidadClima unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<TemasClimaGenerados>
{
    public async Task Handle(TemasClimaGenerados e, CancellationToken ct)
    {
        var encuesta = await encuestas.PorIdInternoAsync(e.EncuestaId, ct);
        if (encuesta?.Analisis is not { Estado: "Preparando" } a)
        {
            return;
        }

        encuesta.RegistrarAnalisis("IA", [.. e.Temas.Select(t => new TemaClima
        {
            Nombre = t.Nombre, Sentimiento = t.Sentimiento, Cantidad = t.Cantidad, Citas = [.. t.Citas], Accion = t.Accion,
        })], e.Resumen, a.Comentarios, e.Modelo, e.CostoUsd, e.Error, reloj.GetUtcNow());
        bitacora.Registrar(encuesta.EmpresaId, TipoActor.Ia, "Analista de clima", $"Agrupó los comentarios de «{encuesta.Titulo}»",
            e.Error ?? $"{e.Temas.Count} temas · {a.Comentarios} comentarios · {e.Modelo}", "Espera la revisión de RRHH", "Encuesta", encuesta.Id);
        await unidad.SaveChangesAsync(ct);
    }
}
