using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Procesos;

public sealed record ListaProcesosDto(Pagina<FilaProcesoDto> Procesos, IReadOnlyDictionary<string, int> PorEtapa);

public sealed record ListarProcesosQuery(int? Pagina, int? Tamano, string? Buscar, string? Etapa) : IQuery<ListaProcesosDto>;

public sealed record ObtenerProcesoQuery(Guid Id) : IQuery<ProcesoDto>;

internal sealed class ListarProcesosHandler(IUsuarioActual usuario, ILecturaVinculacion lectura) : IQueryHandler<ListarProcesosQuery, ListaProcesosDto>
{
    public async Task<Result<ListaProcesosDto>> Handle(ListarProcesosQuery q, CancellationToken ct)
    {
        var etapa = Enum.TryParse<EtapaVinculacion>(q.Etapa, ignoreCase: true, out var e) ? e.ToString() : null;
        var pagina = await lectura.ProcesosAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, new PaginaSolicitada(q.Pagina, q.Tamano), q.Buscar, etapa, ct);
        return new ListaProcesosDto(pagina, await lectura.ConteoPorEtapaAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, ct));
    }
}

internal sealed class ObtenerProcesoHandler(IUsuarioActual usuario, IProcesos procesos, IParametrosLegales parametros, TimeProvider reloj)
    : IQueryHandler<ObtenerProcesoQuery, ProcesoDto>
{
    public async Task<Result<ProcesoDto>> Handle(ObtenerProcesoQuery q, CancellationToken ct)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, q.Id, ct);
        return r.IsFailure ? r.Error : Mapeo.Dto(r.Value, parametros.Actuales, DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime));
    }
}
