using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Contracts;

/// <summary>Un documento de su carpeta tal como lo ve el empleado. <paramref name="Vigencia"/>: Vigente, PorVencer, Vencido o SinVencimiento.</summary>
public sealed record DocumentoDelEmpleo(Guid Id, string Tipo, string Nombre, DateOnly? VenceEn, string Vigencia, bool EnRevision);

/// <summary>Lo que el empleado propuso cambiar en sus datos y RRHH aún no ha decidido.</summary>
public sealed record CambioPendienteDelEmpleo(IReadOnlyList<string> Cambios, DateTimeOffset En);

/// <summary>
/// P5 · Su empleo tal como lo ve la persona en su portal: cargo, contrato, afiliaciones, su carpeta y sus avisos.
/// Sin notas internas de RRHH ni el historial de quién registró cada novedad.
/// </summary>
public sealed record EmpleoDeLaPersona(
    Guid EmpleadoId, string Empresa, string Codigo, string Cargo, string Area, string Sede, string? EmpresaUsuaria, string TipoContrato, decimal SalarioCop,
    DateOnly FechaIngreso, DateOnly? FechaFinContrato, string Estado, string Celular, string Ciudad, string Eps, string FondoPension, string Arl,
    string CajaCompensacion, IReadOnlyList<DocumentoDelEmpleo> Documentos, IReadOnlyList<string> Avisos, bool ActualizacionPedida,
    DateTimeOffset? DatosConfirmadosEn, CambioPendienteDelEmpleo? CambioPendiente);

/// <summary>Los datos que el empleado puede proponer cambiar; RRHH los aplica en el expediente.</summary>
public sealed record DatosPropuestosPorElEmpleado(string Celular, string Ciudad, string Eps, string FondoPension, string CajaCompensacion);

/// <summary>Un soporte que el empleado sube desde su portal (diploma, curso, certificado de afiliación…): la IA lo lee y RRHH decide.</summary>
public sealed record SoporteDelEmpleado(string Tipo, string Nombre, DateOnly? VenceEn, string NombreArchivo, string TipoContenido, byte[] Contenido);

/// <summary>Empleos de la persona en las empresas que usan Selvana, buscados por su cédula y su correo (los dos deben coincidir).</summary>
public interface IEmpleoDeLaPersona
{
    Task<IReadOnlyList<EmpleoDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken);

    /// <summary>Propone cambios a sus datos; queda en la bandeja de RRHH. También responde la campaña de actualización.</summary>
    Task<Result> ProponerDatosAsync(Guid empleadoId, string documento, string correo, DatosPropuestosPorElEmpleado datos, CancellationToken cancellationToken);

    /// <summary>Responde la campaña de actualización diciendo que sus datos siguen iguales.</summary>
    Task<Result> ConfirmarDatosAsync(Guid empleadoId, string documento, string correo, CancellationToken cancellationToken);

    Task<Result> EntregarSoporteAsync(Guid empleadoId, string documento, string correo, SoporteDelEmpleado soporte, CancellationToken cancellationToken);

    /// <summary>Descarga un documento de su propia carpeta.</summary>
    Task<(string Nombre, string TipoContenido, byte[] Contenido)?> DescargarAsync(Guid empleadoId, string documento, string correo, Guid documentoId,
        CancellationToken cancellationToken);
}
