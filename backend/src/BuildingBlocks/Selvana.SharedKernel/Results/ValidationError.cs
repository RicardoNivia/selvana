namespace Selvana.SharedKernel.Results;

/// <summary>Agrupa los errores de validación de un comando o consulta.</summary>
public sealed record ValidationError(IReadOnlyList<Error> Errors)
    : Error("Validacion", "Uno o más datos no son válidos.", ErrorType.Validation);
