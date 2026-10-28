using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Reclutamiento.Application.Portal;
using Selvana.SharedKernel.Messaging;
using Selvana.Web.Common;

namespace Selvana.Reclutamiento.Api;

public sealed record RegistrarCuentaRequest(string Correo, string Contrasena, string Nombre, string TipoDocumento, string NumeroDocumento, bool AceptaAutorizacion);

public sealed record IngresarCuentaRequest(string Correo, string Contrasena)
{
    public override string ToString() => $"IngresarCuentaRequest {{ Correo = {Correo} }}";
}

public sealed record DatosCuentaRequest(string Nombre, string Celular, string Ciudad, string? LinkedIn);

public sealed record PendienteAbiertoResponse(string Ruta);

public sealed record ActualizarHojaDeVidaRequest(Guid BorradorId, string Nombre, string Celular, string Ciudad, string? LinkedIn);

/// <summary>Portal de la persona (ADR 0013): registro con la hoja de vida, ingreso y su cuenta. Sesión propia, distinta de la consola.</summary>
public static class PortalPersonaEndpoints
{
    public static IEndpointRouteBuilder MapPortalPersona(this IEndpointRouteBuilder app)
    {
        var publico = app.MapGroup("/portal/cuenta").WithTags("Portal de la persona").AllowAnonymous().RequireRateLimiting(SesionesPortal.PoliticaIngreso);

        var cuenta = app.MapGroup("/portal/cuenta").WithTags("Portal de la persona").RequireAuthorization(SesionesPortal.Politica);

        // Ya con cuenta, sube su hoja de vida y la IA la lee (igual que al postularse)
        cuenta.MapPost("/hoja-de-vida", async (IFormFile hojaDeVida, ICommandHandler<SubirHojaDeVidaPerfilCommand, Guid> h, CancellationToken ct) =>
            {
                await using var flujo = hojaDeVida.OpenReadStream();
                using var memoria = new MemoryStream();
                await flujo.CopyToAsync(memoria, ct);
                var r = await h.Handle(new SubirHojaDeVidaPerfilCommand(hojaDeVida.FileName, hojaDeVida.ContentType, memoria.ToArray()), ct);
                return r.IsSuccess ? Results.Created($"/api/v1/portal/cuenta/hoja-de-vida/{r.Value}", new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("SubirHojaDeVidaPerfil").DisableAntiforgery().Produces<CreadoResponse>(StatusCodes.Status201Created);

        cuenta.MapGet("/hoja-de-vida/{borradorId:guid}", async (Guid borradorId, IQueryHandler<ObtenerHojaDeVidaPerfilQuery, HojaDeVidaPerfilDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerHojaDeVidaPerfilQuery(borradorId), ct)).ToHttp())
            .WithName("ObtenerHojaDeVidaPerfil").Produces<HojaDeVidaPerfilDto>();

        // Registro con lo mínimo (correo, nombre, identificación, contraseña y autorización); queda con sesión
        publico.MapPost("/registro", async (RegistrarCuentaRequest b, HttpContext http, ICommandHandler<RegistrarCuentaCommand, SesionPortal> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new RegistrarCuentaCommand(b.Correo, b.Contrasena, b.Nombre, b.TipoDocumento, b.NumeroDocumento, b.AceptaAutorizacion,
                    http.Connection.RemoteIpAddress?.ToString() ?? "", http.Request.Headers.UserAgent.ToString()), ct);
                if (r.IsFailure)
                {
                    return r.ToHttp();
                }

                await SesionesPortal.IniciarAsync(http, r.Value.CuentaId, r.Value.Nombre);
                return Results.Ok(r.Value);
            })
            .WithName("RegistrarCuentaPortal").Produces<SesionPortal>();

        publico.MapPost("/ingresar", async (IngresarCuentaRequest b, HttpContext http, ICommandHandler<IngresarCuentaCommand, SesionPortal> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new IngresarCuentaCommand(b.Correo, b.Contrasena), ct);
                if (r.IsFailure)
                {
                    return r.ToHttp();
                }

                await SesionesPortal.IniciarAsync(http, r.Value.CuentaId, r.Value.Nombre);
                return Results.Ok(r.Value);
            })
            .WithName("IngresarPortal").Produces<SesionPortal>();

        cuenta.MapPost("/salir", async (HttpContext http) =>
            {
                await SesionesPortal.CerrarAsync(http);
                return Results.NoContent();
            })
            .WithName("SalirPortal");

        cuenta.MapGet("/", async (IQueryHandler<ObtenerMiCuentaQuery, MiCuentaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerMiCuentaQuery(), ct)).ToHttp())
            .WithName("MiCuentaPortal").Produces<MiCuentaDto>();

        cuenta.MapPut("/datos", async (DatosCuentaRequest b, ICommandHandler<ActualizarMisDatosCommand> h, CancellationToken ct) =>
                (await h.Handle(new ActualizarMisDatosCommand(new DatosEditables(b.Nombre, b.Celular, b.Ciudad, b.LinkedIn)), ct)).ToHttp())
            .WithName("ActualizarMisDatosPortal");

        cuenta.MapPut("/hoja-de-vida", async (ActualizarHojaDeVidaRequest b, ICommandHandler<ActualizarMiHojaDeVidaCommand> h, CancellationToken ct) =>
                (await h.Handle(new ActualizarMiHojaDeVidaCommand(b.BorradorId, new DatosEditables(b.Nombre, b.Celular, b.Ciudad, b.LinkedIn)), ct)).ToHttp())
            .WithName("ActualizarMiHojaDeVidaPortal");

        // Mis postulaciones y mis pendientes (chat, pruebas, oferta y firma, documentos)
        cuenta.MapGet("/portal", async (IQueryHandler<ObtenerMiPortalQuery, MiPortalDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerMiPortalQuery(), ct)).ToHttp())
            .WithName("MiPortal").Produces<MiPortalDto>();

        // Abre un pendiente: renueva el enlace solo si es de la persona y devuelve la pantalla donde se responde
        cuenta.MapPost("/pendientes/{tipo}/{id:guid}/abrir", async (string tipo, Guid id, ICommandHandler<AbrirPendienteCommand, string> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AbrirPendienteCommand(tipo, id), ct);
                return r.IsSuccess ? Results.Ok(new PendienteAbiertoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AbrirPendientePortal").Produces<PendienteAbiertoResponse>();

        // Ofertas que encajan con su hoja de vida y postularse con el perfil (responde las preguntas en la pantalla de postulación)
        cuenta.MapGet("/ofertas", async (IQueryHandler<ObtenerOfertasParaTiQuery, IReadOnlyList<OfertaParaTiDto>> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerOfertasParaTiQuery(), ct)).ToHttp())
            .WithName("OfertasParaTi").Produces<IReadOnlyList<OfertaParaTiDto>>();

        cuenta.MapPost("/postular/{codigo}", async (string codigo, ICommandHandler<PostularmeConMiPerfilCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new PostularmeConMiPerfilCommand(codigo), ct);
                return r.IsSuccess ? Results.Ok(new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("PostularmeConMiPerfil").Produces<CreadoResponse>();

        return app;
    }
}
