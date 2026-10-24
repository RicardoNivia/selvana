using System.Text.RegularExpressions;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Candidatos;

public enum ValorRetroalimentacion
{
    Bueno,
    NoEncaja,
}

/// <summary>Lo que el reclutador opina del candidato para esta vacante: calibra el ranking de la empresa.</summary>
public sealed class RetroalimentacionPostulacion
{
    public ValorRetroalimentacion Valor { get; init; }
    public string? Nota { get; init; }
    public string Por { get; init; } = "";
    public DateTimeOffset En { get; init; }
}

public static class RetroalimentacionErrors
{
    public static readonly Error MotivoDiscriminatorio = Error.Validation("Decision.MotivoDiscriminatorio",
        "El motivo menciona una variable protegida (edad, género, estado civil, hijos, apariencia, religión, barrio o estrato). Escríbalo con lo que el cargo exige.");
}

/// <summary>
/// Variables protegidas en un texto escrito por una persona (motivo de descarte, ajuste o nota): esas decisiones no se pueden basar en ellas.
/// Es la misma regla que aplica el agente a sus razones.
/// </summary>
public static partial class VariablesProtegidas
{
    public static bool Menciona(string? texto) => !string.IsNullOrWhiteSpace(texto) && Patron().IsMatch(texto);

    [GeneratedRegex(
        @"\b(edad|años de edad|mayor de \d+|menor de \d+|muy (joven|viej[oa]|mayor)|joven|viej[oa]|g[eé]nero|sexo|masculino|femenino|" +
        @"hombres?|mujer(es)?|solter[oa]s?|casad[oa]s?|estado civil|hij[oa]s|sin hijos|embaraz\w*|madre|padre de familia|buena presencia|" +
        @"apariencia|foto(grafía)?|estatura|peso|gord[oa]|flac[oa]|religi[oó]n|cristian[oa]|cat[oó]lic[oa]|orientaci[oó]n sexual|" +
        @"barrio|estrato|raza|color de piel|venezolan[oa]s?|extranjer[oa]s?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Patron();
}
