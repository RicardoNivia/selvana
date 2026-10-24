using System.Text.RegularExpressions;

namespace Selvana.AI.Agentes.RedactorVacantes;

/// <summary>Detecta requisitos que discriminan por variables protegidas (edad, género, estado civil, apariencia, dirección).</summary>
public interface IFiltroVariablesProtegidas
{
    bool EsDiscriminatorio(string texto);
}

internal sealed partial class FiltroVariablesProtegidas : IFiltroVariablesProtegidas
{
    public bool EsDiscriminatorio(string texto) => !string.IsNullOrWhiteSpace(texto) && Patron().IsMatch(texto);

    [GeneratedRegex(
        @"\b(edad|años de edad|mayor de \d+|menor de \d+|entre \d+ y \d+ años|joven|g[eé]nero|sexo|masculino|femenino|" +
        @"hombres?|mujer(es)?|solter[oa]s?|casad[oa]s?|estado civil|sin hijos|embaraz|buena presencia|foto(grafía)?|" +
        @"estatura|peso|religi[oó]n|orientaci[oó]n sexual|barrio|direcci[oó]n exacta|estrato)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Patron();
}
