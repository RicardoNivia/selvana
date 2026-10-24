namespace Selvana.Integrations.Nomina;

/// <summary>Contrato canónico: un empleado activo tal como lo entrega cualquier ERP de nómina.</summary>
public sealed record EmpleadoNomina(
    string Documento,
    string CargoCodigo,
    string CargoNombre,
    string Area,
    string CentroCostoCodigo,
    string CentroCostoNombre,
    string Sede);

public sealed record LecturaNomina(IReadOnlyList<EmpleadoNomina> Empleados, IReadOnlyList<string> Errores);

/// <summary>
/// Un adaptador por forma de conexión (ADR 0004): conector nativo, API REST, archivo programado o carga manual.
/// Agregar un ERP = escribir otro adaptador; nada más cambia.
/// </summary>
public interface IConectorNomina
{
    string Nombre { get; }

    Task<LecturaNomina> LeerAsync(Stream contenido, CancellationToken cancellationToken);
}
