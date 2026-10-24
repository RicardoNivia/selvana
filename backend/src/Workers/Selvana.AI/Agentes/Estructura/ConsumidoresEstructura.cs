using Microsoft.Extensions.Logging;
using Selvana.AI.Contracts.Estructura;
using Selvana.AI.Persistence;
using Selvana.Core.Contracts.Estructura;
using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Agentes.Estructura;

/// <summary>Core leyó una nómina: el organizador propone uniones de cargos y responde por el outbox del worker.</summary>
internal sealed class ImportacionNominaLeidaConsumer(IOrganizadorCargos organizador, IaDbContext db, TimeProvider reloj)
    : IIntegrationEventConsumer<ImportacionNominaLeida>
{
    public async Task Handle(ImportacionNominaLeida evento, CancellationToken cancellationToken)
    {
        var resultado = await organizador.ProponerUnionesAsync(evento.EmpresaId, evento.Cargos, cancellationToken);

        db.AddIntegrationEvent(resultado.IsSuccess
            ? new UnionesCargosPropuestas(Guid.CreateVersion7(), reloj.GetUtcNow(), evento.EmpresaId, evento.ImportacionId,
                [.. resultado.Value.Uniones.Select(u => new UnionCargosPropuesta(u.NombreFinal, u.Codigos, u.Porque))], resultado.Value.Modelo, null)
            : new UnionesCargosPropuestas(Guid.CreateVersion7(), reloj.GetUtcNow(), evento.EmpresaId, evento.ImportacionId,
                [], "", resultado.Error.Description));

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Hay cargos sin perfil: se redacta uno por uno y cada borrador se envía apenas está listo.</summary>
internal sealed partial class PerfilesCargoSolicitadosConsumer(
    IRedactorPerfilCargo redactor, IaDbContext db, TimeProvider reloj, ILogger<PerfilesCargoSolicitadosConsumer> logger)
    : IIntegrationEventConsumer<PerfilesCargoSolicitados>
{
    public async Task Handle(PerfilesCargoSolicitados evento, CancellationToken cancellationToken)
    {
        foreach (var cargo in evento.Cargos.OrderByDescending(c => c.Empleados))
        {
            var resultado = await redactor.RedactarAsync(evento.EmpresaId, cargo, cancellationToken);
            if (resultado.IsFailure)
            {
                // Un cargo sin borrador no bloquea a los demás: queda "Sin perfil" y se puede pedir otra vez.
                PerfilFallo(logger, cargo.Nombre, resultado.Error.Description);
                continue;
            }

            var p = resultado.Value.Perfil;
            db.AddIntegrationEvent(new PerfilCargoRedactado(Guid.CreateVersion7(), reloj.GetUtcNow(), evento.EmpresaId, cargo.CargoId,
                p.Mision, p.Funciones, p.Requisitos, p.RiesgoArl,
                [.. p.Razones.Select(r => new RazonIa(r.Signo, r.Texto, r.Fuente))], resultado.Value.Modelo));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se redactó el perfil de {Cargo}: {Motivo}")]
    private static partial void PerfilFallo(ILogger logger, string cargo, string motivo);
}
