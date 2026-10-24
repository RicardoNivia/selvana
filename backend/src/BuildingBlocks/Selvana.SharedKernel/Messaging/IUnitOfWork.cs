namespace Selvana.SharedKernel.Messaging;

/// <summary>Unidad de trabajo del módulo; la usa el decorador de transacción de los comandos.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
