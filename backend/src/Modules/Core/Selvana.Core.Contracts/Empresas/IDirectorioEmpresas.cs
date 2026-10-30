using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Contracts.Empresas;

public sealed record EmpresaUsuariaResumen(Guid Id, string Nombre, string Ciudad, bool Activa);

public sealed record EmpresaResumen(Guid Id, string Nombre, bool EsTemporal, IReadOnlyList<EmpresaUsuariaResumen> EmpresasUsuarias);

/// <summary>Lo único que otros módulos pueden saber de una empresa. Se consulta por este contrato, nunca por las tablas de Core.</summary>
public interface IDirectorioEmpresas
{
    Task<EmpresaResumen?> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken);

    /// <summary>Todas las empresas (id y si es de servicios temporales), para sembrar los datos maestros de otros módulos al arrancar.</summary>
    Task<IReadOnlyList<(Guid Id, bool EsTemporal)>> TodasAsync(CancellationToken cancellationToken);
}

/// <summary>Una empresa se registró sola (autoservicio): los módulos siembran sus datos maestros por defecto (por ejemplo, tipos de contrato).</summary>
public sealed record EmpresaRegistrada(Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, string Nombre, bool EsTemporal) : IIntegrationEvent;
