using System.Globalization;

namespace Selvana.Clima.Domain.Rotacion;

public sealed record SenalRiesgo(string Texto, string Evidencia, int Puntos);

public sealed record RiesgoPersona(string Nivel, int Puntos, IReadOnlyList<SenalRiesgo> Senales, IReadOnlyList<string> Acciones);

/// <summary>Lo que se sabe de una persona para estimar su riesgo de rotación. Sin edad, género, estado civil ni otros datos protegidos.</summary>
public sealed record DatosRiesgo(
    DateOnly FechaIngreso, DateOnly? FechaFinContrato, decimal? PulsoPromedio, int? PulsoRecomendaria, int? PulsoHito, decimal? ClimaArea, int? EnpsArea,
    decimal? Desempeno, string? Potencial);

/// <summary>
/// C4 · Riesgo de rotación por reglas explicadas (todavía no es un modelo predictivo: ese llega con retiros reales). Cada señal suma puntos y
/// trae su evidencia; el nivel orienta la conversación, nunca una decisión sobre la persona.
/// </summary>
public static class RiesgoRotacion
{
    public static RiesgoPersona Calcular(DatosRiesgo d, DateOnly hoy)
    {
        static string N(decimal x) => x.ToString("0.0", CultureInfo.GetCultureInfo("es-CO"));
        var senales = new List<SenalRiesgo>();
        var acciones = new List<string>();

        if (d.PulsoPromedio is { } pulso && (pulso <= 2.5m || d.PulsoRecomendaria <= 6))
        {
            senales.Add(new("Su pulso de ingreso salió bajo", $"Pulso de {d.PulsoHito} días · {N(pulso)} de 5 · recomendaría {d.PulsoRecomendaria} de 10", 35));
            acciones.Add("Converse esta semana con la persona y su líder sobre lo que respondió en el pulso.");
        }
        else if (d.PulsoPromedio is { } p2 && p2 <= 3.2m)
        {
            senales.Add(new("Su pulso de ingreso es apenas regular", $"Pulso de {d.PulsoHito} días · {N(p2)} de 5", 15));
        }

        var meses = (hoy.DayNumber - d.FechaIngreso.DayNumber) / 30;
        if (meses < 6)
        {
            senales.Add(new("Lleva menos de seis meses", $"Ingresó el {d.FechaIngreso:dd/MM/yyyy}", 10));
            acciones.Add("Refuerce el acompañamiento del primer semestre (inducción y seguimiento del líder).");
        }

        if (d.FechaFinContrato is { } fin && fin >= hoy && fin <= hoy.AddDays(45))
        {
            senales.Add(new("Su contrato termina pronto", $"Termina el {fin:dd/MM/yyyy}", 20));
            acciones.Add("Defina con tiempo si se renueva y cuénteselo: la incertidumbre empuja a buscar otro trabajo.");
        }

        if (d.ClimaArea is { } clima && clima < 3.2m)
        {
            senales.Add(new("El clima de su área está bajo", $"Promedio del área {N(clima)} de 5{(d.EnpsArea is { } e ? $" · eNPS {e}" : "")}", 15));
            acciones.Add("Trabaje con el líder del área un plan sobre los temas peor calificados de la encuesta.");
        }

        if (d.Desempeno is { } des && d.Potencial == "Alto" && des >= 3.6m)
        {
            senales.Add(new("Alto potencial y alto desempeño", $"Puntaje {N(des)} · potencial alto", 15));
            acciones.Add("Hable de su crecimiento: plan de carrera, vacantes internas o un reto nuevo.");
        }
        else if (d.Desempeno is { } bajo && bajo < 2.6m)
        {
            senales.Add(new("Su desempeño salió bajo", $"Puntaje {N(bajo)} de 5", 10));
            acciones.Add("Revise con el líder el plan de desarrollo y si el cargo encaja con la persona.");
        }

        var puntos = Math.Min(100, senales.Sum(s => s.Puntos));
        var nivel = puntos >= 45 ? "Alto" : puntos >= 25 ? "Medio" : "Bajo";
        return new RiesgoPersona(nivel, puntos, senales, acciones);
    }
}
