namespace Selvana.AI.Gateway;

/// <summary>Un intento contra un modelo: éxito o falla, tokens y costo. Base para medir el costo de IA por uso y por cliente.</summary>
public sealed class UsoModelo
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmpresaId { get; init; }
    public string Agente { get; init; } = "";
    public string Proveedor { get; init; } = "";
    public string Modelo { get; init; } = "";
    public NivelModelo Nivel { get; init; }
    public int Intento { get; init; }
    public bool Exito { get; init; }
    public string? Falla { get; init; }
    public long TokensEntrada { get; init; }
    public long TokensSalida { get; init; }
    public decimal CostoUsd { get; init; }
    public long DuracionMs { get; init; }
    public DateTimeOffset OcurridoEn { get; init; }
}

public interface IRegistroUsoIa
{
    Task RegistrarAsync(UsoModelo uso, CancellationToken cancellationToken);
}
