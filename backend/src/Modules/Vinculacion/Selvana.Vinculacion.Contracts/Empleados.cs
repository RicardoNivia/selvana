using Selvana.SharedKernel.Messaging;

namespace Selvana.Vinculacion.Contracts.Empleados;

/// <summary>
/// Una persona creó el empleado al terminar la vinculación: el módulo Empleados (expediente) lo recibe con su contrato y afiliaciones.
/// Lleva datos personales; viaja solo dentro de la API.
/// </summary>
public sealed record EmpleadoVinculado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ProcesoId, Guid CandidatoId, Guid? EmpresaUsuariaId, string Documento, string Nombre, string Correo,
    string Celular, string Ciudad, string Cargo, string Area, string CentroCosto, string Sede, string TipoContrato, decimal SalarioCop, DateOnly FechaIngreso,
    DateOnly? FechaFin, string Eps, string FondoPension, string Arl, int? ClaseRiesgoArl, string CajaCompensacion, string CreadoPor, string? EmpresaUsuaria = null)
    : IIntegrationEvent;
