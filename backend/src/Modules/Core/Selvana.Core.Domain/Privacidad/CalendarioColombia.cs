namespace Selvana.Core.Domain.Privacidad;

/// <summary>
/// Días hábiles en Colombia: lunes a viernes sin festivos. Festivos de la Ley 51 de 1983 (Ley Emiliani): los fijos,
/// los que se trasladan al lunes siguiente y los que dependen de la Pascua.
/// </summary>
public static class CalendarioColombia
{
    public static bool EsHabil(DateOnly dia) =>
        dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Festivos(dia.Year).Contains(dia);

    /// <summary>
    /// Vencimiento de un plazo legal de <paramref name="dias"/> días hábiles contados desde el día hábil siguiente al recibo
    /// (como la Ley 1581 cuenta los plazos de consultas y reclamos).
    /// </summary>
    public static DateOnly SumarDiasHabiles(DateOnly recibido, int dias)
    {
        var dia = recibido;
        var contados = 0;
        while (contados < dias)
        {
            dia = dia.AddDays(1);
            if (EsHabil(dia))
            {
                contados++;
            }
        }

        return dia;
    }

    /// <summary>Días hábiles que quedan desde <paramref name="hoy"/> (sin contarlo) hasta el vencimiento (incluido). Negativo si ya venció.</summary>
    public static int DiasHabilesRestantes(DateOnly hoy, DateOnly vence)
    {
        if (hoy >= vence)
        {
            var vencidos = 0;
            for (var d = vence.AddDays(1); d <= hoy; d = d.AddDays(1))
            {
                if (EsHabil(d))
                {
                    vencidos++;
                }
            }

            return -vencidos;
        }

        var quedan = 0;
        for (var d = hoy.AddDays(1); d <= vence; d = d.AddDays(1))
        {
            if (EsHabil(d))
            {
                quedan++;
            }
        }

        return quedan;
    }

    public static HashSet<DateOnly> Festivos(int anio)
    {
        var pascua = DomingoDePascua(anio);
        return
        [
            new(anio, 1, 1),
            Lunes(new(anio, 1, 6)),
            Lunes(new(anio, 3, 19)),
            pascua.AddDays(-3),
            pascua.AddDays(-2),
            new(anio, 5, 1),
            Lunes(pascua.AddDays(39)),
            Lunes(pascua.AddDays(60)),
            Lunes(pascua.AddDays(68)),
            Lunes(new(anio, 6, 29)),
            new(anio, 7, 20),
            new(anio, 8, 7),
            Lunes(new(anio, 8, 15)),
            Lunes(new(anio, 10, 12)),
            Lunes(new(anio, 11, 1)),
            Lunes(new(anio, 11, 11)),
            new(anio, 12, 8),
            new(anio, 12, 25),
        ];
    }

    /// <summary>Ley Emiliani: si no cae en lunes, el festivo pasa al lunes siguiente.</summary>
    private static DateOnly Lunes(DateOnly dia) => dia.DayOfWeek == DayOfWeek.Monday ? dia : dia.AddDays(((int)DayOfWeek.Monday - (int)dia.DayOfWeek + 7) % 7);

    /// <summary>Algoritmo gregoriano anónimo (Meeus/Jones/Butcher).</summary>
    private static DateOnly DomingoDePascua(int anio)
    {
        var a = anio % 19;
        var b = anio / 100;
        var c = anio % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var mes = (h + l - (7 * m) + 114) / 31;
        var dia = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(anio, mes, dia);
    }
}
