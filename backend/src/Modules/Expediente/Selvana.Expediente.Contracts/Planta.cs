namespace Selvana.Expediente.Contracts;

/// <summary>Lo que otros módulos (Desempeño, Clima) pueden saber de un empleado activo. Sin salario ni datos sensibles.</summary>
public sealed record EmpleadoActivo(
    Guid EmpleadoId, string Codigo, string Nombre, string Documento, string Correo, string Cargo, string Area, string Sede, Guid? EmpresaUsuariaId,
    string? EmpresaUsuaria, DateOnly FechaIngreso, DateOnly? FechaFinContrato);

/// <summary>La planta activa de una empresa, para armar evaluaciones, pulsos y señales de rotación.</summary>
public interface IPlantaActiva
{
    Task<IReadOnlyList<EmpleadoActivo>> ListarAsync(Guid empresaId, CancellationToken cancellationToken);
}
