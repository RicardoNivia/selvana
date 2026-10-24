using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Application.Estado.Obtener;

/// <summary>Consulta de humo: confirma que el módulo Core responde de punta a punta.</summary>
public sealed record ObtenerEstadoQuery : IQuery<EstadoDto>;
