using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Domain.Ciclos;

public enum TipoCiclo
{
    Anual,
    Semestral,
    Trimestral,
    PeriodoPrueba,
    FinMision,
}

/// <summary>90°: califica el líder · 180°: líder y autoevaluación · 360°: además los pares.</summary>
public enum AlcanceEvaluacion
{
    Grados90,
    Grados180,
    Grados360,
}

public enum EstadoCiclo
{
    Borrador,
    Abierto,
    Calibracion,
    Cerrado,
}

/// <summary>Una razón de una propuesta (de la IA o de las reglas), con su fuente.</summary>
public sealed class RazonPropuesta
{
    public string Signo { get; init; } = "+";
    public string Texto { get; init; } = "";
    public string Fuente { get; init; } = "";
}

public sealed class Competencia
{
    public string Nombre { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public int Peso { get; init; }
}

public sealed class ObjetivoBase
{
    public string Descripcion { get; init; } = "";
    public string Meta { get; init; } = "";
    public int Peso { get; init; }
}

/// <summary>
/// Formulario de un cargo dentro del ciclo: competencias con su peso y objetivos sugeridos. Lo propone la IA o la plantilla base (reglas),
/// RRHH lo edita y lo aprueba. Mientras no esté aprobado, la evaluación de ese cargo no arranca.
/// </summary>
public sealed class FormularioCargo
{
    public string Cargo { get; init; } = "";
    public List<Competencia> Competencias { get; set; } = [];
    public List<ObjetivoBase> Objetivos { get; set; } = [];

    /// <summary>«IA», «Plantilla base» o «Manual».</summary>
    public string Origen { get; set; } = "";
    public List<RazonPropuesta> Razones { get; set; } = [];
    public string? Modelo { get; set; }

    /// <summary>La IA está preparando el formulario.</summary>
    public bool Preparando { get; set; }
    public string? Error { get; set; }
    public string? AprobadoPor { get; set; }
    public DateTimeOffset? AprobadoEn { get; set; }
}

public static class CicloErrors
{
    public static readonly Error NoExiste = Error.NotFound("Desempeno.CicloNoExiste", "Ese ciclo de evaluación no existe en su empresa.");
    public static readonly Error Fechas = Error.Validation("Desempeno.Fechas", "La fecha de cierre debe ser posterior a la de inicio.");
    public static readonly Error NoEditable = Error.Conflict("Desempeno.NoEditable", "El ciclo ya arrancó: los formularios no se cambian.");
    public static readonly Error SinCargo = Error.NotFound("Desempeno.SinCargo", "Ese cargo no tiene formulario en el ciclo.");
    public static readonly Error Pesos = Error.Validation("Desempeno.Pesos", "Los pesos de las competencias deben sumar 100.");
    public static readonly Error PesosObjetivos = Error.Validation("Desempeno.PesosObjetivos", "Los pesos de los objetivos deben sumar 100.");
    public static readonly Error SinCompetencias = Error.Validation("Desempeno.SinCompetencias", "El formulario necesita al menos tres competencias.");
    public static readonly Error SinAprobar = Error.Conflict("Desempeno.SinAprobar", "Hay cargos con el formulario sin aprobar.");
    public static readonly Error Estado = Error.Conflict("Desempeno.Estado", "El ciclo no está en la etapa para hacer eso.");
    public static readonly Error Preparando = Error.Conflict("Desempeno.Preparando", "La IA está preparando ese formulario: espere un momento.");
}

/// <summary>
/// Ciclo de evaluación de desempeño (D1): tipo, alcance, fechas, peso de los objetivos frente a las competencias y un formulario por cargo.
/// Borrador (se arman los formularios) → Abierto (se evalúa) → Calibración (comité y 9-box) → Cerrado.
/// </summary>
public sealed class Ciclo : AggregateRoot<Guid>
{
    private Ciclo(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string Nombre { get; private set; } = "";
    public TipoCiclo Tipo { get; private set; }
    public AlcanceEvaluacion Alcance { get; private set; }
    public DateOnly Desde { get; private set; }
    public DateOnly Hasta { get; private set; }

    /// <summary>Cuánto pesan los objetivos en el puntaje final (el resto, las competencias).</summary>
    public int PesoObjetivos { get; private set; }
    public EstadoCiclo Estado { get; private set; }
    public List<FormularioCargo> Formularios { get; private set; } = [];
    public string CreadoPor { get; private init; } = "";
    public DateTimeOffset CreadoEn { get; private init; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Result<Ciclo> Crear(Guid empresaId, string nombre, TipoCiclo tipo, AlcanceEvaluacion alcance, DateOnly desde, DateOnly hasta, int pesoObjetivos,
        IEnumerable<string> cargos, string por, DateTimeOffset ahora)
    {
        if (hasta <= desde)
        {
            return CicloErrors.Fechas;
        }

        return new Ciclo(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            Nombre = nombre.Trim(),
            Tipo = tipo,
            Alcance = alcance,
            Desde = desde,
            Hasta = hasta,
            PesoObjetivos = Math.Clamp(pesoObjetivos, 0, 100),
            Estado = EstadoCiclo.Borrador,
            Formularios = [.. cargos.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order().Select(c => new FormularioCargo { Cargo = c })],
            CreadoPor = por,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
        };
    }

    public FormularioCargo? Formulario(string cargo) => Formularios.FirstOrDefault(f => string.Equals(f.Cargo, cargo, StringComparison.OrdinalIgnoreCase));

    /// <summary>Se pidió a la IA el formulario del cargo.</summary>
    public Result PedirFormularioIa(string cargo, DateTimeOffset ahora)
    {
        if (Estado != EstadoCiclo.Borrador)
        {
            return Result.Failure(CicloErrors.NoEditable);
        }

        if (Formulario(cargo) is not { } f)
        {
            return Result.Failure(CicloErrors.SinCargo);
        }

        f.Preparando = true;
        f.Error = null;
        Tocar(ahora);
        return Result.Success();
    }

    /// <summary>
    /// Guarda la propuesta o la edición de un formulario. Lo que llega de la IA o de la plantilla queda sin aprobar; editar a mano
    /// cuenta como confirmación de quien lo cambia solo cuando lo aprueba.
    /// </summary>
    public Result GuardarFormulario(string cargo, IReadOnlyList<Competencia> competencias, IReadOnlyList<ObjetivoBase> objetivos, string origen,
        IReadOnlyList<RazonPropuesta> razones, string? modelo, DateTimeOffset ahora)
    {
        if (Estado != EstadoCiclo.Borrador)
        {
            return Result.Failure(CicloErrors.NoEditable);
        }

        if (Formulario(cargo) is not { } f)
        {
            return Result.Failure(CicloErrors.SinCargo);
        }

        var validos = Validar(competencias, objetivos);
        if (validos.IsFailure)
        {
            return validos;
        }

        f.Competencias = [.. competencias];
        f.Objetivos = [.. objetivos];
        f.Origen = origen;
        f.Razones = [.. razones];
        f.Modelo = modelo;
        f.Preparando = false;
        f.Error = null;
        f.AprobadoPor = null;
        f.AprobadoEn = null;
        Formularios = [.. Formularios];
        Tocar(ahora);
        return Result.Success();
    }

    /// <summary>La IA no pudo: el cargo queda para la plantilla base o para hacerlo a mano.</summary>
    public void FallaFormulario(string cargo, string error, DateTimeOffset ahora)
    {
        if (Formulario(cargo) is { } f)
        {
            f.Preparando = false;
            f.Error = error;
            Formularios = [.. Formularios];
            Tocar(ahora);
        }
    }

    public Result AprobarFormulario(string cargo, string por, DateTimeOffset ahora)
    {
        if (Estado != EstadoCiclo.Borrador)
        {
            return Result.Failure(CicloErrors.NoEditable);
        }

        if (Formulario(cargo) is not { } f)
        {
            return Result.Failure(CicloErrors.SinCargo);
        }

        if (f.Preparando)
        {
            return Result.Failure(CicloErrors.Preparando);
        }

        var validos = Validar(f.Competencias, f.Objetivos);
        if (validos.IsFailure)
        {
            return validos;
        }

        f.AprobadoPor = por;
        f.AprobadoEn = ahora;
        Formularios = [.. Formularios];
        Tocar(ahora);
        return Result.Success();
    }

    /// <summary>Arranca la evaluación: todos los formularios deben estar aprobados.</summary>
    public Result Abrir(DateTimeOffset ahora)
    {
        if (Estado != EstadoCiclo.Borrador)
        {
            return Result.Failure(CicloErrors.Estado);
        }

        if (Formularios.Count == 0 || Formularios.Any(f => f.AprobadoEn is null))
        {
            return Result.Failure(CicloErrors.SinAprobar);
        }

        Estado = EstadoCiclo.Abierto;
        Tocar(ahora);
        return Result.Success();
    }

    public Result PasarACalibracion(DateTimeOffset ahora) => Avanzar(EstadoCiclo.Abierto, EstadoCiclo.Calibracion, ahora);

    public Result Cerrar(DateTimeOffset ahora) => Avanzar(EstadoCiclo.Calibracion, EstadoCiclo.Cerrado, ahora);

    private Result Avanzar(EstadoCiclo desde, EstadoCiclo hacia, DateTimeOffset ahora)
    {
        if (Estado != desde)
        {
            return Result.Failure(CicloErrors.Estado);
        }

        Estado = hacia;
        Tocar(ahora);
        return Result.Success();
    }

    private static Result Validar(IReadOnlyList<Competencia> competencias, IReadOnlyList<ObjetivoBase> objetivos)
    {
        if (competencias.Count < 3)
        {
            return Result.Failure(CicloErrors.SinCompetencias);
        }

        if (competencias.Sum(c => c.Peso) != 100)
        {
            return Result.Failure(CicloErrors.Pesos);
        }

        return objetivos.Count > 0 && objetivos.Sum(o => o.Peso) != 100 ? Result.Failure(CicloErrors.PesosObjetivos) : Result.Success();
    }

    private void Tocar(DateTimeOffset ahora) => ActualizadoEn = ahora;
}
