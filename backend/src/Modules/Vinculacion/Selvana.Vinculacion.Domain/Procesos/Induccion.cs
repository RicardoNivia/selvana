using Selvana.SharedKernel.Results;

namespace Selvana.Vinculacion.Domain.Procesos;

public enum EstadoPlanInduccion
{
    /// <summary>La IA está proponiendo las tareas propias del cargo.</summary>
    Redactando,
    PorAprobar,

    /// <summary>La IA no respondió: quedan las tareas legales y RRHH puede completar el plan a mano.</summary>
    FallaIa,
    Aprobado,
}

public enum EstadoTarea
{
    Pendiente,
    Hecha,
    NoAplica,
}

/// <summary>Una tarea del plan de inducción: qué, quién, cuándo (días desde el ingreso) y de dónde sale.</summary>
public sealed class TareaInduccion
{
    public Guid Id { get; init; }
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";

    /// <summary>Rol que la hace (<see cref="ResponsablesInduccion"/>).</summary>
    public string Responsable { get; set; } = "";

    /// <summary>Persona concreta, si RRHH la asignó.</summary>
    public string? Asignado { get; set; }

    /// <summary>Días desde el ingreso (negativo = antes de que llegue).</summary>
    public int Dia { get; set; }
    public DateOnly Fecha { get; set; }

    /// <summary>Tarea legal por reglas: no se quita ni se marca como «no aplica».</summary>
    public bool Obligatoria { get; init; }
    public string Fuente { get; set; } = "";

    /// <summary>La propuso la IA y nadie la ha cambiado.</summary>
    public bool DeIa { get; set; }
    public EstadoTarea Estado { get; set; }
    public string? Nota { get; set; }
    public string? CerradaPor { get; set; }
    public DateTimeOffset? CerradaEn { get; set; }
}

/// <summary>Plan de inducción del nuevo empleado: tareas legales por reglas más las del cargo que propone la IA; RRHH lo ajusta y lo aprueba.</summary>
public sealed class PlanInduccion
{
    public EstadoPlanInduccion Estado { get; set; }
    public List<TareaInduccion> Tareas { get; set; } = [];
    public List<RazonOferta> Razones { get; set; } = [];
    public List<string> AlertasIa { get; set; } = [];
    public string? Modelo { get; set; }
    public decimal CostoUsd { get; set; }
    public string? ErrorIa { get; set; }
    public DateTimeOffset? RedactadoEn { get; set; }
    public string? EditadoPor { get; set; }
    public DateTimeOffset? EditadoEn { get; set; }
    public string? AprobadoPor { get; set; }
    public DateTimeOffset? AprobadoEn { get; set; }
}

/// <summary>Una tarea que propone la IA (sin estado ni fecha: la fecha sale del ingreso).</summary>
public sealed record TareaPropuesta(string Titulo, string Descripcion, string Responsable, int Dia, string Fuente);

/// <summary>Una tarea tal como la deja RRHH al ajustar el plan; sin Id es nueva.</summary>
public sealed record TareaEditada(Guid? Id, string Titulo, string Descripcion, string Responsable, string? Asignado, int Dia);

public static class ResponsablesInduccion
{
    public const string Rrhh = "Recursos Humanos";
    public const string Lider = "Líder inmediato";
    public const string Sst = "Seguridad y salud en el trabajo";
    public const string Tecnologia = "Tecnología";
    public const string Trabajador = "Trabajador";

    public static readonly IReadOnlyList<string> Todos = [Rrhh, Lider, Sst, Tecnologia, Trabajador];

    /// <summary>«SST», «jefe», «líder del área» → el rol de la lista; si no se reconoce, Recursos Humanos.</summary>
    public static string Normalizar(string? responsable)
    {
        var r = (responsable ?? "").Trim().ToLowerInvariant();
        return Todos.FirstOrDefault(t => string.Equals(t, responsable?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? (r.Contains("sst", StringComparison.Ordinal) || r.Contains("seguridad", StringComparison.Ordinal) ? Sst
                : r.Contains("lider", StringComparison.Ordinal) || r.Contains("líder", StringComparison.Ordinal) || r.Contains("jefe", StringComparison.Ordinal)
                  || r.Contains("supervisor", StringComparison.Ordinal) ? Lider
                : r.Contains("tecnolog", StringComparison.Ordinal) || r.Contains("sistemas", StringComparison.Ordinal) || r is "ti" or "it" ? Tecnologia
                : r.Contains("trabajador", StringComparison.Ordinal) || r.Contains("empleado", StringComparison.Ordinal) ? Trabajador
                : Rrhh);
    }
}

public static class InduccionErrors
{
    public static readonly Error NoEnOnboarding = Error.Conflict("Induccion.NoEnEtapa", "El plan de inducción se arma cuando el empleado ya está creado.");
    public static readonly Error Redactando = Error.Conflict("Induccion.Redactando", "La IA está proponiendo el plan: espere un momento.");
    public static readonly Error YaAprobado = Error.Conflict("Induccion.YaAprobado", "El plan ya está aprobado: ajuste las tareas o márquelas como hechas.");
    public static readonly Error NoAprobado = Error.Conflict("Induccion.NoAprobado", "Apruebe el plan antes de hacerle seguimiento.");
    public static readonly Error SinTareas = Error.Validation("Induccion.SinTareas", "El plan necesita al menos una tarea.");
    public static readonly Error TareaInvalida = Error.Validation("Induccion.TareaInvalida", "Cada tarea necesita un título y un día entre 5 días antes y 180 días después del ingreso.");
    public static readonly Error ObligatoriaNoSeQuita = Error.Validation("Induccion.Obligatoria", "Las tareas legales no se pueden quitar del plan.");
    public static readonly Error ObligatoriaNoAplica = Error.Validation("Induccion.ObligatoriaNoAplica", "Es una tarea legal: no se puede marcar como «no aplica».");
    public static readonly Error SinNota = Error.Validation("Induccion.SinNota", "Diga por qué la tarea no aplica.");
    public static readonly Error TareaNoExiste = Error.NotFound("Induccion.TareaNoExiste", "Esa tarea no está en el plan.");
    public static readonly Error Pendientes = Error.Conflict("Induccion.Pendientes", "Quedan tareas pendientes: márquelas como hechas o como «no aplica» antes de cerrar el onboarding.");
}

/// <summary>Reglas de la inducción (sin IA): las tareas que la ley pide a todo nuevo trabajador y cómo se fechan.</summary>
public static class ReglasInduccion
{
    public const int DiaMinimo = -5;
    public const int DiaMaximo = 180;

    /// <summary>La fecha del día N desde el ingreso; si cae en fin de semana, el lunes siguiente.</summary>
    public static DateOnly Fecha(DateOnly ingreso, int dia)
    {
        var f = ingreso.AddDays(dia);
        return f.DayOfWeek switch
        {
            DayOfWeek.Saturday => f.AddDays(2),
            DayOfWeek.Sunday => f.AddDays(1),
            _ => f,
        };
    }

    /// <summary>Tareas legales según el cargo, la clase de riesgo de la ARL, el contrato y si es misión en una empresa usuaria.</summary>
    public static List<TareaInduccion> Obligatorias(ProcesoVinculacion p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var ingreso = p.Condiciones.FechaIngreso;
        var tareas = new List<TareaInduccion>();
        void Agregar(string titulo, string descripcion, string responsable, int dia, string fuente) => tareas.Add(new TareaInduccion
        {
            Id = Guid.CreateVersion7(), Titulo = titulo, Descripcion = descripcion, Responsable = responsable, Dia = dia, Fecha = Fecha(ingreso, dia),
            Obligatoria = true, Fuente = fuente,
        });

        Agregar("Inducción en seguridad y salud en el trabajo", "Peligros del cargo, plan de emergencias, reporte de incidentes y derechos y deberes en SST. Deje la constancia firmada.",
            ResponsablesInduccion.Sst, 0, "Decreto 1072 de 2015, art. 2.2.4.6.11");
        Agregar("Socializar el reglamento interno de trabajo", "Explique el reglamento, el horario y el canal para dudas y quejas; registre que lo recibió.",
            ResponsablesInduccion.Rrhh, 0, "CST, art. 104 y siguientes");

        var claseRiesgo = p.Afiliaciones.FirstOrDefault(a => a.Tipo == TipoAfiliacion.Arl)?.ClaseRiesgo ?? ReglasAlta.ClaseSugerida(p.Vacante.Cargo).Clase;
        if (claseRiesgo >= 3)
        {
            Agregar("Entregar los elementos de protección personal", $"Entregue los EPP del cargo (riesgo clase {claseRiesgo}) y deje el registro de entrega firmado.",
                ResponsablesInduccion.Sst, 0, "Decreto 1072 de 2015, art. 2.2.4.6.24");
        }

        if (p.Condiciones.Tipo == TipoContrato.MisionTemporal && !string.IsNullOrWhiteSpace(p.EmpresaUsuaria))
        {
            Agregar($"Inducción en el sitio de {p.EmpresaUsuaria}", "Recorrido, normas y peligros propios del sitio de la misión, coordinado con la empresa usuaria.",
                ResponsablesInduccion.Lider, 0, "Decreto 1072 de 2015, art. 2.2.4.2.2.16");
        }

        if (p.Condiciones.PeriodoPruebaDias > 7)
        {
            var dia = p.Condiciones.PeriodoPruebaDias - 7;
            Agregar("Evaluar el periodo de prueba", $"Revise el desempeño antes de que termine el periodo de prueba ({p.Condiciones.PeriodoPruebaDias} días) y deje el concepto por escrito.",
                ResponsablesInduccion.Lider, dia, "CST, art. 76 a 80");
        }

        return tareas;
    }

    /// <summary>Para no duplicar: «Inducción en SST» e «Inducción en seguridad y salud en el trabajo» son la misma tarea.</summary>
    public static string Clave(string titulo)
    {
        var t = System.Text.RegularExpressions.Regex.Replace((titulo ?? "").ToLowerInvariant(), @"\bsst\b", "seguridad y salud en el trabajo");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\bepp\b", "elementos de protección personal");
        return new([.. t.Normalize(System.Text.NormalizationForm.FormD).Where(char.IsLetterOrDigit)]);
    }

    public static bool SePareceA(string a, string b)
    {
        var x = Clave(a);
        var y = Clave(b);
        return x.Length > 0 && y.Length > 0 && (x == y || (Math.Min(x.Length, y.Length) >= 12 && (x.Contains(y, StringComparison.Ordinal) || y.Contains(x, StringComparison.Ordinal))));
    }
}
