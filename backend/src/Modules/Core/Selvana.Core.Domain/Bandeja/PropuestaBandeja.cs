using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Domain.Bandeja;

public enum EstadoPropuesta
{
    Pendiente,

    /// <summary>El módulo dueño ejecutó la decisión (publicó, aprobó, respondió).</summary>
    Resuelta,

    /// <summary>Una persona descartó la sugerencia de la IA, con su motivo.</summary>
    Descartada,
}

public sealed class RazonBandeja
{
    public string Signo { get; init; } = "+";
    public string Texto { get; init; } = "";
    public string Fuente { get; init; } = "";
}

public sealed class CampoBandeja
{
    public string Etiqueta { get; init; } = "";
    public string Valor { get; init; } = "";
}

public sealed class AccionBandeja
{
    public string Etiqueta { get; init; } = "";
    public string Metodo { get; init; } = "POST";
    public string Ruta { get; init; } = "";
}

public static class PropuestaErrors
{
    public static readonly Error NoExiste = Error.NotFound("Propuesta.NoExiste", "Esa decisión ya no está en la bandeja.");
    public static readonly Error YaDecidida = Error.Conflict("Propuesta.YaDecidida", "Esa decisión ya se tomó.");
    public static readonly Error SinMotivo = Error.Validation("Propuesta.SinMotivo", "Diga por qué descarta la sugerencia: así la IA aprende.");
}

/// <summary>
/// Una decisión en la bandeja de aprobaciones: qué propone (la IA o un proceso), por qué, con qué evidencia y qué pasa al aprobar.
/// La bandeja no cambia datos de otros módulos: el botón principal llama al módulo dueño y este avisa cuando quedó resuelta.
/// </summary>
public sealed class PropuestaBandeja : AggregateRoot<Guid>
{
    private PropuestaBandeja(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public Guid? EmpresaUsuariaId { get; private set; }
    public string Modulo { get; private init; } = "";
    public string Tipo { get; private init; } = "";
    public Guid EntidadId { get; private init; }
    public string Titulo { get; private set; } = "";
    public string Descripcion { get; private set; } = "";
    public string? Agente { get; private set; }
    public string? Modelo { get; private set; }
    public List<RazonBandeja> Razones { get; private set; } = [];
    public List<CampoBandeja> Campos { get; private set; } = [];
    public AccionBandeja? Accion { get; private set; }
    public string Revisar { get; private set; } = "";
    public bool AfectaPersona { get; private set; }
    public DateOnly? VenceEl { get; private set; }

    /// <summary>Roles que la ven (por ejemplo, solo gerencia); vacío = todos los que aprueban.</summary>
    public List<string> ParaRoles { get; private set; } = [];
    public DateTimeOffset CreadaEn { get; private init; }
    public DateTimeOffset ActualizadaEn { get; private set; }
    public EstadoPropuesta Estado { get; private set; }
    public string? Decision { get; private set; }
    public string? DecididaPor { get; private set; }
    public DateTimeOffset? DecididaEn { get; private set; }

    public bool EsDeIa => Agente is not null;

    public static PropuestaBandeja Crear(Guid empresaId, string modulo, string tipo, Guid entidadId, DateTimeOffset ahora) => new(Guid.CreateVersion7())
    {
        EmpresaId = empresaId,
        Modulo = modulo,
        Tipo = tipo,
        EntidadId = entidadId,
        CreadaEn = ahora,
        ActualizadaEn = ahora,
        Estado = EstadoPropuesta.Pendiente,
    };

    /// <summary>Contenido nuevo (la IA volvió a redactar, cambió el plazo…). Una propuesta ya decidida vuelve a quedar pendiente.</summary>
    public void Actualizar(
        Guid? empresaUsuariaId, string titulo, string descripcion, string? agente, string? modelo, IEnumerable<RazonBandeja> razones,
        IEnumerable<CampoBandeja> campos, AccionBandeja? accion, string revisar, bool afectaPersona, DateOnly? venceEl, IEnumerable<string>? paraRoles, DateTimeOffset ahora)
    {
        EmpresaUsuariaId = empresaUsuariaId;
        Titulo = titulo.Trim();
        Descripcion = descripcion.Trim();
        Agente = agente;
        Modelo = modelo;
        Razones = [.. razones.Take(5)];
        Campos = [.. campos.Take(6)];
        Accion = accion;
        Revisar = revisar;
        AfectaPersona = afectaPersona;
        VenceEl = venceEl;
        ParaRoles = [.. paraRoles ?? []];
        ActualizadaEn = ahora;
        Estado = EstadoPropuesta.Pendiente;
        Decision = null;
        DecididaPor = null;
        DecididaEn = null;
    }

    /// <summary>El módulo dueño ejecutó la decisión.</summary>
    public void Resolver(string decision, string por, DateTimeOffset ahora)
    {
        if (Estado != EstadoPropuesta.Pendiente)
        {
            return;
        }

        Estado = EstadoPropuesta.Resuelta;
        Decision = decision;
        DecididaPor = por;
        DecididaEn = ahora;
    }

    /// <summary>Una persona descarta la sugerencia. Lo que afecta a una persona nunca se descarta sin decir por qué.</summary>
    public Result Descartar(string motivo, string por, DateTimeOffset ahora)
    {
        if (Estado != EstadoPropuesta.Pendiente)
        {
            return Result.Failure(PropuestaErrors.YaDecidida);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result.Failure(PropuestaErrors.SinMotivo);
        }

        Estado = EstadoPropuesta.Descartada;
        Decision = $"Descartada: {motivo.Trim()}";
        DecididaPor = por;
        DecididaEn = ahora;
        return Result.Success();
    }

    /// <summary>Urgente: vence en 2 días o menos, o ya venció.</summary>
    public bool EsUrgente(DateOnly hoy) => VenceEl is { } v && v.DayNumber - hoy.DayNumber <= 2;
}
