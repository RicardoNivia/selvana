using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Clima.Application.Abstracciones;
using Selvana.Clima.Application.Encuestas;
using Selvana.Clima.Application.Pulsos;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Web.Common;

namespace Selvana.Clima.Api;

public sealed record CrearEncuestaRequest(string Titulo);

public sealed record EditarEncuestaRequest(string Titulo, IReadOnlyList<PreguntaDto> Preguntas, bool IncluyeEnps, string PreguntaAbierta);

public sealed record EnlaceResponse(string Ruta);

public sealed record SeguimientoRequest(string Nota);

public sealed record RespuestaEncuestaRequest(string Area, string Sede, IReadOnlyDictionary<string, int> Puntajes, int? Enps, string? Comentario);

public sealed record CreadoResponse(Guid Id);

public sealed record CantidadResponse(int Cantidad);

public static class ClimaEndpoints
{
    public static IEndpointRouteBuilder MapClimaEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/clima").WithTags("Clima").RequireAuthorization(Politicas.DeAccion("GestionarClima"));

        g.MapGet("/encuestas", async (int? pagina, int? tamano, IQueryHandler<ListarEncuestasQuery, Pagina<FilaEncuestaDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarEncuestasQuery(pagina, tamano), ct)).ToHttp())
            .WithName("ListarEncuestasClima").Produces<Pagina<FilaEncuestaDto>>();

        g.MapGet("/encuestas/{id:guid}", async (Guid id, IQueryHandler<ObtenerEncuestaQuery, EncuestaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerEncuestaQuery(id), ct)).ToHttp())
            .WithName("ObtenerEncuestaClima").Produces<EncuestaDto>();

        g.MapPost("/encuestas", async (CrearEncuestaRequest b, ICommandHandler<CrearEncuestaCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CrearEncuestaCommand(b.Titulo), ct);
                return r.IsSuccess ? Results.Ok(new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CrearEncuestaClima").Produces<CreadoResponse>();

        g.MapPut("/encuestas/{id:guid}", async (Guid id, EditarEncuestaRequest b, ICommandHandler<EditarEncuestaCommand> h, CancellationToken ct) =>
                (await h.Handle(new EditarEncuestaCommand(id, b.Titulo, b.Preguntas ?? [], b.IncluyeEnps, b.PreguntaAbierta ?? ""), ct)).ToHttp())
            .WithName("EditarEncuestaClima");

        g.MapPost("/encuestas/{id:guid}/abrir", async (Guid id, ICommandHandler<AbrirEncuestaCommand, string> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AbrirEncuestaCommand(id), ct);
                return r.IsSuccess ? Results.Ok(new EnlaceResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AbrirEncuestaClima").Produces<EnlaceResponse>();

        g.MapPost("/encuestas/{id:guid}/cerrar", async (Guid id, ICommandHandler<CerrarEncuestaCommand> h, CancellationToken ct) =>
                (await h.Handle(new CerrarEncuestaCommand(id), ct)).ToHttp())
            .WithName("CerrarEncuestaClima");

        g.MapPost("/encuestas/{id:guid}/analisis/ia", async (Guid id, ICommandHandler<AnalizarComentariosIaCommand> h, CancellationToken ct) =>
                (await h.Handle(new AnalizarComentariosIaCommand(id), ct)).ToHttp())
            .WithName("AnalizarComentariosIa");

        g.MapPost("/encuestas/{id:guid}/analisis/reglas", async (Guid id, ICommandHandler<AnalizarComentariosReglasCommand> h, CancellationToken ct) =>
                (await h.Handle(new AnalizarComentariosReglasCommand(id), ct)).ToHttp())
            .WithName("AnalizarComentariosReglas");

        g.MapPost("/encuestas/{id:guid}/analisis/revisado", async (Guid id, ICommandHandler<MarcarAnalisisRevisadoCommand> h, CancellationToken ct) =>
                (await h.Handle(new MarcarAnalisisRevisadoCommand(id), ct)).ToHttp())
            .WithName("MarcarAnalisisRevisado");

        g.MapGet("/pulsos", async (int? pagina, int? tamano, string? filtro, IQueryHandler<ListarPulsosQuery, Pagina<FilaPulsoDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarPulsosQuery(pagina, tamano, filtro), ct)).ToHttp())
            .WithName("ListarPulsos").Produces<Pagina<FilaPulsoDto>>();

        // Camino manual: programar ya los pulsos de quienes llegaron a los 30, 60 o 90 días (también corre solo cada seis horas)
        g.MapPost("/pulsos/programar", async (IUsuarioActual usuario, ICommandHandler<ProgramarPulsosCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new ProgramarPulsosCommand(usuario.EmpresaId), ct);
                return r.IsSuccess ? Results.Ok(new CantidadResponse(r.Value)) : r.ToHttp();
            })
            .WithName("ProgramarPulsos").Produces<CantidadResponse>();

        g.MapPost("/pulsos/{id:guid}/seguimiento", async (Guid id, SeguimientoRequest b, ICommandHandler<RegistrarSeguimientoPulsoCommand> h, CancellationToken ct) =>
                (await h.Handle(new RegistrarSeguimientoPulsoCommand(id, b.Nota ?? ""), ct)).ToHttp())
            .WithName("RegistrarSeguimientoPulso");

        g.MapGet("/rotacion", async (int? pagina, int? tamano, string? nivel, IQueryHandler<ObtenerRiesgoRotacionQuery, RiesgoRotacionDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerRiesgoRotacionQuery(pagina, tamano, nivel), ct)).ToHttp())
            .WithName("RiesgoRotacion").Produces<RiesgoRotacionDto>();

        return app;
    }

    /// <summary>La encuesta anónima por enlace: sin sesión y con límite de peticiones por IP.</summary>
    public static IEndpointRouteBuilder MapClimaPublico(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/clima/publico").WithTags("Clima · encuesta anónima").AllowAnonymous().RequireRateLimiting(ClaveInterna.PoliticaPortalPublico);

        g.MapGet("/{token}", async (string token, IQueryHandler<ObtenerEncuestaPublicaQuery, EncuestaPublicaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerEncuestaPublicaQuery(token), ct)).ToHttp())
            .WithName("EncuestaPublica").Produces<EncuestaPublicaDto>();

        g.MapPost("/{token}", async (string token, RespuestaEncuestaRequest b, ICommandHandler<ResponderEncuestaCommand> h, CancellationToken ct) =>
                (await h.Handle(new ResponderEncuestaCommand(token, b.Area ?? "", b.Sede ?? "", b.Puntajes ?? new Dictionary<string, int>(), b.Enps, b.Comentario), ct))
                .ToHttp())
            .WithName("ResponderEncuesta");

        return app;
    }
}
