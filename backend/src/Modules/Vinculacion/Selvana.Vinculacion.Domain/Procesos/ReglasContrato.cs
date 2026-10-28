using System.Globalization;

namespace Selvana.Vinculacion.Domain.Procesos;

public enum TipoContrato
{
    TerminoFijo,
    Indefinido,
    ObraOLabor,
    Aprendizaje,

    /// <summary>Trabajador en misión de una empresa de servicios temporales en una empresa usuaria.</summary>
    MisionTemporal,
}

public enum NivelAlerta
{
    /// <summary>La oferta no se puede aprobar hasta corregirlo.</summary>
    Bloquea,

    /// <summary>Se puede aprobar, pero conviene revisarlo.</summary>
    Revisar,
    Info,
}

public sealed record AlertaContrato(NivelAlerta Nivel, string Texto, string Fuente);

/// <summary>Valores legales del año (configurables): salario mínimo y auxilio de transporte.</summary>
public sealed record ParametrosLegales(decimal SalarioMinimoCop, decimal AuxilioTransporteCop);

/// <summary>
/// Revisión por reglas (sin IA) de las condiciones de un contrato en Colombia: salario mínimo, duración del término fijo
/// y de la misión, periodo de prueba y auxilio de transporte. Las plantillas aún no tienen revisión jurídica.
/// </summary>
public static class ReglasContrato
{
    public const int PeriodoPruebaMaximoDias = 60;

    private static readonly CultureInfo Colombia = new("es-CO");

    public static string Pesos(decimal valor) => valor.ToString("$#,##0", Colombia);

    public static string Nombre(TipoContrato tipo) => tipo switch
    {
        TipoContrato.TerminoFijo => "Término fijo",
        TipoContrato.Indefinido => "Término indefinido",
        TipoContrato.ObraOLabor => "Obra o labor",
        TipoContrato.Aprendizaje => "Aprendizaje",
        TipoContrato.MisionTemporal => "Misión en empresa usuaria",
        _ => tipo.ToString(),
    };

    /// <summary>El nombre que usa la empresa para el tipo elegido; si no hay, el de la modalidad legal.</summary>
    public static string Nombre(CondicionesContratacion c) => string.IsNullOrWhiteSpace(c.TipoContratoNombre) ? Nombre(c.Tipo) : c.TipoContratoNombre;

    public static bool TieneAuxilioTransporte(CondicionesContratacion c, ParametrosLegales p) =>
        c.Tipo != TipoContrato.Aprendizaje && c.SalarioCop <= 2 * p.SalarioMinimoCop;

    /// <summary>Días entre el ingreso y el fin, contando los dos; 0 si no tiene fin.</summary>
    public static int DuracionDias(CondicionesContratacion c) => c.FechaFin is { } fin ? fin.DayNumber - c.FechaIngreso.DayNumber + 1 : 0;

    public static IReadOnlyList<AlertaContrato> Revisar(CondicionesContratacion c, ParametrosLegales p, DateOnly hoy, string? empresaUsuaria,
        decimal? salarioMinimoVacante = null, decimal? salarioMaximoVacante = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(p);
        var alertas = new List<AlertaContrato>();
        void Agregar(NivelAlerta n, string t, string f) => alertas.Add(new AlertaContrato(n, t, f));

        if (c.Tipo == TipoContrato.Aprendizaje)
        {
            if (c.SalarioCop < Math.Round(p.SalarioMinimoCop * 0.75m))
            {
                Agregar(NivelAlerta.Bloquea, $"La cuota de sostenimiento ({Pesos(c.SalarioCop)}) es menor que el 75 % del salario mínimo ({Pesos(Math.Round(p.SalarioMinimoCop * 0.75m))}).",
                    "Contrato de aprendizaje");
            }

            Agregar(NivelAlerta.Revisar, "En la etapa práctica la cuota de sostenimiento sube: confirme el valor con la institución educativa.", "Contrato de aprendizaje");
        }
        else if (c.SalarioCop < p.SalarioMinimoCop)
        {
            Agregar(NivelAlerta.Bloquea, $"El salario ({Pesos(c.SalarioCop)}) es menor que el salario mínimo legal ({Pesos(p.SalarioMinimoCop)}).", "Salario mínimo");
        }

        if (salarioMinimoVacante is { } min && salarioMaximoVacante is { } max && max > 0 && (c.SalarioCop < min || c.SalarioCop > max))
        {
            Agregar(NivelAlerta.Revisar, $"El salario está fuera del rango aprobado en la vacante ({Pesos(min)} – {Pesos(max)}).", "Vacante");
        }

        var duracion = DuracionDias(c);
        if (c.FechaFin is { } fin && fin < c.FechaIngreso)
        {
            Agregar(NivelAlerta.Bloquea, "La fecha de terminación es anterior a la de ingreso.", "Fechas");
        }

        switch (c.Tipo)
        {
            case TipoContrato.TerminoFijo when c.FechaFin is null:
                Agregar(NivelAlerta.Bloquea, "El contrato a término fijo necesita fecha de terminación.", "Art. 46 CST");
                break;
            case TipoContrato.TerminoFijo when duracion > 4 * 365 + 1:
                Agregar(NivelAlerta.Bloquea, "El término fijo no puede pasar de 4 años.", "Art. 46 CST");
                break;
            case TipoContrato.MisionTemporal when string.IsNullOrWhiteSpace(empresaUsuaria):
                Agregar(NivelAlerta.Revisar, "La misión temporal es para una empresa usuaria: la vacante no tiene una.", "Art. 77 Ley 50 de 1990");
                break;
            case TipoContrato.MisionTemporal when duracion > 366:
                Agregar(NivelAlerta.Bloquea, "Una misión en la misma empresa usuaria no puede pasar de un año (6 meses prorrogables por 6 más).", "Art. 77 Ley 50 de 1990");
                break;
            case TipoContrato.MisionTemporal when duracion > 184:
                Agregar(NivelAlerta.Revisar, "La misión pasa de 6 meses: necesita la prórroga por escrito.", "Art. 77 Ley 50 de 1990");
                break;
        }

        if (c.Tipo is TipoContrato.ObraOLabor or TipoContrato.MisionTemporal && string.IsNullOrWhiteSpace(c.Labor))
        {
            Agregar(NivelAlerta.Bloquea, "Describa la obra o labor contratada: de ella depende cuándo termina el contrato.", "Art. 45 CST");
        }

        if (c.PeriodoPruebaDias > PeriodoPruebaMaximoDias)
        {
            Agregar(NivelAlerta.Bloquea, $"El periodo de prueba no puede pasar de 2 meses ({PeriodoPruebaMaximoDias} días).", "Art. 78 CST");
        }
        else if (c.FechaFin is not null && duracion is > 0 and < 365 && c.PeriodoPruebaDias > duracion / 5)
        {
            Agregar(NivelAlerta.Bloquea, $"En contratos de menos de un año el periodo de prueba no puede pasar de la quinta parte ({duracion / 5} días).", "Art. 78 CST");
        }

        if (c.FechaIngreso < hoy)
        {
            Agregar(NivelAlerta.Revisar, "La fecha de ingreso ya pasó: actualícela antes de enviar la oferta.", "Fechas");
        }

        if (TieneAuxilioTransporte(c, p))
        {
            Agregar(NivelAlerta.Info, $"Gana hasta 2 salarios mínimos: recibe auxilio de transporte ({Pesos(p.AuxilioTransporteCop)}).", "Ley 15 de 1959");
        }

        Agregar(NivelAlerta.Info, "Las plantillas aún no tienen revisión jurídica: lea el texto antes de aprobarlo.", "Selvana");
        return alertas;
    }

    public static bool Bloquea(IEnumerable<AlertaContrato> alertas) => alertas.Any(a => a.Nivel == NivelAlerta.Bloquea);
}
