using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Core.Application.Bandeja;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Web.Common;

namespace Selvana.Core.Api;

public sealed record DescartarPropuestaRequest(string Motivo);

/// <summary>Bandeja de aprobaciones genérica (historia 203): todo lo que la IA o un proceso dejaron listo para decidir.</summary>
public static class BandejaEndpoints
{
    public static IEndpointRouteBuilder MapBandejaEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/core/bandeja").WithTags("Bandeja de aprobaciones").RequireAuthorization();

        g.MapGet("/", async (int? pagina, int? tamano, string? estado, IQueryHandler<ListarBandejaQuery, Pagina<PropuestaDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarBandejaQuery(pagina, tamano, estado), ct)).ToHttp())
            .WithName("ListarBandeja")
            .Produces<Pagina<PropuestaDto>>();

        g.MapGet("/resumen", async (IQueryHandler<ObtenerResumenBandejaQuery, ResumenBandejaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerResumenBandejaQuery(), ct)).ToHttp())
            .WithName("ResumenBandeja")
            .Produces<ResumenBandejaDto>();

        g.MapGet("/actividad", async (IQueryHandler<ObtenerActividadAgentesQuery, IReadOnlyList<ActividadAgenteDto>> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerActividadAgentesQuery(), ct)).ToHttp())
            .WithName("ActividadAgentes")
            .Produces<IReadOnlyList<ActividadAgenteDto>>();

        g.MapPost("/{id:guid}/descartar", async (Guid id, DescartarPropuestaRequest b, ICommandHandler<DescartarPropuestaCommand> h, CancellationToken ct) =>
                (await h.Handle(new DescartarPropuestaCommand(id, b.Motivo), ct)).ToHttp())
            .WithName("DescartarPropuesta")
            .RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        return app;
    }
}
