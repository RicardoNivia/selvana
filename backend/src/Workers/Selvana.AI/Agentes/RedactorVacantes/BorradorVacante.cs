using Selvana.Reclutamiento.Contracts;

namespace Selvana.AI.Agentes.RedactorVacantes;

/// <summary>Frase del líder y, si eligió un cargo del catálogo, su perfil: el borrador parte de él en vez de redactarse desde cero.</summary>
public sealed record SolicitudBorradorVacante(Guid EmpresaId, string Frase, PerfilCargoVacante? Perfil = null);

/// <summary>Lo que el modelo devuelve; se valida antes de mostrarse como sugerencia.</summary>
public sealed record BorradorVacante(
    string Titulo,
    string Cargo,
    string Ciudad,
    int Cupos,
    string Jornada,
    string Descripcion,
    IReadOnlyList<string> Requisitos,
    IReadOnlyList<string> Competencias,
    IReadOnlyList<string> PreguntasFiltro,
    RangoSalarialSugerido RangoSalarial,
    IReadOnlyList<RazonSugerencia> Razones,
    IReadOnlyList<string> Supuestos,
    IReadOnlyList<string>? Aclaraciones = null);

public sealed record RangoSalarialSugerido(decimal MinimoCop, decimal MaximoCop, string Justificacion);

/// <summary>Una razón de la sugerencia con la evidencia de la que sale (cita de la frase o supuesto declarado).</summary>
public sealed record RazonSugerencia(string Texto, string Evidencia);

/// <summary>Borrador listo para la bandeja: la sugerencia, lo que se retiró por sesgo y qué modelo la produjo.</summary>
public sealed record ResultadoBorradorVacante(
    BorradorVacante Borrador,
    IReadOnlyList<string> RequisitosRetiradosPorSesgo,
    string Proveedor,
    string Modelo,
    decimal CostoUsd);
