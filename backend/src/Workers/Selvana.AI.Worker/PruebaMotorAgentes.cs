using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Options;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Motor;

namespace Selvana.AI.Worker;

/// <summary>
/// Demo local del motor de agentes: un asistente con una herramienta de lectura (salario mínimo) y una de escritura
/// (proponer la vacante) que no se ejecuta, queda propuesta. Corre dos veces sobre el mismo caso para mostrar la memoria.
/// </summary>
internal static class PruebaMotorAgentes
{
    private const string Caso = "demo:vacante-bodega";

    public static async Task<int> EjecutarAsync(IServiceProvider services, string mensaje)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var motor = services.GetRequiredService<IMotorAgentes>();
        var minimo = services.GetRequiredService<IOptions<RedactorVacantesOptions>>().Value.SalarioMinimoCop;
        var agente = new DefinicionAgente("asistente-demo",
            "Eres el asistente de vacantes de Selvana. Ayudas a un líder a abrir una vacante con un salario legal.",
            [
                HerramientaAgente.Lectura(() => minimo, "salario_minimo", "Salario mínimo mensual legal vigente en Colombia, en pesos"),
                HerramientaAgente.Escritura(
                    ([Description("Cargo")] string cargo, [Description("Ciudad")] string ciudad, [Description("Cupos")] int cupos, [Description("Salario mensual en pesos")] decimal salario) => 0,
                    "proponer_vacante", "Propone abrir la vacante; una persona la aprueba en la bandeja"),
            ], ContieneDatosPersonales: false);

        var mensajes = new[]
        {
            string.IsNullOrWhiteSpace(mensaje) ? "Necesito 5 auxiliares de bodega en Funza pagando el mínimo. Recuerda los cupos." : mensaje,
            "¿Cuántos cupos te había pedido?",
        };

        foreach (var texto in mensajes)
        {
            Console.WriteLine($"\nLíder: {texto}");
            var r = await motor.EjecutarAsync(agente, new SolicitudAgente(EjecutorEvaluacion.EmpresaEvaluacion, Caso, texto), CancellationToken.None);
            if (r.IsFailure)
            {
                Console.WriteLine($"No se pudo: {r.Error.Description}");
                return 1;
            }

            var v = r.Value;
            Console.WriteLine($"Agente: {v.Respuesta}");
            foreach (var p in v.Propuestas)
            {
                Console.WriteLine($"  Propuesta para aprobar: {p.Herramienta}({string.Join(", ", p.Argumentos.Select(a => $"{a.Key}: {a.Value}"))})");
            }

            Console.WriteLine($"  Memoria del caso: {string.Join(" · ", v.Memoria.Select(m => $"{m.Key} = {m.Value}"))}");
            Console.WriteLine($"  {v.Pasos} pasos · {v.Modelos} · USD {v.CostoUsd.ToString("0.000000", CultureInfo.InvariantCulture)} · ejecución {v.EjecucionId}");
        }

        return 0;
    }
}
