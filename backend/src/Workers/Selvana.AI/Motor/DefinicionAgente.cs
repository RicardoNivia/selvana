using Microsoft.Extensions.AI;

namespace Selvana.AI.Motor;

/// <summary>Qué tanto puede hacer una herramienta: leer se ejecuta, escribir queda propuesto para que una persona lo apruebe.</summary>
public enum RiesgoHerramienta
{
    Lectura,
    Escritura,
}

/// <summary>Una herramienta que el agente puede usar. Las de escritura nunca se ejecutan solas: "la IA trabaja, el humano decide".</summary>
public sealed record HerramientaAgente(AIFunction Funcion, RiesgoHerramienta Riesgo)
{
    public static HerramientaAgente Lectura(Delegate metodo, string nombre, string descripcion) =>
        new(AIFunctionFactory.Create(metodo, nombre, descripcion), RiesgoHerramienta.Lectura);

    /// <summary>La función no se invoca: sus argumentos son la propuesta que va a la bandeja de aprobaciones.</summary>
    public static HerramientaAgente Escritura(Delegate metodo, string nombre, string descripcion) =>
        new(AIFunctionFactory.Create(metodo, nombre, descripcion), RiesgoHerramienta.Escritura);

    public string Nombre => Funcion.Name;
}

/// <summary>Un agente: sus instrucciones, sus herramientas y si trabaja con datos personales (entonces nunca usa modelos gratis).</summary>
public sealed record DefinicionAgente(
    string Nombre,
    string Instrucciones,
    IReadOnlyList<HerramientaAgente> Herramientas,
    bool ContieneDatosPersonales,
    int MaximoPasos = 6);

/// <summary>Trabajo pedido al agente sobre un caso ("vacante:…", "candidato:…"): la memoria se guarda por caso.</summary>
public sealed record SolicitudAgente(Guid EmpresaId, string Caso, string Mensaje);

/// <summary>Lo que el agente quiere hacer y espera aprobación humana.</summary>
public sealed record AccionPropuesta(string Herramienta, IReadOnlyDictionary<string, object?> Argumentos);

public sealed record ResultadoAgente(
    Guid EjecucionId,
    string Respuesta,
    IReadOnlyList<AccionPropuesta> Propuestas,
    IReadOnlyDictionary<string, string> Memoria,
    int Pasos,
    string Modelos,
    decimal CostoUsd);
