using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Reclutamiento.Application;
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

public sealed record PerfilRequest(
    string Nombre, string Celular, string Ciudad, string? LinkedIn, IReadOnlyList<ExperienciaDto> Experiencia, IReadOnlyList<FormacionDto> Formacion,
    IReadOnlyList<string> Cursos, IReadOnlyList<string> Habilidades);

public sealed record EnviarMiDocumentoRequest(Guid ProcesoId, string Tipo);

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

        // Su hoja de vida escrita o corregida a mano
        cuenta.MapPut("/perfil", async (PerfilRequest b, ICommandHandler<ActualizarMiPerfilCommand> h, CancellationToken ct) =>
                (await h.Handle(new ActualizarMiPerfilCommand(new PerfilEditable(new DatosEditables(b.Nombre, b.Celular, b.Ciudad, b.LinkedIn),
                    b.Experiencia ?? [], b.Formacion ?? [], b.Cursos ?? [], b.Habilidades ?? [])), ct)).ToHttp())
            .WithName("ActualizarMiPerfilPortal");

        // Historial del chat de preselección de una de sus postulaciones
        cuenta.MapGet("/postulaciones/{postulacionId:guid}/chat", async (Guid postulacionId, IQueryHandler<ObtenerChatDePostulacionQuery, ChatDePostulacionDto> h,
                CancellationToken ct) => (await h.Handle(new ObtenerChatDePostulacionQuery(postulacionId), ct)).ToHttp())
            .WithName("ChatDePostulacionPortal").Produces<ChatDePostulacionDto>();

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

        // P4 · Mis documentos: se suben una vez (cifrados) y se entregan a los procesos que los piden
        cuenta.MapGet("/documentos", async (IQueryHandler<ObtenerMisDocumentosQuery, MisDocumentosDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerMisDocumentosQuery(), ct)).ToHttp())
            .WithName("MisDocumentos").Produces<MisDocumentosDto>();

        cuenta.MapPost("/documentos", async (IFormFile archivo, string tipo, DateOnly? venceEn, ICommandHandler<SubirMiDocumentoCommand, Guid> h, CancellationToken ct) =>
            {
                await using var flujo = archivo.OpenReadStream();
                using var memoria = new MemoryStream();
                await flujo.CopyToAsync(memoria, ct);
                var tipoContenido = Path.GetExtension(archivo.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : archivo.ContentType;
                var r = await h.Handle(new SubirMiDocumentoCommand(tipo, venceEn, archivo.FileName, tipoContenido, memoria.ToArray()), ct);
                return r.IsSuccess ? Results.Ok(new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("SubirMiDocumento").DisableAntiforgery().Produces<CreadoResponse>();

        cuenta.MapDelete("/documentos/{id:guid}", async (Guid id, ICommandHandler<QuitarMiDocumentoCommand> h, CancellationToken ct) =>
                (await h.Handle(new QuitarMiDocumentoCommand(id), ct)).ToHttp())
            .WithName("QuitarMiDocumento");

        cuenta.MapGet("/documentos/{id:guid}/archivo", async (Guid id, IQueryHandler<DescargarMiDocumentoQuery, Application.Privacidad.ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new DescargarMiDocumentoQuery(id), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("DescargarMiDocumento");

        cuenta.MapPost("/documentos/enviar", async (EnviarMiDocumentoRequest b, ICommandHandler<EnviarMiDocumentoCommand> h, CancellationToken ct) =>
                (await h.Handle(new EnviarMiDocumentoCommand(b.ProcesoId, b.Tipo), ct)).ToHttp())
            .WithName("EnviarMiDocumento");

        // P5 · Mi empleo: su expediente, proponer cambios a sus datos, responder la campaña y subir soportes
        cuenta.MapGet("/empleo", async (IQueryHandler<ObtenerMiEmpleoQuery, IReadOnlyList<Expediente.Contracts.EmpleoDeLaPersona>> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerMiEmpleoQuery(), ct)).ToHttp())
            .WithName("MiEmpleo").Produces<IReadOnlyList<Expediente.Contracts.EmpleoDeLaPersona>>();

        cuenta.MapPut("/empleo/{empleadoId:guid}/datos", async (Guid empleadoId, Expediente.Contracts.DatosPropuestosPorElEmpleado b, ICommandHandler<ProponerMisDatosCommand> h,
                CancellationToken ct) => (await h.Handle(new ProponerMisDatosCommand(empleadoId, b), ct)).ToHttp())
            .WithName("ProponerMisDatos");

        cuenta.MapPost("/empleo/{empleadoId:guid}/confirmar", async (Guid empleadoId, ICommandHandler<ConfirmarMisDatosCommand> h, CancellationToken ct) =>
                (await h.Handle(new ConfirmarMisDatosCommand(empleadoId), ct)).ToHttp())
            .WithName("ConfirmarMisDatos");

        cuenta.MapPost("/empleo/{empleadoId:guid}/soportes", async (Guid empleadoId, IFormFile archivo, string tipo, string nombre, DateOnly? venceEn,
                ICommandHandler<SubirSoporteEmpleoCommand> h, CancellationToken ct) =>
            {
                await using var flujo = archivo.OpenReadStream();
                using var memoria = new MemoryStream();
                await flujo.CopyToAsync(memoria, ct);
                var tipoContenido = Path.GetExtension(archivo.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : archivo.ContentType;
                return (await h.Handle(new SubirSoporteEmpleoCommand(empleadoId, tipo, nombre, venceEn, archivo.FileName, tipoContenido, memoria.ToArray()), ct)).ToHttp();
            })
            .WithName("SubirSoporteEmpleo").DisableAntiforgery();

        cuenta.MapGet("/empleo/{empleadoId:guid}/documentos/{documentoId:guid}", async (Guid empleadoId, Guid documentoId,
                IQueryHandler<DescargarDocumentoEmpleoQuery, Application.Privacidad.ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new DescargarDocumentoEmpleoQuery(empleadoId, documentoId), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("DescargarDocumentoEmpleo");

        return app;
    }
}
