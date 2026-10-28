using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.SharedKernel.Messaging;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Application.Alta;
using Selvana.Vinculacion.Application.Documentos;
using Selvana.Vinculacion.Application.Onboarding;
using Selvana.Vinculacion.Application.Procesos;
using Selvana.Vinculacion.Application.TiposContrato;
using Selvana.Web.Common;

namespace Selvana.Vinculacion.Api;

public sealed record AprobarContratacionRequest(Guid PostulacionId, CondicionesRequest Condiciones);

public sealed record EditarOfertaRequest(string Carta, IReadOnlyList<ClausulaDto> Clausulas);

public sealed record CancelarProcesoRequest(string Motivo);

public sealed record ResponderOfertaRequest(bool Acepta, string? Nombre, string? Documento, string? Motivo);

public sealed record ProcesoCreadoResponse(Guid Id);

public sealed record TiposCargadosResponse(int Creados);

public sealed record TipoCreadoResponse(Guid Id);

public sealed record PlanInduccionRequest(IReadOnlyList<TareaInduccionRequest> Tareas);

public sealed record MarcarTareaRequest(bool Hecha);

public sealed record RechazarDocumentoRequest(string Motivo);

public sealed record AfiliacionRequest(string Estado, string? Entidad, string? Radicado, DateOnly? FechaEfectiva, int? ClaseRiesgo);

public sealed record AltaRequest(string Area, string CentroCosto, string Sede, string Banco, string TipoCuenta, string NumeroCuenta);

public sealed record CrearEmpleadoRequest(bool ConfirmaAlertas);

public sealed record AprobadosResponse(int Documentos);

public sealed record ExamenMedicoResponse(string Concepto, string? Restricciones, string RegistradoPor, DateTimeOffset? RegistradoEn);

public static class VinculacionEndpoints
{
    /// <summary>El lector de IA descarga el documento descifrado. Solo con la clave interna del worker.</summary>
    public static IEndpointRouteBuilder MapVinculacionInterno(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/archivos/vinculacion/{id:guid}", async (Guid id, IAlmacenDocumentos almacen, CancellationToken ct) =>
                await almacen.LeerAsync(id, ct) is { } a ? Results.File(a.Contenido, a.TipoContenido, a.Nombre) : Results.NotFound())
            .RequiereClaveInterna();
        return app;
    }

    private static async Task<(string Nombre, string Tipo, byte[] Contenido)> Leer(IFormFile archivo, CancellationToken ct)
    {
        await using var flujo = archivo.OpenReadStream();
        using var memoria = new MemoryStream();
        await flujo.CopyToAsync(memoria, ct);
        return (archivo.FileName, archivo.ContentType, memoria.ToArray());
    }

    public static IEndpointRouteBuilder MapVinculacionEndpoints(this IEndpointRouteBuilder app)
    {
        MapConsola(app.MapGroup("/vinculacion").WithTags("Vinculación").RequireAuthorization(Politicas.VerHojasDeVida));
        MapDocumentos(app.MapGroup("/vinculacion/procesos/{id:guid}/documentos").WithTags("Vinculación · documentos").RequireAuthorization(Politicas.VerHojasDeVida));
        MapTiposContrato(app.MapGroup("/vinculacion/tipos-contrato").WithTags("Vinculación · tipos de contrato").RequireAuthorization(Politicas.VerHojasDeVida));
        MapOnboarding(app.MapGroup("/vinculacion/procesos/{id:guid}/onboarding").WithTags("Vinculación · onboarding").RequireAuthorization(Politicas.VerHojasDeVida));
        MapAlta(app.MapGroup("/vinculacion/procesos/{id:guid}").WithTags("Vinculación · afiliaciones y alta").RequireAuthorization(Politicas.VerHojasDeVida));
        MapCandidato(app.MapGroup("/portal/vinculacion").WithTags("Vinculación (candidato)").AllowAnonymous().RequireRateLimiting(ClaveInterna.PoliticaPortalPublico));
        return app;
    }

    private static void MapConsola(RouteGroupBuilder g)
    {
        g.MapGet("/contratacion/sugerida", async (Guid postulacion, IQueryHandler<ObtenerContratacionSugeridaQuery, ContratacionSugeridaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerContratacionSugeridaQuery(postulacion), ct)).ToHttp())
            .WithName("ContratacionSugerida").Produces<ContratacionSugeridaDto>();

        g.MapPost("/contratacion/revisar", async (AprobarContratacionRequest b, IQueryHandler<RevisarCondicionesQuery, IReadOnlyList<AlertaDto>> h, CancellationToken ct) =>
                (await h.Handle(new RevisarCondicionesQuery(b.PostulacionId, b.Condiciones), ct)).ToHttp())
            .WithName("RevisarCondiciones").Produces<IReadOnlyList<AlertaDto>>();

        g.MapPost("/procesos", async (AprobarContratacionRequest b, ICommandHandler<AprobarContratacionCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AprobarContratacionCommand(b.PostulacionId, b.Condiciones), ct);
                return r.IsSuccess ? Results.Ok(new ProcesoCreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AprobarContratacion").Produces<ProcesoCreadoResponse>().RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapGet("/procesos", async (int? pagina, int? tamano, string? buscar, string? etapa, IQueryHandler<ListarProcesosQuery, ListaProcesosDto> h, CancellationToken ct) =>
                (await h.Handle(new ListarProcesosQuery(pagina, tamano, buscar, etapa), ct)).ToHttp())
            .WithName("ListarProcesosVinculacion").Produces<ListaProcesosDto>();

        g.MapGet("/procesos/{id:guid}", async (Guid id, IQueryHandler<ObtenerProcesoQuery, ProcesoDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerProcesoQuery(id), ct)).ToHttp())
            .WithName("ObtenerProcesoVinculacion").Produces<ProcesoDto>();

        g.MapPut("/procesos/{id:guid}/condiciones", async (Guid id, CondicionesRequest b, ICommandHandler<CambiarCondicionesCommand> h, CancellationToken ct) =>
                (await h.Handle(new CambiarCondicionesCommand(id, b), ct)).ToHttp())
            .WithName("CambiarCondiciones").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/procesos/{id:guid}/oferta/redactar", async (Guid id, ICommandHandler<RedactarOfertaCommand> h, CancellationToken ct) =>
                (await h.Handle(new RedactarOfertaCommand(id), ct)).ToHttp())
            .WithName("RedactarOferta");

        g.MapPut("/procesos/{id:guid}/oferta", async (Guid id, EditarOfertaRequest b, ICommandHandler<EditarOfertaCommand> h, CancellationToken ct) =>
                (await h.Handle(new EditarOfertaCommand(id, b.Carta, b.Clausulas ?? []), ct)).ToHttp())
            .WithName("EditarOferta");

        g.MapPost("/procesos/{id:guid}/oferta/aprobar", async (Guid id, ICommandHandler<AprobarOfertaCommand, EnlaceOfertaDto> h, CancellationToken ct) =>
                (await h.Handle(new AprobarOfertaCommand(id), ct)).ToHttp())
            .WithName("AprobarOferta").Produces<EnlaceOfertaDto>().RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/procesos/{id:guid}/oferta/enlace", async (Guid id, ICommandHandler<RenovarEnlaceOfertaCommand, EnlaceOfertaDto> h, CancellationToken ct) =>
                (await h.Handle(new RenovarEnlaceOfertaCommand(id), ct)).ToHttp())
            .WithName("RenovarEnlaceOferta").Produces<EnlaceOfertaDto>();

        g.MapPost("/procesos/{id:guid}/cancelar", async (Guid id, CancelarProcesoRequest b, ICommandHandler<CancelarProcesoCommand> h, CancellationToken ct) =>
                (await h.Handle(new CancelarProcesoCommand(id, b.Motivo), ct)).ToHttp())
            .WithName("CancelarProcesoVinculacion").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));
    }

    private static void MapAlta(RouteGroupBuilder g)
    {
        g.MapPut("/afiliaciones/{tipo}", async (Guid id, string tipo, AfiliacionRequest b, ICommandHandler<ActualizarAfiliacionCommand> h, CancellationToken ct) =>
                (await h.Handle(new ActualizarAfiliacionCommand(id, tipo, b.Estado, b.Entidad, b.Radicado, b.FechaEfectiva, b.ClaseRiesgo), ct)).ToHttp())
            .WithName("ActualizarAfiliacion");

        g.MapPut("/alta", async (Guid id, AltaRequest b, ICommandHandler<EditarAltaCommand> h, CancellationToken ct) =>
                (await h.Handle(new EditarAltaCommand(id, b.Area ?? "", b.CentroCosto ?? "", b.Sede ?? "", b.Banco ?? "", b.TipoCuenta ?? "", b.NumeroCuenta ?? ""), ct)).ToHttp())
            .WithName("EditarAlta");

        // Sin cuerpo (desde la bandeja) no confirma alertas: si las hay, la bandeja manda a revisar
        g.MapPost("/crear-empleado", async (Guid id, CrearEmpleadoRequest? b, ICommandHandler<CrearEmpleadoCommand> h, CancellationToken ct) =>
                (await h.Handle(new CrearEmpleadoCommand(id, b?.ConfirmaAlertas ?? false), ct)).ToHttp())
            .WithName("CrearEmpleado").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/alta/archivo", async (Guid id, ICommandHandler<DescargarAltaCommand, ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new DescargarAltaCommand(id), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("DescargarAlta");
    }

    /// <summary>Plan de inducción: la IA lo propone, RRHH lo ajusta y aprueba; el seguimiento lo hace el equipo.</summary>
    private static void MapOnboarding(RouteGroupBuilder g)
    {
        var aprobar = Politicas.DeAccion("AprobarBandeja");

        g.MapPost("/pedir-plan", async (Guid id, ICommandHandler<PedirPlanInduccionCommand> h, CancellationToken ct) =>
                (await h.Handle(new PedirPlanInduccionCommand(id), ct)).ToHttp())
            .WithName("PedirPlanInduccion");

        g.MapPost("/plan-base", async (Guid id, ICommandHandler<UsarPlanBaseCommand> h, CancellationToken ct) =>
                (await h.Handle(new UsarPlanBaseCommand(id), ct)).ToHttp())
            .WithName("UsarPlanBaseInduccion");

        g.MapPut("/plan", async (Guid id, PlanInduccionRequest b, ICommandHandler<EditarPlanInduccionCommand> h, CancellationToken ct) =>
                (await h.Handle(new EditarPlanInduccionCommand(id, b.Tareas), ct)).ToHttp())
            .WithName("EditarPlanInduccion");

        g.MapPost("/aprobar", async (Guid id, ICommandHandler<AprobarPlanInduccionCommand> h, CancellationToken ct) =>
                (await h.Handle(new AprobarPlanInduccionCommand(id), ct)).ToHttp())
            .WithName("AprobarPlanInduccion").RequireAuthorization(aprobar);

        g.MapPut("/tareas/{tareaId:guid}", async (Guid id, Guid tareaId, MarcarTareaRequest b, ICommandHandler<MarcarTareaInduccionCommand> h, CancellationToken ct) =>
                (await h.Handle(new MarcarTareaInduccionCommand(id, tareaId, b.Hecha), ct)).ToHttp())
            .WithName("MarcarTareaInduccion");

        g.MapPost("/cerrar", async (Guid id, ICommandHandler<CerrarOnboardingCommand> h, CancellationToken ct) =>
                (await h.Handle(new CerrarOnboardingCommand(id), ct)).ToHttp())
            .WithName("CerrarOnboarding").RequireAuthorization(aprobar);
    }

    /// <summary>Maestro de tipos de contrato: lo ve quien contrata; lo edita quien configura la estructura.</summary>
    private static void MapTiposContrato(RouteGroupBuilder g)
    {
        var configurar = Politicas.DeAccion("ConfigurarEstructura");

        g.MapGet("", async (bool? soloActivos, IQueryHandler<ListarTiposContratoQuery, IReadOnlyList<TipoContratoDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarTiposContratoQuery(soloActivos ?? false), ct)).ToHttp())
            .WithName("ListarTiposContrato").Produces<IReadOnlyList<TipoContratoDto>>();

        g.MapPost("", async (TipoContratoRequest b, ICommandHandler<CrearTipoContratoCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CrearTipoContratoCommand(b), ct);
                return r.IsSuccess ? Results.Ok(new TipoCreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CrearTipoContrato").Produces<TipoCreadoResponse>().RequireAuthorization(configurar);

        g.MapPut("/{id:guid}", async (Guid id, TipoContratoRequest b, ICommandHandler<EditarTipoContratoCommand> h, CancellationToken ct) =>
                (await h.Handle(new EditarTipoContratoCommand(id, b), ct)).ToHttp())
            .WithName("EditarTipoContrato").RequireAuthorization(configurar);

        g.MapPost("/por-defecto", async (ICommandHandler<CargarTiposPorDefectoCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CargarTiposPorDefectoCommand(), ct);
                return r.IsSuccess ? Results.Ok(new TiposCargadosResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CargarTiposContratoPorDefecto").Produces<TiposCargadosResponse>().RequireAuthorization(configurar);

        g.MapPost("/{id:guid}/plantilla", async (Guid id, IFormFile archivo, ICommandHandler<SubirPlantillaContratoCommand> h, CancellationToken ct) =>
            {
                var (nombre, tipoContenido, contenido) = await Leer(archivo, ct);
                // Algunos navegadores mandan el Word sin tipo o como octet-stream: se deduce por la extensión
                tipoContenido = Path.GetExtension(nombre).ToUpperInvariant() switch
                {
                    ".PDF" => "application/pdf",
                    ".DOCX" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    _ => tipoContenido,
                };
                return (await h.Handle(new SubirPlantillaContratoCommand(id, nombre, tipoContenido, contenido), ct)).ToHttp();
            })
            .WithName("SubirPlantillaContrato").DisableAntiforgery().RequireAuthorization(configurar);

        g.MapDelete("/{id:guid}/plantilla", async (Guid id, ICommandHandler<QuitarPlantillaContratoCommand> h, CancellationToken ct) =>
                (await h.Handle(new QuitarPlantillaContratoCommand(id), ct)).ToHttp())
            .WithName("QuitarPlantillaContrato").RequireAuthorization(configurar);

        g.MapPost("/{id:guid}/plantilla/archivo", async (Guid id, IQueryHandler<ObtenerPlantillaContratoQuery, Application.Documentos.ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new ObtenerPlantillaContratoQuery(id), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("DescargarPlantillaContrato");
    }

    private static void MapDocumentos(RouteGroupBuilder g)
    {
        g.MapPost("/{tipo}", async (Guid id, string tipo, IFormFile archivo, string? antecedentes, string? concepto, string? restricciones,
                ICommandHandler<SubirDocumentoConsolaCommand> h, CancellationToken ct) =>
            {
                var (nombre, tipoContenido, contenido) = await Leer(archivo, ct);
                return (await h.Handle(new SubirDocumentoConsolaCommand(id, tipo, nombre, tipoContenido, contenido, antecedentes, concepto, restricciones), ct)).ToHttp();
            })
            .WithName("SubirDocumentoConsola").DisableAntiforgery();

        g.MapPost("/{tipo}/aprobar", async (Guid id, string tipo, ICommandHandler<AprobarDocumentoCommand> h, CancellationToken ct) =>
                (await h.Handle(new AprobarDocumentoCommand(id, tipo), ct)).ToHttp())
            .WithName("AprobarDocumento").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/{tipo}/rechazar", async (Guid id, string tipo, RechazarDocumentoRequest b, ICommandHandler<RechazarDocumentoCommand> h, CancellationToken ct) =>
                (await h.Handle(new RechazarDocumentoCommand(id, tipo, b.Motivo), ct)).ToHttp())
            .WithName("RechazarDocumento").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/aprobar-sin-observaciones", async (Guid id, ICommandHandler<AprobarSinObservacionesCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AprobarSinObservacionesCommand(id), ct);
                return r.IsSuccess ? Results.Ok(new AprobadosResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AprobarDocumentosSinObservaciones").Produces<AprobadosResponse>().RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        // Abrir un archivo queda en la bitácora; por eso es POST
        g.MapPost("/{tipo}/archivo", async (Guid id, string tipo, ICommandHandler<AbrirDocumentoCommand, ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AbrirDocumentoCommand(id, tipo), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("AbrirDocumento");

        // Examen médico: dato sensible, con su propio permiso
        g.MapPost("/examen-medico/concepto", async (Guid id, ICommandHandler<VerExamenMedicoCommand, (ConceptoMedicoDto Concepto, ArchivoDto? Archivo)> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new VerExamenMedicoCommand(id, false), ct);
                return r.IsSuccess
                    ? Results.Ok(new ExamenMedicoResponse(r.Value.Concepto.Concepto, r.Value.Concepto.Restricciones, r.Value.Concepto.RegistradoPor, r.Value.Concepto.RegistradoEn))
                    : r.ToHttp();
            })
            .WithName("VerConceptoMedico").Produces<ExamenMedicoResponse>().RequireAuthorization(Politicas.DeAccion("VerDatosSensibles"));

        g.MapPost("/examen-medico/archivo", async (Guid id, ICommandHandler<VerExamenMedicoCommand, (ConceptoMedicoDto Concepto, ArchivoDto? Archivo)> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new VerExamenMedicoCommand(id, true), ct);
                return r.IsSuccess && r.Value.Archivo is { } a ? Results.File(a.Contenido, a.TipoContenido, a.Nombre) : r.IsSuccess ? Results.NotFound() : r.ToHttp();
            })
            .WithName("AbrirExamenMedico").RequireAuthorization(Politicas.DeAccion("VerDatosSensibles"));
    }

    private static void MapCandidato(RouteGroupBuilder g)
    {
        g.MapPost("/{token}/documentos/{tipo}", async (string token, string tipo, IFormFile archivo, ICommandHandler<SubirDocumentoCandidatoCommand> h, CancellationToken ct) =>
            {
                var (nombre, tipoContenido, contenido) = await Leer(archivo, ct);
                return (await h.Handle(new SubirDocumentoCandidatoCommand(token, tipo, nombre, tipoContenido, contenido), ct)).ToHttp();
            })
            .WithName("SubirDocumentoCandidato").DisableAntiforgery();

        g.MapGet("/{token}", async (string token, IQueryHandler<ObtenerOfertaPublicaQuery, OfertaPublicaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerOfertaPublicaQuery(token), ct)).ToHttp())
            .WithName("OfertaCandidato").Produces<OfertaPublicaDto>();

        g.MapPost("/{token}/respuesta", async (string token, ResponderOfertaRequest b, HttpContext http, ICommandHandler<ResponderOfertaCommand> h, CancellationToken ct) =>
                (await h.Handle(new ResponderOfertaCommand(token, b.Acepta, b.Nombre, b.Documento, b.Motivo, http.Connection.RemoteIpAddress?.ToString() ?? "",
                    http.Request.Headers.UserAgent.ToString()), ct)).ToHttp())
            .WithName("ResponderOferta");
    }
}
