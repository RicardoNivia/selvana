using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Selvana.Clima.Application.Abstracciones;
using Selvana.Clima.Application.Pulsos;
using Selvana.Clima.Contracts;
using Selvana.Clima.Domain.Encuestas;
using Selvana.Clima.Domain.Pulsos;
using Selvana.Clima.Infrastructure.Persistence;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Empresas;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Infrastructure;

internal sealed class EncuestasRepositorio(ClimaDbContext db) : IEncuestas
{
    public Task<Encuesta?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<Encuesta>().FirstOrDefaultAsync(e => e.EmpresaId == empresaId && e.Id == id, cancellationToken);

    public Task<Encuesta?> PorIdInternoAsync(Guid id, CancellationToken cancellationToken) => db.Set<Encuesta>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Encuesta?> PorTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Set<Encuesta>().FirstOrDefaultAsync(e => e.TokenHash == tokenHash && e.Estado == EstadoEncuesta.Abierta, cancellationToken);

    public void Agregar(Encuesta encuesta) => db.Add(encuesta);

    public void AgregarRespuesta(RespuestaClima respuesta) => db.Add(respuesta);

    public async Task<IReadOnlyList<RespuestaClima>> RespuestasAsync(Guid encuestaId, CancellationToken cancellationToken) =>
        await db.Set<RespuestaClima>().AsNoTracking().Where(r => r.EncuestaId == encuestaId).ToListAsync(cancellationToken);
}

internal sealed class PulsosRepositorio(ClimaDbContext db) : IPulsos
{
    public Task<Pulso?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<Pulso>().FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.Id == id, cancellationToken);

    public Task<Pulso?> PorIdInternoAsync(Guid id, CancellationToken cancellationToken) => db.Set<Pulso>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<HashSet<(Guid EmpleadoId, int Hito)>> ProgramadosAsync(Guid empresaId, CancellationToken cancellationToken) =>
        [.. (await db.Set<Pulso>().AsNoTracking().Where(p => p.EmpresaId == empresaId).Select(p => new { p.EmpleadoId, p.Hito }).ToListAsync(cancellationToken))
            .Select(p => (p.EmpleadoId, p.Hito))];

    public async Task<IReadOnlyList<Pulso>> UltimosRespondidosAsync(Guid empresaId, CancellationToken cancellationToken) =>
        [.. (await db.Set<Pulso>().AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Respuesta != null).ToListAsync(cancellationToken))
            .GroupBy(p => p.EmpleadoId).Select(g => g.OrderByDescending(p => p.Hito).First())];

    public void Agregar(Pulso pulso) => db.Add(pulso);
}

internal sealed class LecturaClima(ClimaDbContext db) : ILecturaClima
{
    public async Task<Pagina<FilaEncuestaDto>> EncuestasAsync(Guid empresaId, PaginaSolicitada pagina, CancellationToken cancellationToken)
    {
        var q = db.Set<Encuesta>().AsNoTracking().Where(e => e.EmpresaId == empresaId);
        var total = await q.CountAsync(cancellationToken);
        var encuestas = await q.OrderByDescending(e => e.CreadaEn).Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);
        var ids = encuestas.Select(e => e.Id).ToList();
        var respuestas = await db.Set<RespuestaClima>().AsNoTracking().Where(r => ids.Contains(r.EncuestaId)).GroupBy(r => r.EncuestaId)
            .Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, cancellationToken);
        return new Pagina<FilaEncuestaDto>([.. encuestas.Select(e => new FilaEncuestaDto(e.Id, e.Titulo, e.Estado.ToString(), e.Preguntas.Count,
            respuestas.GetValueOrDefault(e.Id), e.AbiertaEn, e.CerradaEn))], pagina.Numero, pagina.Tamano, total);
    }

    public async Task<Pagina<FilaPulsoDto>> PulsosAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina, string? filtro,
        CancellationToken cancellationToken)
    {
        var q = db.Set<Pulso>().AsNoTracking().Where(p => p.EmpresaId == empresaId && (alcance == null || p.EmpresaUsuariaId == null
            || alcance.Contains(p.EmpresaUsuariaId.Value)));
        q = filtro switch
        {
            "alertas" => q.Where(p => p.Alerta),
            "sinSeguimiento" => q.Where(p => p.Alerta && p.Seguimiento == null),
            "pendientes" => q.Where(p => p.Respuesta == null),
            "respondidos" => q.Where(p => p.Respuesta != null),
            _ => q,
        };
        var total = await q.CountAsync(cancellationToken);
        var filas = await q.OrderByDescending(p => p.Alerta).ThenByDescending(p => p.PendienteDesde).Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);
        return new Pagina<FilaPulsoDto>([.. filas.Select(p => new FilaPulsoDto(p.Id, p.EmpleadoId, p.Nombre, p.Cargo, p.Area, p.Hito, p.PendienteDesde, p.Respuesta != null,
            p.Promedio, p.Respuesta?.Recomendaria, p.Respuesta?.Comentario, p.Alerta, p.Seguimiento?.Nota, p.Seguimiento?.Por))], pagina.Numero, pagina.Tamano, total);
    }

    public async Task<Encuesta?> UltimaConRespuestasAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var conRespuestas = db.Set<RespuestaClima>().Where(r => r.EmpresaId == empresaId).Select(r => r.EncuestaId);
        return await db.Set<Encuesta>().AsNoTracking().Where(e => e.EmpresaId == empresaId && conRespuestas.Contains(e.Id))
            .OrderByDescending(e => e.AbiertaEn).FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>Token aleatorio de 32 bytes para el enlace anónimo; se guarda solo su huella SHA-256.</summary>
internal sealed class TokensEncuesta : ITokensEncuesta
{
    public (string Token, string Huella) Generar()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Huella(token));
    }

    public string Huella(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
}

internal sealed class EventosClima(ClimaDbContext db) : IEventosClima
{
    public void Publicar(IIntegrationEvent evento) => db.AddIntegrationEvent(evento);
}

internal sealed class Bitacora(IEventosClima eventos, TimeProvider reloj) : IBitacora
{
    public void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false) =>
        eventos.Publicar(new AccionAuditada(Guid.NewGuid(), reloj.GetUtcNow(), empresaId, tipoActor, actor, accion, detalle, nota, "Clima", entidad, entidadId, datoSensible));
}

/// <summary>C3 · Los pulsos del empleado en su portal, por cédula y correo.</summary>
internal sealed class PulsosDeLaPersona(ClimaDbContext db, ICommandHandler<ResponderPulsoCommand> responder) : IPulsosDeLaPersona
{
    public async Task<IReadOnlyList<PulsoDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken)
    {
        var patron = correo.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        return await db.Set<Pulso>().AsNoTracking().Where(p => p.Documento == documento && EF.Functions.ILike(p.Correo, patron, "\\"))
            .OrderByDescending(p => p.Hito).Select(p => new PulsoDeLaPersona(p.Id, p.Hito, p.PendienteDesde, p.Respuesta != null)).ToListAsync(cancellationToken);
    }

    public Task<Result> ResponderAsync(Guid pulsoId, string documento, string correo, RespuestaPulsoDeLaPersona r, CancellationToken cancellationToken) =>
        responder.Handle(new ResponderPulsoCommand(pulsoId, documento, correo, r.Satisfaccion, r.Claridad, r.Apoyo, r.Recomendaria, r.Comentario), cancellationToken);
}

/// <summary>Cada seis horas programa los pulsos de 30, 60 y 90 días en todas las empresas.</summary>
internal sealed partial class ProgramadorPulsos(IServiceScopeFactory scopes, ILogger<ProgramadorPulsos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var empresas = await scope.ServiceProvider.GetRequiredService<IDirectorioEmpresas>().TodasAsync(stoppingToken);
                var programar = scope.ServiceProvider.GetRequiredService<ICommandHandler<ProgramarPulsosCommand, int>>();
                foreach (var (id, _) in empresas)
                {
                    await programar.Handle(new ProgramarPulsosCommand(id), stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                FallaProgramacion(logger, ex);
            }
        }
        while (await reloj.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudieron programar los pulsos de ingreso; se intenta en la próxima vuelta.")]
    private static partial void FallaProgramacion(ILogger logger, Exception ex);
}
