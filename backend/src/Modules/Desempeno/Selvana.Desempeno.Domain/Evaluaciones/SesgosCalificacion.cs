using System.Globalization;

namespace Selvana.Desempeno.Domain.Evaluaciones;

public sealed record AlertaSesgo(string Tipo, string Texto, string Evidencia);

/// <summary>
/// D3 · Señales de sesgo en las calificaciones del ciclo, por reglas y con su evidencia: evaluadores que califican a todos igual o todos
/// alto, y áreas muy por encima o por debajo del resto. Son alertas para el comité, no correcciones automáticas.
/// </summary>
public static class SesgosCalificacion
{
    public const int MinimoPorGrupo = 3;

    public static IReadOnlyList<AlertaSesgo> Revisar(IReadOnlyList<(string Evaluador, string Area, decimal Puntaje)> calificadas)
    {
        var alertas = new List<AlertaSesgo>();
        if (calificadas.Count < MinimoPorGrupo)
        {
            return alertas;
        }

        static string N(decimal x) => x.ToString("0.0", CultureInfo.GetCultureInfo("es-CO"));
        var promedio = calificadas.Average(c => c.Puntaje);

        foreach (var g in calificadas.GroupBy(c => c.Evaluador).Where(g => g.Count() >= MinimoPorGrupo))
        {
            var notas = g.Select(c => c.Puntaje).ToList();
            var media = notas.Average();
            var desviacion = (decimal)Math.Sqrt(notas.Average(n => Math.Pow((double)(n - media), 2)));
            if (desviacion < 0.25m)
            {
                alertas.Add(new("Todo igual", $"{g.Key} calificó a {notas.Count} personas casi igual (promedio {N(media)}). Revise si diferenció el desempeño de cada una.",
                    $"Desviación {N(desviacion)} · {notas.Count} evaluaciones"));
            }
            else if (media >= 4.5m)
            {
                alertas.Add(new("Todo alto", $"{g.Key} califica en promedio {N(media)}: muy por encima del resto ({N(promedio)}).",
                    $"{notas.Count} evaluaciones"));
            }
            else if (media is >= 2.8m and <= 3.2m && notas.All(n => n is >= 2.6m and <= 3.4m))
            {
                alertas.Add(new("Todo medio", $"{g.Key} dejó a todas sus {notas.Count} personas en el centro de la escala. Puede estar evitando diferenciar.",
                    $"Puntajes entre 2,6 y 3,4"));
            }
        }

        foreach (var g in calificadas.GroupBy(c => c.Area).Where(g => g.Count() >= MinimoPorGrupo && !string.IsNullOrWhiteSpace(g.Key)))
        {
            var media = g.Average(c => c.Puntaje);
            if (Math.Abs(media - promedio) >= 0.8m)
            {
                alertas.Add(new("Diferencia entre áreas",
                    $"El área {g.Key} tiene un promedio de {N(media)} frente a {N(promedio)} de la empresa. Confirme con el comité que la diferencia es real.",
                    $"{g.Count()} evaluaciones en el área"));
            }
        }

        return alertas;
    }
}
