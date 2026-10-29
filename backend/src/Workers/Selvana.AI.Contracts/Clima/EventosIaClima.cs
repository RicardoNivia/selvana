using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Contracts.Clima;

/// <summary>Un tema de los comentarios abiertos. <paramref name="Citas"/> son frases cortas sin nombres ni datos que identifiquen a nadie.</summary>
public sealed record TemaComentarios(string Nombre, string Sentimiento, int Cantidad, IReadOnlyList<string> Citas, string Accion);

/// <summary>C2 · Temas y sentimiento de los comentarios de una encuesta de clima. Si falló, trae <see cref="Error"/>.</summary>
public sealed record TemasClimaGenerados(
    Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid EncuestaId, IReadOnlyList<TemaComentarios> Temas, string Resumen, string Modelo, decimal CostoUsd,
    string? Error) : IIntegrationEvent;
