namespace Selvana.Vinculacion.Domain.Procesos;

public sealed record CondicionesSugeridas(CondicionesContratacion Condiciones, IReadOnlyList<RazonOferta> Razones);

/// <summary>
/// Prellena las condiciones de la contratación con reglas (sin IA) y explica cada una: tipo de contrato según la vacante,
/// salario según la aspiración y el rango, fecha de ingreso, duración y periodo de prueba. RRHH las ajusta.
/// </summary>
public static class SugeridorCondiciones
{
    public static CondicionesSugeridas Sugerir(string cargo, string? empresaUsuaria, string jornada, string ciudad, decimal salarioMinimo,
        decimal salarioMaximo, decimal? aspiracion, ParametrosLegales p, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(p);
        var razones = new List<RazonOferta>();
        void Razon(string signo, string texto, string fuente) => razones.Add(new RazonOferta { Signo = signo, Texto = texto, Fuente = fuente });

        TipoContrato tipo;
        if ((cargo ?? "").Contains("aprendiz", StringComparison.OrdinalIgnoreCase))
        {
            tipo = TipoContrato.Aprendizaje;
            Razon("+", "El cargo es de aprendiz: contrato de aprendizaje", "Vacante · cargo");
        }
        else if (!string.IsNullOrWhiteSpace(empresaUsuaria))
        {
            tipo = TipoContrato.MisionTemporal;
            Razon("+", $"La vacante es para la empresa usuaria {empresaUsuaria}: contrato por la duración de la misión", "Vacante · empresa usuaria");
        }
        else
        {
            tipo = TipoContrato.Indefinido;
            Razon("+", "Es para su propia planta: el término indefinido es la regla general", "Art. 47 CST");
        }

        // Ingreso: el lunes siguiente a una semana, para dar tiempo a documentos y examen médico
        var ingreso = hoy.AddDays(7);
        while (ingreso.DayOfWeek != DayOfWeek.Monday)
        {
            ingreso = ingreso.AddDays(1);
        }

        Razon("+", $"Ingreso el lunes {ingreso:dd/MM}: deja una semana para documentos, examen médico y afiliaciones", "Selvana");

        var salario = salarioMinimo > 0 ? salarioMinimo : p.SalarioMinimoCop;
        if (aspiracion is { } a && a >= salarioMinimo && (salarioMaximo <= 0 || a <= salarioMaximo))
        {
            salario = a;
            Razon("+", $"Su aspiración ({ReglasContrato.Pesos(a)}) está dentro del rango de la vacante", "Preselección");
        }
        else if (aspiracion is { } b && salarioMaximo > 0 && b > salarioMaximo)
        {
            salario = salarioMaximo;
            Razon("−", $"Su aspiración ({ReglasContrato.Pesos(b)}) supera el rango: se propone el máximo aprobado", "Preselección");
        }
        else
        {
            Razon("+", $"Se propone el mínimo del rango aprobado ({ReglasContrato.Pesos(salario)})", "Vacante · salario");
        }

        if (tipo == TipoContrato.Aprendizaje)
        {
            salario = Math.Max(Math.Round(p.SalarioMinimoCop * 0.75m), Math.Min(salario, p.SalarioMinimoCop));
        }
        else
        {
            salario = Math.Max(salario, p.SalarioMinimoCop);
        }

        DateOnly? fin = tipo switch
        {
            TipoContrato.MisionTemporal => ingreso.AddMonths(6).AddDays(-1),
            TipoContrato.Aprendizaje => ingreso.AddMonths(12).AddDays(-1),
            _ => null,
        };
        var dias = fin is { } f ? f.DayNumber - ingreso.DayNumber + 1 : 0;
        var prueba = tipo == TipoContrato.Aprendizaje ? 0 : dias is > 0 and < 365 ? Math.Min(ReglasContrato.PeriodoPruebaMaximoDias, dias / 5) : ReglasContrato.PeriodoPruebaMaximoDias;
        if (tipo == TipoContrato.MisionTemporal)
        {
            Razon("+", $"Misión de 6 meses con periodo de prueba de {prueba} días (la quinta parte)", "Art. 77 Ley 50 de 1990 · art. 78 CST");
        }
        else if (prueba > 0)
        {
            Razon("+", $"Periodo de prueba de {prueba} días, el máximo legal", "Art. 78 CST");
        }

        var labor = tipo == TipoContrato.MisionTemporal
            ? $"Labores de {cargo} en {empresaUsuaria}, según la necesidad de la empresa usuaria."
            : null;

        return new CondicionesSugeridas(new CondicionesContratacion
        {
            Tipo = tipo,
            SalarioCop = salario,
            FechaIngreso = ingreso,
            FechaFin = fin,
            PeriodoPruebaDias = prueba,
            Labor = labor,
            Jornada = jornada,
            Ciudad = ciudad,
        }, razones.Take(5).ToList());
    }
}
