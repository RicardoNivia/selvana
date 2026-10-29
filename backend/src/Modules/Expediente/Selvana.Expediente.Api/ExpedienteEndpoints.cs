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

public sealed record MotivoRequest(string Motivo);

/// <summary>A cuántos empleados se les pidió actualizar sus datos.</summary>
public sealed record CampanaResponse(int Pedidos);

public sealed record AplicarLecturaRequest(IReadOnlyList<CambioAplicadoRequest> Cambios);

public static class ExpedienteEndpoints
{
    /// <summary>El lector de IA descarga el documento descifrado. Solo con la clave interna del worker.</summary>
    public static IEndpointRouteBuilder MapExpedienteInterno(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/archivos/expediente/{id:guid}", async (Guid id, Application.Abstracciones.IAlmacenArchivos almacen, CancellationToken ct) =>
                await almacen.LeerInternoAsync(id, ct) is { } a ? Results.File(a.Contenido, a.TipoContenido, a.Nombre) : Results.NotFound())
            .RequiereClaveInterna();
        return app;
    }

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
                bool? leerConIa, ICommandHandler<GuardarDocumentoEmpleadoCommand, Guid> h, CancellationToken ct) =>
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
                    memoria.ToArray(), leerConIa ?? false), ct);
                return r.IsSuccess ? Results.Ok(new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("GuardarDocumentoEmpleado").DisableAntiforgery().Produces<CreadoResponse>();

        // Actualización inteligente (E2): la IA lee, las reglas proponen y una persona aplica o descarta
        g.MapPost("/{id:guid}/documentos/{documentoId:guid}/leer", async (Guid id, Guid documentoId, ICommandHandler<PedirLecturaDocumentoCommand> h, CancellationToken ct) =>
                (await h.Handle(new PedirLecturaDocumentoCommand(id, documentoId), ct)).ToHttp())
            .WithName("PedirLecturaDocumento");

        g.MapPost("/{id:guid}/documentos/{documentoId:guid}/aplicar", async (Guid id, Guid documentoId, AplicarLecturaRequest b, ICommandHandler<AplicarLecturaCommand> h,
                CancellationToken ct) => (await h.Handle(new AplicarLecturaCommand(id, documentoId, b.Cambios ?? []), ct)).ToHttp())
            .WithName("AplicarLectura");

        g.MapPost("/{id:guid}/documentos/{documentoId:guid}/descartar", async (Guid id, Guid documentoId, ICommandHandler<DescartarLecturaCommand> h, CancellationToken ct) =>
                (await h.Handle(new DescartarLecturaCommand(id, documentoId), ct)).ToHttp())
            .WithName("DescartarLectura");

        g.MapPost("/{id:guid}/archivos/{archivoId:guid}", async (Guid id, Guid archivoId, IQueryHandler<ObtenerArchivoEmpleadoQuery, ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new ObtenerArchivoEmpleadoQuery(id, archivoId), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("DescargarArchivoEmpleado");

        // Hoja de vida del empleado: la de su postulación o la que compartió desde su portal
        g.MapGet("/{id:guid}/hoja-de-vida", async (Guid id, IQueryHandler<ObtenerHojaDeVidaEmpleadoQuery, HojaDeVidaEmpleadoDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerHojaDeVidaEmpleadoQuery(id), ct)).ToHttp())
            .WithName("HojaDeVidaEmpleado").Produces<HojaDeVidaEmpleadoDto>();

        g.MapPost("/{id:guid}/hoja-de-vida/abrir", async (Guid id, MotivoRequest b, ICommandHandler<AbrirHojaDeVidaCompartidaCommand, ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AbrirHojaDeVidaCompartidaCommand(id, b.Motivo ?? ""), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("AbrirHojaDeVidaCompartida");

        // P5 · Lo que el empleado propuso desde su portal: una persona aplica o descarta
        g.MapPost("/{id:guid}/datos-propuestos/aplicar", async (Guid id, ICommandHandler<AplicarCambioPropuestoCommand> h, CancellationToken ct) =>
                (await h.Handle(new AplicarCambioPropuestoCommand(id), ct)).ToHttp())
            .WithName("AplicarDatosPropuestos");

        g.MapPost("/{id:guid}/datos-propuestos/descartar", async (Guid id, ICommandHandler<DescartarCambioPropuestoCommand> h, CancellationToken ct) =>
                (await h.Handle(new DescartarCambioPropuestoCommand(id), ct)).ToHttp())
            .WithName("DescartarDatosPropuestos");

        // Campaña de actualización de datos: a un empleado o a todos los activos que ve el usuario
        g.MapPost("/{id:guid}/pedir-actualizacion", async (Guid id, ICommandHandler<PedirActualizacionDatosCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new PedirActualizacionDatosCommand(id), ct);
                return r.IsSuccess ? Results.Ok(new CampanaResponse(r.Value)) : r.ToHttp();
            })
            .WithName("PedirActualizacionEmpleado").Produces<CampanaResponse>();

        g.MapPost("/campana-actualizacion", async (ICommandHandler<PedirActualizacionDatosCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new PedirActualizacionDatosCommand(null), ct);
                return r.IsSuccess ? Results.Ok(new CampanaResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CampanaActualizacionDatos").Produces<CampanaResponse>();

        // Planta que ya existía: primero se previsualiza (no guarda nada) y una persona confirma la carga
        var planta = app.MapGroup("/expediente/planta").WithTags("Empleados · carga de planta").RequireAuthorization(Politicas.DeAccion("GestionarEmpleados"));

        planta.MapPost("/previsualizar", async (IFormFile archivo, IQueryHandler<PrevisualizarPlantaQuery, PlantaPrevisualizadaDto> h, CancellationToken ct) =>
                (await h.Handle(new PrevisualizarPlantaQuery(await Leer(archivo, ct)), ct)).ToHttp())
            .WithName("PrevisualizarPlanta").DisableAntiforgery().Produces<PlantaPrevisualizadaDto>();

        planta.MapPost("", async (IFormFile archivo, ICommandHandler<CargarPlantaCommand, PlantaCargadaDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CargarPlantaCommand(await Leer(archivo, ct)), ct);
                return r.IsSuccess ? Results.Ok(r.Value) : r.ToHttp();
            })
            .WithName("CargarPlanta").DisableAntiforgery().Produces<PlantaCargadaDto>();

        return app;
    }

    private static async Task<byte[]> Leer(IFormFile archivo, CancellationToken ct)
    {
        await using var flujo = archivo.OpenReadStream();
        using var memoria = new MemoryStream();
        await flujo.CopyToAsync(memoria, ct);
        return memoria.ToArray();
    }
}
