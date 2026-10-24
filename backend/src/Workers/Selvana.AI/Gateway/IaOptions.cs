namespace Selvana.AI.Gateway;

/// <summary>Configuración de la plataforma de IA (sección "Ia"). Las claves van en user-secrets o variables de entorno.</summary>
public sealed class IaOptions
{
    public const string Section = "Ia";

    /// <summary>Clave por proveedor: "Gemini", "Anthropic". El nombre coincide con <see cref="IProveedorModelos.Nombre"/>.</summary>
    public Dictionary<string, ProveedorOptions> Proveedores { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Modelos en orden de preferencia: primero los gratis, luego los de pago, al final el respaldo.</summary>
    public List<EslabonModeloOptions> Cadena { get; init; } = [];

    /// <summary>Tiempo que se omite un modelo después de agotar su cuota (429).</summary>
    public TimeSpan EsperaTrasCuotaAgotada { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Tiempo que se omite un modelo que no existe o rechaza la clave (401, 403, 404).</summary>
    public TimeSpan EsperaTrasModeloNoDisponible { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan TiempoMaximoPorIntento { get; init; } = TimeSpan.FromSeconds(90);
}

public sealed class ProveedorOptions
{
    public string ApiKey { get; init; } = "";
}

public sealed class EslabonModeloOptions
{
    public string Proveedor { get; init; } = "";
    public string Modelo { get; init; } = "";
    public NivelModelo Nivel { get; init; } = NivelModelo.Pago;
    public decimal PrecioEntradaUsdPorMillon { get; init; }
    public decimal PrecioSalidaUsdPorMillon { get; init; }

    public decimal CalcularCostoUsd(long tokensEntrada, long tokensSalida) =>
        ((tokensEntrada * PrecioEntradaUsdPorMillon) + (tokensSalida * PrecioSalidaUsdPorMillon)) / 1_000_000m;

    public override string ToString() => $"{Proveedor}/{Modelo}";
}

/// <summary>Los modelos gratis pueden usar el contenido para entrenar: no reciben datos personales.</summary>
public enum NivelModelo
{
    Gratis,
    Pago,
}
