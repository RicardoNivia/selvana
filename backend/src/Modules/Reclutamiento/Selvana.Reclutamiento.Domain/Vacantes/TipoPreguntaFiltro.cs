using Selvana.Reclutamiento.Domain.Candidatos;

namespace Selvana.Reclutamiento.Domain.Vacantes;

/// <summary>Cómo responde el candidato una pregunta filtro.</summary>
public enum TipoPreguntaFiltro
{
    /// <summary>Sí o no («¿Tienes curso de alturas vigente?»).</summary>
    SiNo,

    /// <summary>Un número («¿Cuántos años de experiencia tienes como operario de montacargas?»).</summary>
    Numero,

    /// <summary>Un valor en pesos colombianos («¿Cuál es tu aspiración salarial mensual en pesos?»).</summary>
    Pesos,
}

/// <summary>
/// El tipo de respuesta sale de cómo está escrita la pregunta, sin guardarlo aparte: así lo que escribe una persona
/// en el formulario y lo que redacta la IA se responden igual en el portal.
/// </summary>
public static class PreguntasFiltro
{
    private static readonly string[] Salario = ["salari", "aspiracion", "expectativa", "cuanto esperas ganar", "pretension"];

    public static TipoPreguntaFiltro Tipo(string pregunta)
    {
        var p = Normalizacion.Simplificar(pregunta).TrimStart('¿', ' ');
        if (Salario.Any(p.Contains))
        {
            return TipoPreguntaFiltro.Pesos;
        }

        return p.StartsWith("cuant", StringComparison.Ordinal) ? TipoPreguntaFiltro.Numero : TipoPreguntaFiltro.SiNo;
    }
}
