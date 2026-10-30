namespace Selvana.Reclutamiento.Contracts;

/// <summary>Cuántas vacantes (todas y publicadas) y postulaciones tiene la empresa: lo usa el recorrido guiado de Core.</summary>
public sealed record ResumenReclutamiento(int Vacantes, int Publicadas, int Postulaciones);

public interface IResumenReclutamiento
{
    Task<ResumenReclutamiento> ResumenAsync(Guid empresaId, CancellationToken cancellationToken);
}
