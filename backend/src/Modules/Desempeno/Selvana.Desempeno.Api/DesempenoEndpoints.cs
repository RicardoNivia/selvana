using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Desempeno.Application.Abstracciones;
using Selvana.Desempeno.Application.Ciclos;
using Selvana.Desempeno.Application.Evaluaciones;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Web.Common;

namespace Selvana.Desempeno.Api;

public sealed record CrearCicloRequest(string Nombre, string Tipo, string Alcance, DateOnly Desde, DateOnly Hasta, int PesoObjetivos);

public sealed record CargoRequest(string? Cargo);

public sealed record FormularioRequest(string Cargo, IReadOnlyList<CompetenciaDto> Competencias, IReadOnlyList<ObjetivoBaseDto> Objetivos);

public sealed record ObjetivosRequest(IReadOnlyList<ObjetivoRequest> Objetivos);

public sealed record AvanceRequest(Guid ObjetivoId, int Avance, string? Evidencia);

public sealed record CalificacionesRequest(IReadOnlyDictionary<string, int> Calificaciones);

public sealed record CalificarRequest(IReadOnlyDictionary<string, int> Calificaciones, string Comentario, string Potencial);

public sealed record CalibrarRequest(int Caja, string Motivo);

public sealed record PlanRequest(IReadOnlyList<AccionPlanRequest> Acciones);

public sealed record CreadoResponse(Guid Id);

public sealed record CantidadResponse(int Cantidad);

public static class DesempenoEndpoints
{
    public static IEndpointRouteBuilder MapDesempenoEndpoints(this IEndpointRouteBuilder app)
    {
        var c = app.MapGroup("/desempeno/ciclos").WithTags("Desempeño · ciclos").RequireAuthorization(Politicas.DeAccion("GestionarDesempeno"));

        c.MapGet("", async (int? pagina, int? tamano, IQueryHandler<ListarCiclosQuery, Pagina<FilaCicloDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarCiclosQuery(pagina, tamano), ct)).ToHttp())
            .WithName("ListarCiclos").Produces<Pagina<FilaCicloDto>>();

        c.MapGet("/{id:guid}", async (Guid id, IQueryHandler<ObtenerCicloQuery, CicloDto> h, CancellationToken ct) => (await h.Handle(new ObtenerCicloQuery(id), ct)).ToHttp())
            .WithName("ObtenerCiclo").Produces<CicloDto>();

        c.MapPost("", async (CrearCicloRequest b, ICommandHandler<CrearCicloCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CrearCicloCommand(b.Nombre, b.Tipo, b.Alcance, b.Desde, b.Hasta, b.PesoObjetivos), ct);
                return r.IsSuccess ? Results.Ok(new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CrearCiclo").Produces<CreadoResponse>();

        c.MapPost("/{id:guid}/formularios/ia", async (Guid id, CargoRequest b, ICommandHandler<PedirFormularioIaCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new PedirFormularioIaCommand(id, b.Cargo), ct);
                return r.IsSuccess ? Results.Ok(new CantidadResponse(r.Value)) : r.ToHttp();
            })
            .WithName("PedirFormularioIa").Produces<CantidadResponse>();

        c.MapPost("/{id:guid}/formularios/plantilla", async (Guid id, CargoRequest b, ICommandHandler<UsarPlantillaBaseCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new UsarPlantillaBaseCommand(id, b.Cargo), ct);
                return r.IsSuccess ? Results.Ok(new CantidadResponse(r.Value)) : r.ToHttp();
            })
            .WithName("UsarPlantillaBase").Produces<CantidadResponse>();

        c.MapPut("/{id:guid}/formularios", async (Guid id, FormularioRequest b, ICommandHandler<GuardarFormularioCommand> h, CancellationToken ct) =>
                (await h.Handle(new GuardarFormularioCommand(id, b.Cargo, b.Competencias ?? [], b.Objetivos ?? []), ct)).ToHttp())
            .WithName("GuardarFormulario");

        c.MapPost("/{id:guid}/formularios/aprobar", async (Guid id, CargoRequest b, ICommandHandler<AprobarFormularioCommand> h, CancellationToken ct) =>
                (await h.Handle(new AprobarFormularioCommand(id, b.Cargo ?? ""), ct)).ToHttp())
            .WithName("AprobarFormulario");

        c.MapPost("/{id:guid}/abrir", async (Guid id, ICommandHandler<AbrirCicloCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AbrirCicloCommand(id), ct);
                return r.IsSuccess ? Results.Ok(new CantidadResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AbrirCiclo").Produces<CantidadResponse>();

        c.MapPost("/{id:guid}/calibracion", async (Guid id, ICommandHandler<PasarACalibracionCommand> h, CancellationToken ct) =>
                (await h.Handle(new PasarACalibracionCommand(id), ct)).ToHttp())
            .WithName("PasarACalibracion");

        c.MapPost("/{id:guid}/cerrar", async (Guid id, ICommandHandler<CerrarCicloCommand> h, CancellationToken ct) =>
                (await h.Handle(new CerrarCicloCommand(id), ct)).ToHttp())
            .WithName("CerrarCiclo");

        c.MapGet("/{id:guid}/evaluaciones", async (Guid id, int? pagina, int? tamano, string? buscar, string? estado,
                IQueryHandler<ListarEvaluacionesQuery, Pagina<FilaEvaluacionDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarEvaluacionesQuery(id, pagina, tamano, buscar, estado), ct)).ToHttp())
            .WithName("ListarEvaluaciones").Produces<Pagina<FilaEvaluacionDto>>();

        var e = app.MapGroup("/desempeno/evaluaciones").WithTags("Desempeño · evaluaciones").RequireAuthorization(Politicas.DeAccion("GestionarDesempeno"));

        e.MapGet("/{id:guid}", async (Guid id, IQueryHandler<ObtenerEvaluacionQuery, EvaluacionDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerEvaluacionQuery(id), ct)).ToHttp())
            .WithName("ObtenerEvaluacion").Produces<EvaluacionDto>();

        e.MapPut("/{id:guid}/objetivos", async (Guid id, ObjetivosRequest b, ICommandHandler<DefinirObjetivosCommand> h, CancellationToken ct) =>
                (await h.Handle(new DefinirObjetivosCommand(id, b.Objetivos ?? []), ct)).ToHttp())
            .WithName("DefinirObjetivos");

        e.MapPost("/{id:guid}/avance", async (Guid id, AvanceRequest b, ICommandHandler<RegistrarAvanceCommand> h, CancellationToken ct) =>
                (await h.Handle(new RegistrarAvanceCommand(id, b.ObjetivoId, b.Avance, b.Evidencia), ct)).ToHttp())
            .WithName("RegistrarAvance");

        e.MapPost("/{id:guid}/pares", async (Guid id, CalificacionesRequest b, ICommandHandler<RegistrarParCommand> h, CancellationToken ct) =>
                (await h.Handle(new RegistrarParCommand(id, b.Calificaciones ?? new Dictionary<string, int>()), ct)).ToHttp())
            .WithName("RegistrarPar");

        e.MapPost("/{id:guid}/borrador", async (Guid id, ICommandHandler<PedirBorradorCommand> h, CancellationToken ct) =>
                (await h.Handle(new PedirBorradorCommand(id), ct)).ToHttp())
            .WithName("PedirBorradorCopiloto");

        e.MapPost("/{id:guid}/borrador/usar", async (Guid id, ICommandHandler<UsarBorradorCommand> h, CancellationToken ct) =>
                (await h.Handle(new UsarBorradorCommand(id), ct)).ToHttp())
            .WithName("UsarBorradorCopiloto");

        e.MapPost("/{id:guid}/calificar", async (Guid id, CalificarRequest b, ICommandHandler<CalificarEvaluacionCommand> h, CancellationToken ct) =>
                (await h.Handle(new CalificarEvaluacionCommand(id, b.Calificaciones ?? new Dictionary<string, int>(), b.Comentario ?? "", b.Potencial ?? ""), ct)).ToHttp())
            .WithName("CalificarEvaluacion");

        e.MapPost("/{id:guid}/reabrir", async (Guid id, ICommandHandler<ReabrirEvaluacionCommand> h, CancellationToken ct) =>
                (await h.Handle(new ReabrirEvaluacionCommand(id), ct)).ToHttp())
            .WithName("ReabrirEvaluacion");

        e.MapPost("/{id:guid}/calibrar", async (Guid id, CalibrarRequest b, ICommandHandler<CalibrarEvaluacionCommand> h, CancellationToken ct) =>
                (await h.Handle(new CalibrarEvaluacionCommand(id, b.Caja, b.Motivo ?? ""), ct)).ToHttp())
            .WithName("CalibrarEvaluacion");

        e.MapPut("/{id:guid}/plan", async (Guid id, PlanRequest b, ICommandHandler<DefinirPlanCommand> h, CancellationToken ct) =>
                (await h.Handle(new DefinirPlanCommand(id, b.Acciones ?? []), ct)).ToHttp())
            .WithName("DefinirPlanDesarrollo");

        return app;
    }
}
