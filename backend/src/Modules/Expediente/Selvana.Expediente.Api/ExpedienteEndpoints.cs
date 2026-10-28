using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Expediente.Application.Empleados;
using Selvana.SharedKernel.Messaging;
using Selvana.Web.Common;

namespace Selvana.Expediente.Api;

public sealed record CrearEmpleadoRequest(
    string Documento, string Nombre, string Correo, string Celular, string Ciudad, SituacionDto Situacion, DateOnly FechaIngreso, AfiliacionesDto Afiliaciones);

public sealed record DatosEmpleadoRequest(string Nombre, string Correo, string Celular, string Ciudad, AfiliacionesDto Afiliaciones);

public sealed record NovedadRequest(string Tipo, SituacionDto Situacion, DateOnly FechaEfectiva, string Motivo);

public sealed record RetiroRequest(DateOnly Fecha, string Motivo);

public sealed record CreadoResponse(Guid Id);

public static class ExpedienteEndpoints
{
    public static IEndpointRouteBuilder MapExpedienteEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/expediente/empleados").WithTags("Empleados · expediente").RequireAuthorization(Politicas.DeAccion("GestionarEmpleados"));

        g.MapGet("", async (int? pagina, int? tamano, string? buscar, string? estado, bool? alertas, IQueryHandler<ListarEmpleadosQuery, ListaEmpleadosDto> h,
                CancellationToken ct) => (await h.Handle(new ListarEmpleadosQuery(pagina, tamano, buscar, estado, alertas ?? false), ct)).ToHttp())
            .WithName("ListarEmpleados").Produces<ListaEmpleadosDto>();

        g.MapGet("/{id:guid}", async (Guid id, IQueryHandler<ObtenerEmpleadoQuery, EmpleadoDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerEmpleadoQuery(id), ct)).ToHttp())
            .WithName("ObtenerEmpleado").Produces<EmpleadoDto>();

        g.MapPost("", async (CrearEmpleadoRequest b, ICommandHandler<CrearEmpleadoManualCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CrearEmpleadoManualCommand(b.Documento, b.Nombre, b.Correo, b.Celular, b.Ciudad, b.Situacion, b.FechaIngreso, b.Afiliaciones), ct);
                return r.IsSuccess ? Results.Ok(new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CrearEmpleadoManual").Produces<CreadoResponse>();

        g.MapPut("/{id:guid}/datos", async (Guid id, DatosEmpleadoRequest b, ICommandHandler<ActualizarDatosEmpleadoCommand> h, CancellationToken ct) =>
                (await h.Handle(new ActualizarDatosEmpleadoCommand(id, b.Nombre, b.Correo, b.Celular, b.Ciudad, b.Afiliaciones), ct)).ToHttp())
            .WithName("ActualizarDatosEmpleado");

        g.MapPost("/{id:guid}/novedades", async (Guid id, NovedadRequest b, ICommandHandler<RegistrarNovedadCommand> h, CancellationToken ct) =>
                (await h.Handle(new RegistrarNovedadCommand(id, b.Tipo, b.Situacion, b.FechaEfectiva, b.Motivo), ct)).ToHttp())
            .WithName("RegistrarNovedad");

        g.MapPost("/{id:guid}/retiro", async (Guid id, RetiroRequest b, ICommandHandler<RetirarEmpleadoCommand> h, CancellationToken ct) =>
                (await h.Handle(new RetirarEmpleadoCommand(id, b.Fecha, b.Motivo), ct)).ToHttp())
            .WithName("RetirarEmpleado").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/{id:guid}/documentos", async (Guid id, IFormFile archivo, Guid? documentoId, string? tipo, string? nombre, DateOnly? venceEn, string? nota,
                ICommandHandler<GuardarDocumentoEmpleadoCommand, Guid> h, CancellationToken ct) =>
            {
                await using var flujo = archivo.OpenReadStream();
                using var memoria = new MemoryStream();
                await flujo.CopyToAsync(memoria, ct);
                // Algunos navegadores mandan el Word sin tipo: se deduce por la extensión
                var tipoContenido = Path.GetExtension(archivo.FileName).ToUpperInvariant() switch
                {
                    ".PDF" => "application/pdf",
                    ".DOCX" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    _ => archivo.ContentType,
                };
                var r = await h.Handle(new GuardarDocumentoEmpleadoCommand(id, documentoId, tipo ?? "", nombre ?? "", venceEn, nota, archivo.FileName, tipoContenido,
                    memoria.ToArray()), ct);
                return r.IsSuccess ? Results.Ok(new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("GuardarDocumentoEmpleado").DisableAntiforgery().Produces<CreadoResponse>();

        g.MapPost("/{id:guid}/archivos/{archivoId:guid}", async (Guid id, Guid archivoId, IQueryHandler<ObtenerArchivoEmpleadoQuery, ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new ObtenerArchivoEmpleadoQuery(id, archivoId), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("DescargarArchivoEmpleado");

        return app;
    }
}
