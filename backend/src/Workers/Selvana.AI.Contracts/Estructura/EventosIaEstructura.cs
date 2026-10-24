using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Contracts.Estructura;

/// <summary>Una razón de una sugerencia de IA: signo (+/−), texto y de dónde sale.</summary>
public sealed record RazonIa(string Signo, string Texto, string Fuente);

/// <summary>Nombres de cargo de la nómina que la IA considera el mismo cargo.</summary>
public sealed record UnionCargosPropuesta(string NombreFinal, IReadOnlyList<string> Codigos, string Porque);

/// <summary>Respuesta del agente organizador a <c>ImportacionNominaLeida</c>. Si falló, trae <see cref="Error"/>.</summary>
public sealed record UnionesCargosPropuestas(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid ImportacionId,
    IReadOnlyList<UnionCargosPropuesta> Uniones, string Modelo, string? Error) : IIntegrationEvent;

/// <summary>Borrador de perfil de cargo redactado por la IA; queda pendiente de aprobación humana.</summary>
public sealed record PerfilCargoRedactado(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid CargoId,
    string Mision, IReadOnlyList<string> Funciones, IReadOnlyList<string> Requisitos, string RiesgoArl,
    IReadOnlyList<RazonIa> Razones, string Modelo) : IIntegrationEvent;
