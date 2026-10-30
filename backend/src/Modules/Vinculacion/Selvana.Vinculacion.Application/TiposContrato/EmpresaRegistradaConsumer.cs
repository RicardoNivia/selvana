using Selvana.Core.Contracts.Empresas;
using Selvana.SharedKernel.Messaging;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Domain.TiposContrato;

namespace Selvana.Vinculacion.Application.TiposContrato;

/// <summary>Empresa nueva por autoservicio: arranca con los tipos de contrato por defecto (idempotente).</summary>
internal sealed class EmpresaRegistradaConsumer(ITiposContrato tipos, IUnidadVinculacion unidad, TimeProvider reloj) : IIntegrationEventConsumer<EmpresaRegistrada>
{
    public async Task Handle(EmpresaRegistrada e, CancellationToken cancellationToken)
    {
        if (await tipos.ContarAsync(e.EmpresaId, cancellationToken) > 0)
        {
            return;
        }

        foreach (var t in TipoContratoEmpresa.PorDefectoPara(e.EmpresaId, e.EsTemporal, reloj.GetUtcNow()))
        {
            tipos.Agregar(t);
        }

        await unidad.SaveChangesAsync(cancellationToken);
    }
}
