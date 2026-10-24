using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Abstracciones;

/// <summary>Quién hace la petición en la consola y en qué empresa trabaja.</summary>
public interface IUsuarioActual
{
    Guid EmpresaId { get; }
    string Nombre { get; }
    string Rol { get; }

    /// <summary>Empresas usuarias que ve; null = todas.</summary>
    IReadOnlyCollection<Guid>? EmpresasUsuarias { get; }
}

public static class AlcanceUsuario
{
    public static bool Ve(this IUsuarioActual usuario, Guid? empresaUsuariaId) =>
        empresaUsuariaId is not { } id || usuario.EmpresasUsuarias is null || usuario.EmpresasUsuarias.Contains(id);
}

/// <summary>Unidad de trabajo del módulo (esquema "vinculacion").</summary>
public interface IUnidadVinculacion
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProcesos
{
    Task<ProcesoVinculacion?> PorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProcesoVinculacion?> PorTokenAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>El proceso abierto de esa postulación, si lo hay.</summary>
    Task<ProcesoVinculacion?> AbiertoDePostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken);

    void Agregar(ProcesoVinculacion proceso);
}

public interface IEventosVinculacion
{
    void Publicar(IIntegrationEvent evento);
}

/// <summary>Deja una acción en la bitácora de la empresa (Core), por el outbox del módulo y en la misma transacción.</summary>
public interface IBitacora
{
    void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false);
}

/// <summary>Enlaces de un solo dueño: se entrega el token y se guarda solo su SHA-256.</summary>
public interface ITokensEnlace
{
    (string Token, string Huella) Generar();

    string Huella(string token);
}

/// <summary>Salario mínimo y auxilio de transporte vigentes (configuración).</summary>
public interface IParametrosLegales
{
    ParametrosLegales Actuales { get; }
}

public sealed record FilaProcesoDto(
    Guid Id, string Codigo, string Nombre, string Documento, string Cargo, string Vacante, string? EmpresaUsuaria, string TipoContrato, decimal SalarioCop,
    DateOnly FechaIngreso, string Etapa, string EstadoOferta, DateTimeOffset ActualizadoEn);

public interface ILecturaVinculacion
{
    Task<Pagina<FilaProcesoDto>> ProcesosAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina, string? buscar, string? etapa,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, int>> ConteoPorEtapaAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, CancellationToken cancellationToken);
}
