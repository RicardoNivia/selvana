using System.Globalization;
using System.Text;

namespace Selvana.Integrations.Nomina;

/// <summary>
/// Archivo programado o carga manual: un CSV de empleados activos (separado por coma o punto y coma).
/// Acepta los nombres de columna con o sin tildes, mayúsculas o espacios.
/// </summary>
public sealed class ArchivoCsvConector : IConectorNomina
{
    private static readonly Dictionary<string, string[]> Columnas = new()
    {
        ["documento"] = ["documento", "cedula", "numero_documento", "identificacion"],
        ["cargo_codigo"] = ["cargo_codigo", "codigo_cargo"],
        ["cargo"] = ["cargo", "cargo_nombre", "nombre_cargo"],
        ["area"] = ["area", "departamento", "dependencia"],
        ["centro_costo_codigo"] = ["centro_costo_codigo", "codigo_centro_costo", "cc_codigo"],
        ["centro_costo"] = ["centro_costo", "centro_de_costo", "centro_costo_nombre"],
        ["sede"] = ["sede", "ciudad", "sucursal"],
    };

    public string Nombre => "Archivo CSV";

    public async Task<LecturaNomina> LeerAsync(Stream contenido, CancellationToken cancellationToken)
    {
        using var lector = new StreamReader(contenido, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var encabezado = await lector.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(encabezado))
        {
            return new LecturaNomina([], ["El archivo está vacío."]);
        }

        var separador = encabezado.Count(c => c == ';') > encabezado.Count(c => c == ',') ? ';' : ',';
        var nombres = Dividir(encabezado, separador).Select(Normalizar).ToList();
        var indices = new Dictionary<string, int>();
        var faltantes = new List<string>();
        foreach (var (clave, alias) in Columnas)
        {
            var i = nombres.FindIndex(n => alias.Contains(n));
            if (i < 0)
            {
                faltantes.Add(clave);
            }
            else
            {
                indices[clave] = i;
            }
        }

        if (faltantes.Count > 0)
        {
            return new LecturaNomina([], [$"Faltan columnas: {string.Join(", ", faltantes)}. Revise el encabezado del archivo."]);
        }

        var empleados = new List<EmpleadoNomina>();
        var errores = new List<string>();
        var fila = 1;
        while (await lector.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } linea)
        {
            fila++;
            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            var campos = Dividir(linea, separador);
            string Campo(string clave) => indices[clave] < campos.Count ? campos[indices[clave]].Trim() : "";

            var documento = Campo("documento");
            var cargo = Campo("cargo");
            if (documento.Length == 0 || cargo.Length == 0)
            {
                errores.Add($"Fila {fila}: falta el documento o el cargo.");
                continue;
            }

            empleados.Add(new EmpleadoNomina(documento, Campo("cargo_codigo"), cargo, Campo("area"),
                Campo("centro_costo_codigo"), Campo("centro_costo"), Campo("sede")));
        }

        return new LecturaNomina(empleados, errores);
    }

    private static string Normalizar(string nombre)
    {
        var sinTildes = new string(nombre.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return sinTildes.Replace(' ', '_').Replace('-', '_');
    }

    private static List<string> Dividir(string linea, char separador)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (c == '"')
            {
                if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else
                {
                    entreComillas = !entreComillas;
                }
            }
            else if (c == separador && !entreComillas)
            {
                campos.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }

        campos.Add(actual.ToString());
        return campos;
    }
}
