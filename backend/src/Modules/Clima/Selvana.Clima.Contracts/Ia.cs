using Selvana.SharedKernel.Messaging;

namespace Selvana.Clima.Contracts.Ia;

/// <summary>C2 · Que la IA agrupe por temas los comentarios abiertos de una encuesta. Van sin área, sede ni fecha: solo el texto.</summary>
public sealed record ComentariosClimaSolicitados(Guid Id, DateTimeOffset OccurredOn, Guid EmpresaId, Guid EncuestaId, IReadOnlyList<string> Comentarios)
    : IIntegrationEvent;
