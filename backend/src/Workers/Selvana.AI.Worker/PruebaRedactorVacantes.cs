using System.Globalization;
using Selvana.AI.Agentes.RedactorVacantes;

namespace Selvana.AI.Worker;

/// <summary>Demo local del gateway: pide un borrador de vacante y lo muestra en consola.</summary>
internal static class PruebaRedactorVacantes
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static async Task<int> EjecutarAsync(IServiceProvider services, string frase)
    {
        if (string.IsNullOrWhiteSpace(frase))
        {
            frase = "Necesito 25 auxiliares de bodega para Funza, turno noche, con curso de montacargas.";
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"Frase: {frase}\n");

        var redactor = services.GetRequiredService<IRedactorVacantes>();
        var resultado = await redactor.RedactarAsync(new SolicitudBorradorVacante(Guid.Empty, frase), CancellationToken.None);

        if (resultado.IsFailure)
        {
            Console.WriteLine($"No se pudo: {resultado.Error.Description}");
            return 1;
        }

        var r = resultado.Value;
        var b = r.Borrador;
        Console.WriteLine($"Respondió: {r.Proveedor}/{r.Modelo} · costo USD {r.CostoUsd.ToString("0.000000", CultureInfo.InvariantCulture)}\n");
        Console.WriteLine($"{b.Titulo}");
        Console.WriteLine($"{b.Cargo} · {b.Ciudad} · {b.Cupos} cupos · {b.Jornada}");
        Console.WriteLine($"Salario: {b.RangoSalarial.MinimoCop.ToString("C0", Colombia)} – {b.RangoSalarial.MaximoCop.ToString("C0", Colombia)} ({b.RangoSalarial.Justificacion})\n");
        Console.WriteLine(b.Descripcion + "\n");
        Imprimir("Requisitos", b.Requisitos);
        Imprimir("Competencias", b.Competencias);
        Imprimir("Preguntas filtro para WhatsApp", b.PreguntasFiltro);
        Imprimir("Por qué la IA lo propone", b.Razones.Select(x => $"{x.Texto} [{x.Evidencia}]"));
        Imprimir("Supuestos", b.Supuestos);
        if (r.RequisitosRetiradosPorSesgo.Count > 0)
        {
            Imprimir("Retirados por variables protegidas", r.RequisitosRetiradosPorSesgo);
        }

        return 0;
    }

    private static void Imprimir(string titulo, IEnumerable<string> items)
    {
        Console.WriteLine(titulo + ":");
        foreach (var item in items)
        {
            Console.WriteLine($"  - {item}");
        }

        Console.WriteLine();
    }
}
