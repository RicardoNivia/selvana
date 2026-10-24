using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Contracts.Estructura;

/// <summary>Un cargo tal como vino de la nómina, con cuántos empleados lo tienen.</summary>
public sealed record CargoLeido(string Codigo, string Nombre, string Area, int Empleados);

/// <summary>Se leyó la nómina de una empresa; la IA debe proponer qué nombres de cargo son el mismo cargo.</summary>
public sealed record ImportacionNominaLeida(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ImportacionId, IReadOnlyList<CargoLeido> Cargos) : IIntegrationEvent;

public sealed record CargoParaPerfil(Guid CargoId, string Nombre, string Area, int Empleados, IReadOnlyList<string> NombresEnNomina);

/// <summary>Hay cargos sin perfil; la IA debe redactar un borrador de perfil para cada uno.</summary>
public sealed record PerfilesCargoSolicitados(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, IReadOnlyList<CargoParaPerfil> Cargos) : IIntegrationEvent;
