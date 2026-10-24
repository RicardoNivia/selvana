using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Selvana.Core.Contracts.Auditoria;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Domain.Procesos;
using Selvana.Vinculacion.Infrastructure.Persistence;

namespace Selvana.Vinculacion.Infrastructure;

/// <summary>Valores legales del año; se actualizan en la configuración cada enero (`Vinculacion:SalarioMinimoCop`).</summary>
public sealed class VinculacionOptions
{
    public const string Section = "Vinculacion";

    public decimal SalarioMinimoCop { get; init; } = 1_750_905m;
    public decimal AuxilioTransporteCop { get; init; } = 249_095m;
}

internal sealed class ParametrosLegalesConfigurados(IOptions<VinculacionOptions> opciones) : IParametrosLegales
{
    public ParametrosLegales Actuales => new(opciones.Value.SalarioMinimoCop, opciones.Value.AuxilioTransporteCop);
}

internal sealed class ProcesosRepositorio(VinculacionDbContext db) : IProcesos
{
    public Task<ProcesoVinculacion?> PorIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<ProcesoVinculacion>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<ProcesoVinculacion?> PorTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Set<ProcesoVinculacion>().FirstOrDefaultAsync(p => p.Oferta.TokenHash == tokenHash, cancellationToken);

    public Task<ProcesoVinculacion?> AbiertoDePostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken) =>
        db.Set<ProcesoVinculacion>().FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.Vacante.PostulacionId == postulacionId
            && p.Etapa != EtapaVinculacion.Cancelado && p.Etapa != EtapaVinculacion.Vinculado, cancellationToken);

    public void Agregar(ProcesoVinculacion proceso) => db.Add(proceso);
}

internal sealed class EventosVinculacion(VinculacionDbContext db) : IEventosVinculacion
{
    public void Publicar(IIntegrationEvent evento) => db.AddIntegrationEvent(evento);
}

internal sealed class Bitacora(IEventosVinculacion eventos, TimeProvider reloj) : IBitacora
{
    public void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false) =>
        eventos.Publicar(new AccionAuditada(Guid.NewGuid(), reloj.GetUtcNow(), empresaId, tipoActor, actor, accion, detalle, nota,
            "Vinculacion", entidad, entidadId, datoSensible));
}

internal sealed class TokensEnlace : ITokensEnlace
{
    public (string Token, string Huella) Generar()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Huella(token));
    }

    public string Huella(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
}

internal sealed class LecturaVinculacion(VinculacionDbContext db) : ILecturaVinculacion
{
    private IQueryable<ProcesoVinculacion> Visibles(Guid empresaId, IReadOnlyCollection<Guid>? alcance)
    {
        var q = db.Set<ProcesoVinculacion>().AsNoTracking().Where(p => p.EmpresaId == empresaId);
        return alcance is null ? q : q.Where(p => p.EmpresaUsuariaId == null || alcance.Contains(p.EmpresaUsuariaId.Value));
    }

    public async Task<Pagina<FilaProcesoDto>> ProcesosAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina, string? buscar, string? etapa,
        CancellationToken cancellationToken)
    {
        var q = Visibles(empresaId, alcance);
        if (Enum.TryParse<EtapaVinculacion>(etapa, out var e))
        {
            q = q.Where(p => p.Etapa == e);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.Persona.Nombre, patron) || EF.Functions.ILike(p.Persona.Documento, patron)
                || EF.Functions.ILike(p.Codigo, patron) || EF.Functions.ILike(p.Vacante.Cargo, patron));
        }

        var total = await q.CountAsync(cancellationToken);
        var filas = await q.OrderByDescending(p => p.ActualizadoEn).Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);
        return new Pagina<FilaProcesoDto>([.. filas.Select(p => new FilaProcesoDto(p.Id, p.Codigo, p.Persona.Nombre, p.Persona.Documento, p.Vacante.Cargo,
            p.Vacante.Titulo, p.EmpresaUsuaria, ReglasContrato.Nombre(p.Condiciones.Tipo), p.Condiciones.SalarioCop, p.Condiciones.FechaIngreso,
            p.Etapa.ToString(), p.Oferta.Estado.ToString(), p.ActualizadoEn))], pagina.Numero, pagina.Tamano, total);
    }

    public async Task<IReadOnlyDictionary<string, int>> ConteoPorEtapaAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, CancellationToken cancellationToken) =>
        (await Visibles(empresaId, alcance).GroupBy(p => p.Etapa).Select(g => new { g.Key, Total = g.Count() }).ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key.ToString(), x => x.Total);
}
