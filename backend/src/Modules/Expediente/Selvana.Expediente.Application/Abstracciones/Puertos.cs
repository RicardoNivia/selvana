using Selvana.Expediente.Domain.Empleados;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Application.Abstracciones;

/// <summary>Quién hace la petición en la consola y en qué empresa trabaja.</summary>
public interface IUsuarioActual
{
    Guid EmpresaId { get; }
    string Nombre { get; }

    /// <summary>Empresas usuarias que ve; null = todas.</summary>
    IReadOnlyCollection<Guid>? EmpresasUsuarias { get; }
}

public static class AlcanceUsuario
{
    public static bool Ve(this IUsuarioActual usuario, Guid? empresaUsuariaId) =>
        empresaUsuariaId is not { } id || usuario.EmpresasUsuarias is null || usuario.EmpresasUsuarias.Contains(id);
}

/// <summary>Unidad de trabajo del módulo (esquema "expediente").</summary>
public interface IUnidadExpediente
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IEmpleados
{
    Task<Empleado?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    Task<bool> ActivoConDocumentoAsync(Guid empresaId, string documento, CancellationToken cancellationToken);

    Task<bool> DeProcesoAsync(Guid procesoVinculacionId, CancellationToken cancellationToken);

    /// <summary>De esos documentos, los que ya son empleados activos de la empresa.</summary>
    Task<HashSet<string>> DocumentosActivosAsync(Guid empresaId, IReadOnlyList<string> documentos, CancellationToken cancellationToken);

    void Agregar(Empleado empleado);
}

public interface IEventosExpediente
{
    void Publicar(IIntegrationEvent evento);
}

/// <summary>Deja una acción en la bitácora de la empresa (Core), por el outbox del módulo y en la misma transacción.</summary>
public interface IBitacora
{
    void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false);
}

/// <summary>Archivos de la carpeta del empleado, cifrados.</summary>
public interface IAlmacenArchivos
{
    Guid Guardar(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, DateTimeOffset ahora);

    Task<(string Nombre, string TipoContenido, byte[] Contenido)?> LeerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    /// <summary>Solo para el worker de IA, con la clave interna.</summary>
    Task<(string Nombre, string TipoContenido, byte[] Contenido)?> LeerInternoAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record FilaEmpleadoDto(
    Guid Id, string Codigo, string Nombre, string Documento, string Cargo, string Area, string Sede, string? EmpresaUsuaria, string TipoContrato,
    decimal SalarioCop, DateOnly FechaIngreso, string Estado, int Alertas);

public sealed record ConteoEmpleadosDto(int Activos, int Retirados, int ConAlertas);

public interface ILecturaEmpleados
{
    Task<Pagina<FilaEmpleadoDto>> EmpleadosAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina, string? buscar, string? estado,
        bool soloConAlertas, DateOnly hoy, CancellationToken cancellationToken);

    Task<ConteoEmpleadosDto> ConteoAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, DateOnly hoy, CancellationToken cancellationToken);
}
