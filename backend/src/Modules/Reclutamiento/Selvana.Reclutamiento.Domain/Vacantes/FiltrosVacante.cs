using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Vacantes;

/// <summary>
/// Filtros ATS de la vacante: lo que es excluyente. Un candidato que no los pasa queda como «descarte sugerido»;
/// nunca se descarta solo: una persona aprueba el descarte.
/// </summary>
public sealed class FiltrosVacante
{
    public const int MaximoCiudades = 10;

    /// <summary>Requisitos de la vacante que son excluyentes (copiados tal cual de la lista de requisitos).</summary>
    public List<string> RequisitosExcluyentes { get; set; } = [];

    /// <summary>Preguntas filtro en las que responder «No» descarta.</summary>
    public List<string> PreguntasExcluyentes { get; set; } = [];

    /// <summary>Ciudades donde debe vivir; vacía = cualquiera.</summary>
    public List<string> Ciudades { get; set; } = [];
    public int? ExperienciaMinimaMeses { get; set; }
    public string ConfiguradoPor { get; set; } = "";
    public DateTimeOffset ConfiguradoEn { get; set; }

    public bool Vacios => RequisitosExcluyentes.Count == 0 && PreguntasExcluyentes.Count == 0 && Ciudades.Count == 0 && ExperienciaMinimaMeses is null or 0;
}

public static class FiltrosErrors
{
    public static readonly Error NoEsRequisito = Error.Validation("Filtros.NoEsRequisito", "Solo puede marcar como excluyentes los requisitos y las preguntas de la vacante.");
    public static readonly Error Protegido = Error.Validation("Filtros.Protegido",
        "Un filtro no puede usar edad, género, estado civil, foto, barrio ni otras variables protegidas.");
    public static readonly Error ExperienciaInvalida = Error.Validation("Filtros.Experiencia", "La experiencia mínima va en meses, entre 0 y 480.");
    public static readonly Error DemasiadasCiudades = Error.Validation("Filtros.Ciudades", $"Elija hasta {FiltrosVacante.MaximoCiudades} ciudades.");
}

public static class ReglasFiltros
{
    /// <summary>Valida y arma los filtros contra los requisitos y las preguntas que tiene hoy la vacante.</summary>
    public static Result<FiltrosVacante> Armar(IReadOnlyList<string> requisitosVacante, IReadOnlyList<string> preguntasVacante, IEnumerable<string> requisitos,
        IEnumerable<string> preguntas, IEnumerable<string> ciudades, int? experienciaMinimaMeses, string por, DateTimeOffset ahora)
    {
        var r = requisitos.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var p = preguntas.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var c = ciudades.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (r.Any(x => !requisitosVacante.Contains(x, StringComparer.OrdinalIgnoreCase)) || p.Any(x => !preguntasVacante.Contains(x, StringComparer.OrdinalIgnoreCase)))
        {
            return FiltrosErrors.NoEsRequisito;
        }

        if (r.Concat(p).Concat(c).Any(VariablesProtegidas.Menciona))
        {
            return FiltrosErrors.Protegido;
        }

        if (experienciaMinimaMeses is < 0 or > 480)
        {
            return FiltrosErrors.ExperienciaInvalida;
        }

        if (c.Count > FiltrosVacante.MaximoCiudades)
        {
            return FiltrosErrors.DemasiadasCiudades;
        }

        return new FiltrosVacante
        {
            RequisitosExcluyentes = r,
            PreguntasExcluyentes = p,
            Ciudades = c,
            ExperienciaMinimaMeses = experienciaMinimaMeses is 0 ? null : experienciaMinimaMeses,
            ConfiguradoPor = por,
            ConfiguradoEn = ahora,
        };
    }
}
