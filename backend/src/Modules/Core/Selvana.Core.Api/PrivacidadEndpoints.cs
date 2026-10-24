using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Core.Application.Privacidad;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Web.Common;

namespace Selvana.Core.Api;

public sealed record RegistrarSolicitudRequest(string Tipo, string Titular, string Documento, string? Contacto, string Canal, string? Descripcion);

public sealed record SolicitudPortalRequest(string Tipo, string Titular, string Documento, string Contacto, string? Descripcion);

public sealed record ResponderSolicitudRequest(string Respuesta, bool AprobarSupresion);

/// <summary>Privacidad y auditoría (bloque 2.3): bitácora encadenada y solicitudes de titulares (Ley 1581).</summary>
public static class PrivacidadEndpoints
{
    public static IEndpointRouteBuilder MapPrivacidadEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/core/privacidad").WithTags("Privacidad y auditoría").RequireAuthorization(Politicas.VerBitacora);

        g.MapGet("/resumen", async (IQueryHandler<ObtenerResumenPrivacidadQuery, ResumenPrivacidadDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerResumenPrivacidadQuery(), ct)).ToHttp())
            .WithName("ResumenPrivacidad")
            .Produces<ResumenPrivacidadDto>();

        g.MapGet("/bitacora", async (int? pagina, int? tamano, string? tipo, string? buscar, bool? sensibles,
                IQueryHandler<ListarBitacoraQuery, Pagina<EntradaBitacoraDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarBitacoraQuery(pagina, tamano, tipo, buscar, sensibles ?? false), ct)).ToHttp())
            .WithName("ListarBitacora")
            .Produces<Pagina<EntradaBitacoraDto>>();

        g.MapGet("/bitacora/verificar", async (IQueryHandler<VerificarBitacoraQuery, VerificacionDto> h, CancellationToken ct) =>
                (await h.Handle(new VerificarBitacoraQuery(), ct)).ToHttp())
            .WithName("VerificarBitacora")
            .Produces<VerificacionDto>();

        g.MapGet("/solicitudes", async (int? pagina, int? tamano, string? estado, IQueryHandler<ListarSolicitudesQuery, Pagina<SolicitudDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarSolicitudesQuery(pagina, tamano, estado), ct)).ToHttp())
            .WithName("ListarSolicitudesTitular")
            .Produces<Pagina<SolicitudDto>>();

        g.MapPost("/solicitudes", async (RegistrarSolicitudRequest b, ICommandHandler<RegistrarSolicitudCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new RegistrarSolicitudCommand(b.Tipo, b.Titular, b.Documento, b.Contacto ?? "", b.Canal, b.Descripcion ?? ""), ct);
                return r.IsSuccess ? Results.Created($"/api/v1/core/privacidad/solicitudes/{r.Value}", new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("RegistrarSolicitudTitular")
            .Produces<CreadoResponse>(StatusCodes.Status201Created);

        g.MapPost("/solicitudes/{id:guid}/responder", async (Guid id, ResponderSolicitudRequest b, ICommandHandler<ResponderSolicitudCommand> h, CancellationToken ct) =>
                (await h.Handle(new ResponderSolicitudCommand(id, b.Respuesta, b.AprobarSupresion), ct)).ToHttp())
            .WithName("ResponderSolicitudTitular");

        // Sin sesión: el titular pide desde el portal de empleo. Siempre la misma respuesta, tenga o no datos (no se filtra quién está)
        app.MapPost("/portal/solicitudes-titular", async (SolicitudPortalRequest b, ICommandHandler<RegistrarSolicitudPortalCommand> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new RegistrarSolicitudPortalCommand(b.Tipo, b.Titular, b.Documento, b.Contacto, b.Descripcion ?? ""), ct);
                return r.IsSuccess ? Results.Accepted() : r.ToHttp();
            })
            .WithName("SolicitudTitularPortal")
            .WithTags("Portal de empleo público")
            .AllowAnonymous()
            .RequireRateLimiting(Sesiones.PoliticaIngreso);

        return app;
    }
}
