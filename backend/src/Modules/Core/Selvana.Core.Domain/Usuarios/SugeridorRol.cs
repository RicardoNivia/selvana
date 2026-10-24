using System.Globalization;
using System.Text;

namespace Selvana.Core.Domain.Usuarios;

/// <summary>Rol sugerido para una persona, con la razón y la evidencia que la sustenta.</summary>
public sealed record SugerenciaRol(Rol Rol, string Razon, string Evidencia);

/// <summary>
/// Sugiere el rol a partir del cargo que la persona tiene en la nómina. Hoy son reglas sobre el nombre del cargo;
/// cuando la nómina traiga la jerarquía (quién reporta a quién) se sumará el número de personas a cargo.
/// Nunca sugiere Administrador: ese acceso se da a propósito.
/// </summary>
public static class SugeridorRol
{
    private static readonly string[] TalentoHumano = ["talento humano", "gestion humana", "recursos humanos", "rrhh", "personal", "nomina"];
    private static readonly string[] Seleccion = ["seleccion", "reclut", "psicolog", "analista de talento", "analista de gestion humana", "analista de rrhh", "atraccion"];
    private static readonly string[] Mando = ["jefe", "coordinador", "supervisor", "lider", "gerente", "director", "administrador de tienda", "administrador de punto", "capataz", "maestro de obra"];

    public static SugerenciaRol Sugerir(string cargo, string area, int empleados)
    {
        var texto = Normalizar($"{cargo} {area}");
        var nombreCargo = Normalizar(cargo);
        var evidencia = $"Cargo «{cargo.Trim()}» en la nómina" + (empleados > 0 ? $" · {empleados} {(empleados == 1 ? "persona" : "personas")} con ese cargo" : "");

        var dirige = Mando.Any(nombreCargo.Contains);
        if (dirige && TalentoHumano.Any(texto.Contains))
        {
            return new(Rol.GerenteRrhh, "Dirige talento humano: aprueba ternas y ve datos sensibles.", evidencia);
        }

        if (Seleccion.Any(texto.Contains) || (TalentoHumano.Any(texto.Contains) && nombreCargo.Contains("analista", StringComparison.Ordinal)))
        {
            return new(Rol.Analista, "Trabaja en selección: crea vacantes y revisa hojas de vida, sin aprobar decisiones.", evidencia);
        }

        if (dirige)
        {
            return new(Rol.Lider, "Tiene un equipo a cargo: aprueba lo de su equipo y pide vacantes.", evidencia);
        }

        return new(Rol.Empleado, "No tiene equipo a cargo ni trabaja en talento humano: consulta solo sus propios datos.", evidencia);
    }

    private static string Normalizar(string texto)
    {
        var sinTildes = new StringBuilder();
        foreach (var c in (texto ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sinTildes.Append(char.ToLowerInvariant(c));
            }
        }

        return sinTildes.ToString();
    }
}
