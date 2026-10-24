using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Selvana.AI.Gateway;

/// <summary>Crea el cliente de chat de un modelo. Agregar un proveedor = una implementación nueva, sin tocar la cadena.</summary>
public interface IProveedorModelos
{
    string Nombre { get; }

    IChatClient CrearCliente(string modelo);
}

internal sealed class GeminiProveedor(IOptions<IaOptions> options) : IProveedorModelos
{
    public string Nombre => "Gemini";

    public IChatClient CrearCliente(string modelo) =>
        new Google.GenAI.Client(apiKey: options.Value.ClaveDe(Nombre)).AsIChatClient(modelo);
}

internal sealed class AnthropicProveedor(IOptions<IaOptions> options) : IProveedorModelos
{
    public string Nombre => "Anthropic";

    public IChatClient CrearCliente(string modelo) =>
        new AnthropicClient { ApiKey = options.Value.ClaveDe(Nombre) }.AsIChatClient(modelo);
}

internal static class IaOptionsExtensions
{
    public static string ClaveDe(this IaOptions options, string proveedor) =>
        options.Proveedores.TryGetValue(proveedor, out var p) && !string.IsNullOrWhiteSpace(p.ApiKey)
            ? p.ApiKey
            : throw new InvalidOperationException($"Falta la clave 'Ia:Proveedores:{proveedor}:ApiKey'.");
}
