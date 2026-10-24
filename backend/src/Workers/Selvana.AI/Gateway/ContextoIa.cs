using Microsoft.Extensions.AI;

namespace Selvana.AI.Gateway;

/// <summary>Quién pide la respuesta: se usa para medir costo por empresa y por agente, y para proteger datos personales.</summary>
public sealed record ContextoIa(Guid EmpresaId, string Agente, bool ContieneDatosPersonales)
{
    private const string Clave = "selvana.contexto";

    public static ContextoIa SinIdentificar { get; } = new(Guid.Empty, "sin-agente", ContieneDatosPersonales: true);

    public static ContextoIa De(ChatOptions? options) =>
        options?.AdditionalProperties?.TryGetValue(Clave, out var valor) == true && valor is ContextoIa contexto
            ? contexto
            : SinIdentificar;

    public ChatOptions AplicarA(ChatOptions? options)
    {
        var resultado = options?.Clone() ?? new ChatOptions();
        resultado.AdditionalProperties ??= [];
        resultado.AdditionalProperties[Clave] = this;
        return resultado;
    }
}
