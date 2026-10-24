using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.SharedKernel.Messaging;
using Selvana.Vinculacion.Contracts.Eventos;

namespace Selvana.Reclutamiento.Application.Ranking;

/// <summary>Vinculación abrió el proceso de contratación: la postulación de la terna queda marcada en el tablero.</summary>
internal sealed class ContratacionAprobadaConsumer(ICandidatos candidatos, IUnidadReclutamiento unidad) : IIntegrationEventConsumer<ContratacionAprobada>
{
    public async Task Handle(ContratacionAprobada e, CancellationToken cancellationToken)
    {
        var c = await candidatos.ObtenerPorPostulacionAsync(e.EmpresaId, e.PostulacionId, cancellationToken);
        if (c is not null && c.MarcarContratacion(e.PostulacionId, e.ProcesoId, e.Por, e.OccurredOn))
        {
            await unidad.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>El proceso se canceló: la postulación vuelve a ser solo de la terna.</summary>
internal sealed class ContratacionCanceladaConsumer(ICandidatos candidatos, IUnidadReclutamiento unidad) : IIntegrationEventConsumer<ContratacionCancelada>
{
    public async Task Handle(ContratacionCancelada e, CancellationToken cancellationToken)
    {
        var c = await candidatos.ObtenerPorPostulacionAsync(e.EmpresaId, e.PostulacionId, cancellationToken);
        if (c is not null && c.MarcarContratacion(e.PostulacionId, null, null, null))
        {
            await unidad.SaveChangesAsync(cancellationToken);
        }
    }
}
