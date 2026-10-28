using Selvana.SharedKernel.Results;

namespace Selvana.Vinculacion.Domain.Procesos;

public enum MomentoInduccion
{
    AntesDelIngreso,
    PrimerDia,
    PrimeraSemana,
    PrimerMes,
    PeriodoPrueba,
}

public enum EstadoPlanInduccion
{
    /// <summary>Procesos que llegaron a onboarding antes de existir el plan: se genera a pedido.</summary>
    SinPlan,

    /// <summary>La IA está preparando el plan.</summary>
    Generando,
    FallaIa,

    /// <summary>Propuesta (de la IA o del plan base) que RRHH revisa y aprueba.</summary>
    PorAprobar,

    /// <summary>Aprobado: se hace el seguimiento tarea por tarea.</summary>
    EnCurso,
    Terminado,
}

/// <summary>Una tarea del plan de inducción: qué, quién y cuándo (día relativo al ingreso).</summary>
public sealed class TareaInduccion
{
    public Guid Id { get; init; }
    public string Titulo { get; init; } = "";
    public string Detalle { get; init; } = "";
    public string Responsable { get; init; } = "";
    public MomentoInduccion Momento { get; init; }

    /// <summary>Días desde el ingreso (negativo: antes de ingresar).</summary>
    public int Dia { get; init; }
    public DateOnly Fecha { get; init; }
    public bool Hecha { get; set; }
    public string? HechaPor { get; set; }
    public DateTimeOffset? HechaEn { get; set; }

    /// <summary>La propuso la IA y nadie la ha confirmado.</summary>
    public bool DeIa { get; set; }
}

public sealed class PlanInduccion
{
    public EstadoPlanInduccion Estado { get; set; }
    public List<TareaInduccion> Tareas { get; set; } = [];
    public List<RazonOferta> Razones { get; set; } = [];
    public List<string> AlertasIa { get; set; } = [];

    /// <summary>«IA» o «Plan base» (reglas, sin IA).</summary>
    public string? Origen { get; set; }
    public string? Modelo { get; set; }
    public decimal CostoUsd { get; set; }
    public string? ErrorIa { get; set; }
    public DateTimeOffset? PedidoEn { get; set; }
    public DateTimeOffset? PropuestoEn { get; set; }
    public string? AprobadoPor { get; set; }
    public DateTimeOffset? AprobadoEn { get; set; }
    public string? CerradoPor { get; set; }
    public DateTimeOffset? CerradoEn { get; set; }

    public int Avance => Tareas.Count == 0 ? 0 : (int)Math.Round(100.0 * Tareas.Count(t => t.Hecha) / Tareas.Count);
}

/// <summary>Una tarea tal como llega (de la IA, del plan base o editada por RRHH), antes de calcular su fecha.</summary>
public sealed record TareaPropuesta(string Titulo, string Detalle, string Responsable, MomentoInduccion Momento, int Dia, Guid? Id = null);

public static class OnboardingErrors
{
    public static readonly Error NoEnOnboarding = Error.Conflict("Vinculacion.NoEnOnboarding", "El plan de inducción se arma cuando el empleado ya está creado.");
    public static readonly Error Generando = Error.Conflict("Vinculacion.PlanGenerando", "La IA está preparando el plan de inducción: espere un momento.");
    public static readonly Error NoPorAprobar = Error.Conflict("Vinculacion.PlanNoPorAprobar", "El plan no está esperando aprobación.");
    public static readonly Error NoEnCurso = Error.Conflict("Vinculacion.PlanNoEnCurso", "El plan de inducción aún no está aprobado o ya terminó.");
    public static readonly Error SinTareas = Error.Validation("Vinculacion.PlanSinTareas", "El plan necesita al menos una tarea.");
    public static readonly Error TareaNoExiste = Error.NotFound("Vinculacion.TareaNoExiste", "Esa tarea no está en el plan.");
    public static readonly Error Pendientes = Error.Validation("Vinculacion.TareasPendientes", "Aún hay tareas sin terminar. Márquelas o quítelas del plan antes de cerrar el onboarding.");
}

/// <summary>
/// Plan base por reglas (sin IA): lo mínimo de una buena inducción en Colombia. Sirve cuando la IA falla o cuando
/// la empresa prefiere no usarla; RRHH lo ajusta igual que el de la IA.
/// </summary>
public static class PlanBaseInduccion
{
    public static readonly string[] Responsables = ["RRHH", "Líder", "Empleado", "SST", "TI", "Empresa usuaria"];

    public static IReadOnlyList<TareaPropuesta> Para(string cargo, string? empresaUsuaria, int periodoPruebaDias)
    {
        var tareas = new List<TareaPropuesta>
        {
            new("Enviar la bienvenida", "Fecha, hora y lugar de ingreso, a quién buscar y qué llevar el primer día.", "RRHH", MomentoInduccion.AntesDelIngreso, -3),
            new("Preparar el puesto y los accesos", $"Herramientas, dotación y accesos que necesita un(a) {cargo} desde el primer día.", "TI", MomentoInduccion.AntesDelIngreso, -2),
            new("Asignar un acompañante", "Una persona del equipo que guíe la primera semana y resuelva dudas.", "Líder", MomentoInduccion.AntesDelIngreso, -1),
            new("Recibir y presentar la empresa", "Bienvenida, historia, valores, reglamento interno de trabajo y entrega de carné y dotación.", "RRHH", MomentoInduccion.PrimerDia, 0),
            new("Inducción en seguridad y salud en el trabajo", "Riesgos del cargo, plan de emergencias, uso de elementos de protección y cómo reportar incidentes (SG-SST).", "SST", MomentoInduccion.PrimerDia, 0),
            new("Presentar al equipo y el lugar de trabajo", "Recorrido, presentación del equipo y de las personas con quienes trabajará.", "Líder", MomentoInduccion.PrimerDia, 0),
            new("Explicar las funciones del cargo", $"Funciones, metas y forma de trabajo del cargo {cargo}, con el manual de funciones.", "Líder", MomentoInduccion.PrimeraSemana, 2),
            new("Leer las políticas internas", "Reglamento interno, tratamiento de datos personales y canales para pedir ayuda.", "Empleado", MomentoInduccion.PrimeraSemana, 3),
            new("Seguimiento de la primera semana", "Conversación corta: cómo le fue, qué le falta y qué dudas tiene.", "Líder", MomentoInduccion.PrimeraSemana, 5),
            new("Revisión del primer mes", "Avance frente a las funciones del cargo y plan para los meses siguientes.", "Líder", MomentoInduccion.PrimerMes, 30),
        };

        if (!string.IsNullOrWhiteSpace(empresaUsuaria))
        {
            tareas.Insert(5, new("Inducción en la empresa usuaria", $"Normas, horarios, riesgos y lugar de trabajo en {empresaUsuaria}.", "Empresa usuaria", MomentoInduccion.PrimerDia, 0));
        }

        if (periodoPruebaDias > 7)
        {
            tareas.Add(new("Evaluar el periodo de prueba", $"Evaluación antes de que terminen los {periodoPruebaDias} días de prueba, con su retroalimentación.", "Líder",
                MomentoInduccion.PeriodoPrueba, periodoPruebaDias - 5));
        }

        return tareas;
    }
}

public sealed partial class ProcesoVinculacion
{
    public const int TareasMaximas = 30;

    public PlanInduccion Induccion { get; private set; } = new();

    private IEnumerable<TareaInduccion> Armar(IEnumerable<TareaPropuesta> tareas, bool deIa, Dictionary<Guid, TareaInduccion>? previas = null) =>
        tareas.Take(TareasMaximas).OrderBy(t => t.Dia).Select(t =>
        {
            var previa = t.Id is { } id && previas is not null && previas.TryGetValue(id, out var x) ? x : null;
            return new TareaInduccion
            {
                Id = previa?.Id ?? Guid.CreateVersion7(),
                Titulo = t.Titulo.Trim(),
                Detalle = t.Detalle.Trim(),
                Responsable = t.Responsable.Trim(),
                Momento = t.Momento,
                Dia = t.Dia,
                Fecha = Condiciones.FechaIngreso.AddDays(t.Dia),
                Hecha = previa?.Hecha ?? false,
                HechaPor = previa?.HechaPor,
                HechaEn = previa?.HechaEn,
                DeIa = deIa,
            };
        });

    /// <summary>Pedir (o volver a pedir) el plan a la IA.</summary>
    public Result PedirPlanInduccion(DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Onboarding)
        {
            return Result.Failure(OnboardingErrors.NoEnOnboarding);
        }

        if (Induccion.Estado is EstadoPlanInduccion.Generando)
        {
            return Result.Failure(OnboardingErrors.Generando);
        }

        if (Induccion.Estado is not (EstadoPlanInduccion.SinPlan or EstadoPlanInduccion.FallaIa or EstadoPlanInduccion.PorAprobar))
        {
            return Result.Failure(OnboardingErrors.NoPorAprobar);
        }

        Induccion = new PlanInduccion { Estado = EstadoPlanInduccion.Generando, PedidoEn = ahora };
        ActualizadoEn = ahora;
        return Result.Success();
    }

    public bool RegistrarPlanInduccion(IEnumerable<TareaPropuesta> tareas, IEnumerable<RazonOferta> razones, IEnumerable<string> alertas, string modelo,
        decimal costoUsd, DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Onboarding || Induccion.Estado != EstadoPlanInduccion.Generando)
        {
            return false;
        }

        Induccion = new PlanInduccion
        {
            Estado = EstadoPlanInduccion.PorAprobar,
            Tareas = [.. Armar(tareas, deIa: true)],
            Razones = [.. razones],
            AlertasIa = [.. alertas],
            Origen = "IA",
            Modelo = modelo,
            CostoUsd = costoUsd,
            PedidoEn = Induccion.PedidoEn,
            PropuestoEn = ahora,
        };
        ActualizadoEn = ahora;
        return Induccion.Tareas.Count > 0;
    }

    public bool FallaPlanInduccion(string error, DateTimeOffset ahora)
    {
        if (Induccion.Estado != EstadoPlanInduccion.Generando)
        {
            return false;
        }

        Induccion.Estado = EstadoPlanInduccion.FallaIa;
        Induccion.ErrorIa = error;
        ActualizadoEn = ahora;
        return true;
    }

    /// <summary>Plan por reglas, sin IA: cuando la IA falla o la empresa prefiere empezar de una plantilla.</summary>
    public Result UsarPlanBase(DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Onboarding)
        {
            return Result.Failure(OnboardingErrors.NoEnOnboarding);
        }

        if (Induccion.Estado is EstadoPlanInduccion.EnCurso or EstadoPlanInduccion.Terminado)
        {
            return Result.Failure(OnboardingErrors.NoPorAprobar);
        }

        Induccion = new PlanInduccion
        {
            Estado = EstadoPlanInduccion.PorAprobar,
            Tareas = [.. Armar(PlanBaseInduccion.Para(Vacante.Cargo, EmpresaUsuaria, Condiciones.PeriodoPruebaDias), deIa: false)],
            Origen = "Plan base",
            PropuestoEn = ahora,
        };
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>RRHH ajusta el plan (antes o durante el seguimiento). Editar cuenta como confirmar: las tareas dejan de ser violeta.</summary>
    public Result EditarPlanInduccion(IReadOnlyList<TareaPropuesta> tareas, DateTimeOffset ahora)
    {
        if (Induccion.Estado is not (EstadoPlanInduccion.PorAprobar or EstadoPlanInduccion.EnCurso))
        {
            return Result.Failure(OnboardingErrors.NoPorAprobar);
        }

        if (tareas.Count == 0)
        {
            return Result.Failure(OnboardingErrors.SinTareas);
        }

        Induccion.Tareas = [.. Armar(tareas, deIa: false, Induccion.Tareas.ToDictionary(t => t.Id))];
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Decisión humana: el plan queda aprobado y arranca el seguimiento.</summary>
    public Result AprobarPlanInduccion(string por, DateTimeOffset ahora)
    {
        if (Induccion.Estado != EstadoPlanInduccion.PorAprobar)
        {
            return Result.Failure(OnboardingErrors.NoPorAprobar);
        }

        if (Induccion.Tareas.Count == 0)
        {
            return Result.Failure(OnboardingErrors.SinTareas);
        }

        Induccion.Tareas.ForEach(t => t.DeIa = false);
        Induccion.Estado = EstadoPlanInduccion.EnCurso;
        Induccion.AprobadoPor = por;
        Induccion.AprobadoEn = ahora;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    public Result<TareaInduccion> MarcarTareaInduccion(Guid tareaId, bool hecha, string por, DateTimeOffset ahora)
    {
        if (Induccion.Estado != EstadoPlanInduccion.EnCurso)
        {
            return Result.Failure<TareaInduccion>(OnboardingErrors.NoEnCurso);
        }

        var tarea = Induccion.Tareas.FirstOrDefault(t => t.Id == tareaId);
        if (tarea is null)
        {
            return Result.Failure<TareaInduccion>(OnboardingErrors.TareaNoExiste);
        }

        tarea.Hecha = hecha;
        tarea.HechaPor = hecha ? por : null;
        tarea.HechaEn = hecha ? ahora : null;
        ActualizadoEn = ahora;
        return tarea;
    }

    /// <summary>Con todas las tareas hechas, el onboarding termina y el proceso queda «Vinculado».</summary>
    public Result CerrarOnboarding(string por, DateTimeOffset ahora)
    {
        if (Induccion.Estado != EstadoPlanInduccion.EnCurso)
        {
            return Result.Failure(OnboardingErrors.NoEnCurso);
        }

        if (Induccion.Tareas.Any(t => !t.Hecha))
        {
            return Result.Failure(OnboardingErrors.Pendientes);
        }

        Induccion.Estado = EstadoPlanInduccion.Terminado;
        Induccion.CerradoPor = por;
        Induccion.CerradoEn = ahora;
        Etapa = EtapaVinculacion.Vinculado;
        ActualizadoEn = ahora;
        return Result.Success();
    }
}
