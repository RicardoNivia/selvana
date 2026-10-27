using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Selvana.Core.Application.Empresas;
using Selvana.Core.Application.Estado.Obtener;
using Selvana.Core.Application.Estructura;
using Selvana.Core.Application.Estructura.AplicarImportacion;
using Selvana.Core.Application.Estructura.AprobarPerfilCargo;
using Selvana.Core.Application.Estructura.CambiarPropuesta;
using Selvana.Core.Application.Estructura.Cargos;
using Selvana.Core.Application.Estructura.EditarMaestros;
using Selvana.Core.Application.Estructura.ImportarNomina;
using Selvana.Core.Application.Sesion;
using Selvana.Core.Contracts.Empresas;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Web.Common;

namespace Selvana.Core.Api;

public sealed record CambiarPropuestaRequest(bool Incluida);

public sealed record ImportacionCreadaResponse(Guid Id);

public sealed record CrearCargoRequest(string Nombre, string Area, string? Descripcion = null, PerfilEditado? Perfil = null);

public sealed record EditarCargoRequest(string Nombre, string Area, bool Activo, PerfilEditado? Perfil);

public sealed record EditarMaestroRequest(string Nombre, bool Activo);

public sealed record EditarCentroCostoRequest(string Nombre, string Sede, bool Activo);

public sealed record CrearMaestroRequest(string Nombre);

public sealed record CrearCentroCostoRequest(string Codigo, string Nombre, string Sede);

public sealed record IngresarRequest(string Correo, string Contrasena)
{
    public override string ToString() => $"IngresarRequest {{ Correo = {Correo} }}";
}

public static class CoreEndpoints
{
    public static IEndpointRouteBuilder MapCoreEndpoints(this IEndpointRouteBuilder app)
    {
        var core = app.MapGroup("/core").WithTags("Core").RequireAuthorization();

        core.MapGet("/estado", async (IQueryHandler<ObtenerEstadoQuery, EstadoDto> handler, CancellationToken ct) =>
                (await handler.Handle(new ObtenerEstadoQuery(), ct)).ToHttp())
            .WithName("ObtenerEstadoCore")
            .Produces<EstadoDto>()
            .AllowAnonymous();

        core.MapGet("/empresa", async (IQueryHandler<ObtenerEmpresaActualQuery, EmpresaResumen> handler, CancellationToken ct) =>
                (await handler.Handle(new ObtenerEmpresaActualQuery(), ct)).ToHttp())
            .WithName("ObtenerEmpresaActual")
            .Produces<EmpresaResumen>();

        MapSesion(app.MapGroup("/sesion").WithTags("Sesión"));
        MapNomina(core.MapGroup("/nomina").WithTags("Conectar nómina"));
        MapEstructura(core.MapGroup("/estructura").WithTags("Estructura y cargos"));
        return app;
    }

    private static void MapSesion(RouteGroupBuilder sesion)
    {
        sesion.MapPost("/ingresar", async (IngresarRequest body, HttpContext http,
                ICommandHandler<IngresarCommand, ResultadoIngreso> handler, CancellationToken ct) =>
            {
                var resultado = await handler.Handle(new IngresarCommand(body.Correo, body.Contrasena), ct);
                if (resultado.IsFailure)
                {
                    return resultado.ToHttp();
                }

                if (resultado.Value.Sesion is not { } s)
                {
                    return Rechazo(resultado.Value);
                }

                await Sesiones.IniciarAsync(http, s.UsuarioId, s.Nombre, s.Rol, s.EmpresaId, s.TodasLasEmpresasUsuarias, s.EmpresasUsuarias.Select(e => e.Id));
                return Results.Ok(s);
            })
            .WithName("Ingresar")
            .AllowAnonymous()
            .RequireRateLimiting(Sesiones.PoliticaIngreso)
            .Produces<SesionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        sesion.MapGet("/", async (IQueryHandler<ObtenerSesionQuery, SesionDto> handler, CancellationToken ct) =>
                (await handler.Handle(new ObtenerSesionQuery(), ct)).ToHttp())
            .WithName("ObtenerSesion")
            .RequireAuthorization()
            .Produces<SesionDto>();

        sesion.MapPost("/salir", async (HttpContext http) =>
            {
                await Sesiones.CerrarAsync(http);
                return Results.NoContent();
            })
            .WithName("Salir")
            .AllowAnonymous();
    }

    private static IResult Rechazo(ResultadoIngreso r) => r.Motivo switch
    {
        MotivoRechazo.Bloqueado => Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Ingreso bloqueado", detail: r.Mensaje),
        MotivoRechazo.Desactivado => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Acceso desactivado", detail: r.Mensaje),
        _ => Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No pudimos ingresar", detail: r.Mensaje),
    };

    private static void MapNomina(RouteGroupBuilder nomina)
    {
        nomina.MapPost("/importaciones", async (IFormFile archivo, [FromForm] string? fuente,
                ICommandHandler<ImportarNominaCommand, Guid> handler, CancellationToken ct) =>
            {
                await using var contenido = archivo.OpenReadStream();
                var resultado = await handler.Handle(new ImportarNominaCommand(fuente ?? "Carga manual", contenido), ct);
                return resultado.IsSuccess
                    ? Results.Created($"/api/v1/core/nomina/importaciones/{resultado.Value}", new ImportacionCreadaResponse(resultado.Value))
                    : resultado.ToHttp();
            })
            .WithName("ImportarNomina")
            .DisableAntiforgery()
            .Produces<ImportacionCreadaResponse>(StatusCodes.Status201Created);

        nomina.MapGet("/importaciones/actual", async (IQueryHandler<ObtenerImportacionActualQuery, ImportacionDto?> handler, CancellationToken ct) =>
                (await handler.Handle(new ObtenerImportacionActualQuery(), ct)).ToHttp())
            .WithName("ObtenerImportacionActual")
            .Produces<ImportacionDto>();

        nomina.MapPatch("/importaciones/{importacionId:guid}/propuestas/{propuestaId:guid}",
                async (Guid importacionId, Guid propuestaId, CambiarPropuestaRequest body, ICommandHandler<CambiarPropuestaCommand> handler, CancellationToken ct) =>
                    (await handler.Handle(new CambiarPropuestaCommand(importacionId, propuestaId, body.Incluida), ct)).ToHttp())
            .WithName("CambiarPropuesta");

        nomina.MapPost("/importaciones/{importacionId:guid}/aplicar",
                async (Guid importacionId, ICommandHandler<AplicarImportacionCommand> handler, CancellationToken ct) =>
                    (await handler.Handle(new AplicarImportacionCommand(importacionId), ct)).ToHttp())
            .WithName("AplicarImportacion");
    }

    private static void MapEstructura(RouteGroupBuilder estructura)
    {
        estructura.MapGet("/resumen", async (IQueryHandler<ObtenerResumenEstructuraQuery, ResumenEstructuraDto> handler, CancellationToken ct) =>
                (await handler.Handle(new ObtenerResumenEstructuraQuery(), ct)).ToHttp())
            .WithName("ObtenerResumenEstructura")
            .Produces<ResumenEstructuraDto>();

        estructura.MapGet("/cargos", async (int? pagina, int? tamano, string? buscar, string? estadoPerfil,
                IQueryHandler<ListarCargosQuery, Pagina<CargoDto>> handler, CancellationToken ct) =>
                (await handler.Handle(new ListarCargosQuery(pagina, tamano, buscar, estadoPerfil), ct)).ToHttp())
            .WithName("ListarCargos")
            .Produces<Pagina<CargoDto>>();

        estructura.MapPost("/cargos", async (CrearCargoRequest body, ICommandHandler<CrearCargoCommand, Guid> handler, CancellationToken ct) =>
            {
                var resultado = await handler.Handle(new CrearCargoCommand(body.Nombre, body.Area, body.Descripcion, body.Perfil), ct);
                return resultado.IsSuccess
                    ? Results.Created($"/api/v1/core/estructura/cargos/{resultado.Value}", new ImportacionCreadaResponse(resultado.Value))
                    : resultado.ToHttp();
            })
            .WithName("CrearCargo")
            .Produces<ImportacionCreadaResponse>(StatusCodes.Status201Created);

        estructura.MapGet("/cargos/{cargoId:guid}", async (Guid cargoId, IQueryHandler<ObtenerCargoQuery, CargoDto> handler, CancellationToken ct) =>
                (await handler.Handle(new ObtenerCargoQuery(cargoId), ct)).ToHttp())
            .WithName("ObtenerCargo")
            .Produces<CargoDto>();

        estructura.MapPut("/cargos/{cargoId:guid}",async (Guid cargoId, EditarCargoRequest body, ICommandHandler<EditarCargoCommand> handler, CancellationToken ct) =>
                (await handler.Handle(new EditarCargoCommand(cargoId, body.Nombre, body.Area, body.Activo, body.Perfil), ct)).ToHttp())
            .WithName("EditarCargo");

        estructura.MapPost("/cargos/{cargoId:guid}/aprobar-perfil",
                async (Guid cargoId, ICommandHandler<AprobarPerfilCargoCommand> handler, CancellationToken ct) =>
                    (await handler.Handle(new AprobarPerfilCargoCommand(cargoId), ct)).ToHttp())
            .WithName("AprobarPerfilCargo");

        estructura.MapPost("/cargos/{cargoId:guid}/solicitar-perfil",
                async (Guid cargoId, ICommandHandler<SolicitarPerfilCargoCommand> handler, CancellationToken ct) =>
                    (await handler.Handle(new SolicitarPerfilCargoCommand(cargoId), ct)).ToHttp())
            .WithName("SolicitarPerfilCargo");

        estructura.MapGet("/sedes", async (int? pagina, int? tamano, string? buscar, IQueryHandler<ListarSedesQuery, Pagina<MaestroDto>> handler, CancellationToken ct) =>
                (await handler.Handle(new ListarSedesQuery(pagina, tamano, buscar), ct)).ToHttp())
            .WithName("ListarSedes")
            .Produces<Pagina<MaestroDto>>();

        estructura.MapPut("/sedes/{id:guid}", async (Guid id, EditarMaestroRequest body, ICommandHandler<EditarSedeCommand> handler, CancellationToken ct) =>
                (await handler.Handle(new EditarSedeCommand(id, body.Nombre, body.Activo), ct)).ToHttp())
            .WithName("EditarSede");

        estructura.MapGet("/areas", async (int? pagina, int? tamano, string? buscar, IQueryHandler<ListarAreasQuery, Pagina<MaestroDto>> handler, CancellationToken ct) =>
                (await handler.Handle(new ListarAreasQuery(pagina, tamano, buscar), ct)).ToHttp())
            .WithName("ListarAreas")
            .Produces<Pagina<MaestroDto>>();

        estructura.MapPut("/areas/{id:guid}", async (Guid id, EditarMaestroRequest body, ICommandHandler<EditarAreaCommand> handler, CancellationToken ct) =>
                (await handler.Handle(new EditarAreaCommand(id, body.Nombre, body.Activo), ct)).ToHttp())
            .WithName("EditarArea");

        estructura.MapGet("/centros-costo", async (int? pagina, int? tamano, string? buscar, IQueryHandler<ListarCentrosCostoQuery, Pagina<CentroCostoDto>> handler, CancellationToken ct) =>
                (await handler.Handle(new ListarCentrosCostoQuery(pagina, tamano, buscar), ct)).ToHttp())
            .WithName("ListarCentrosCosto")
            .Produces<Pagina<CentroCostoDto>>();

        estructura.MapPut("/centros-costo/{id:guid}", async (Guid id, EditarCentroCostoRequest body, ICommandHandler<EditarCentroCostoCommand> handler, CancellationToken ct) =>
                (await handler.Handle(new EditarCentroCostoCommand(id, body.Nombre, body.Sede, body.Activo), ct)).ToHttp())
            .WithName("EditarCentroCosto");

        estructura.MapPost("/sedes", async (CrearMaestroRequest body, ICommandHandler<CrearSedeCommand, Guid> handler, CancellationToken ct) =>
                Creado(await handler.Handle(new CrearSedeCommand(body.Nombre), ct), "sedes"))
            .WithName("CrearSede")
            .Produces<ImportacionCreadaResponse>(StatusCodes.Status201Created);

        estructura.MapPost("/areas", async (CrearMaestroRequest body, ICommandHandler<CrearAreaCommand, Guid> handler, CancellationToken ct) =>
                Creado(await handler.Handle(new CrearAreaCommand(body.Nombre), ct), "areas"))
            .WithName("CrearArea")
            .Produces<ImportacionCreadaResponse>(StatusCodes.Status201Created);

        estructura.MapPost("/centros-costo", async (CrearCentroCostoRequest body, ICommandHandler<CrearCentroCostoCommand, Guid> handler, CancellationToken ct) =>
                Creado(await handler.Handle(new CrearCentroCostoCommand(body.Codigo, body.Nombre, body.Sede), ct), "centros-costo"))
            .WithName("CrearCentroCosto")
            .Produces<ImportacionCreadaResponse>(StatusCodes.Status201Created);
    }

    private static IResult Creado(Result<Guid> resultado, string coleccion) =>
        resultado.IsSuccess
            ? Results.Created($"/api/v1/core/estructura/{coleccion}/{resultado.Value}", new ImportacionCreadaResponse(resultado.Value))
            : resultado.ToHttp();
}
