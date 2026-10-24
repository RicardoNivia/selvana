namespace Selvana.AI.Motor;

/// <summary>Lo que el agente sabe de un caso entre una ejecución y la siguiente (ia.memorias_caso).</summary>
public sealed class MemoriaCaso
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmpresaId { get; init; }
    public string Agente { get; init; } = "";
    public string Caso { get; init; } = "";

    /// <summary>Hechos que el agente decidió recordar: clave → valor.</summary>
    public Dictionary<string, string> Hechos { get; set; } = [];
    public DateTimeOffset ActualizadaEn { get; set; }
}

public enum TipoAccionAgente
{
    Modelo,
    Herramienta,
    Recuerdo,
    Propuesta,
    Respuesta,
    Error,
}

/// <summary>Cada paso de un agente queda registrado (ia.acciones_agentes): qué hizo, con qué, cuánto costó y cuánto tardó.</summary>
public sealed class AccionAgente
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EjecucionId { get; init; }
    public Guid EmpresaId { get; init; }
    public string Agente { get; init; } = "";
    public string Caso { get; init; } = "";
    public int Paso { get; init; }
    public TipoAccionAgente Tipo { get; init; }

    /// <summary>Herramienta usada o modelo que respondió.</summary>
    public string Nombre { get; init; } = "";
    public string Detalle { get; init; } = "";
    public decimal CostoUsd { get; init; }
    public long DuracionMs { get; init; }
    public DateTimeOffset OcurridoEn { get; init; }
}

public interface IMemoriaCasos
{
    Task<MemoriaCaso?> BuscarAsync(Guid empresaId, string agente, string caso, CancellationToken cancellationToken);

    Task GuardarAsync(MemoriaCaso memoria, CancellationToken cancellationToken);
}

public interface IRegistroAccionesAgente
{
    Task RegistrarAsync(IReadOnlyList<AccionAgente> acciones, CancellationToken cancellationToken);
}
