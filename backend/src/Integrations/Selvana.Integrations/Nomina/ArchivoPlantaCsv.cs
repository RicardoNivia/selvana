using System.Globalization;
using System.Text;

namespace Selvana.Integrations.Nomina;

/// <summary>Contrato canónico: un empleado activo con lo necesario para abrir su expediente (planta que ya existía).</summary>
public sealed record EmpleadoPlanta(
    int Fila, string Documento, string Nombre, string Cargo, string Area, string CentroCosto, string Sede, string TipoContrato, decimal SalarioCop,
    DateOnly FechaIngreso, DateOnly? FechaFinContrato, string Correo, string Celular, string Ciudad, string Eps, string FondoPension, string Arl,
    int? ClaseRiesgoArl, string CajaCompensacion);

public sealed record LecturaPlanta(IReadOnlyList<EmpleadoPlanta> Empleados, IReadOnlyList<string> Errores);

public interface ILectorPlanta
{
    Task<LecturaPlanta> LeerAsync(Stream contenido, CancellationToken cancellationToken);
}

/// <summary>
/// Archivo de planta en CSV (coma o punto y coma): documento, nombre, cargo, salario y fecha de ingreso son obligatorios;
/// el resto (área, sede, contrato, afiliaciones, contacto) es opcional. Acepta encabezados con o sin tildes y fechas dd/mm/aaaa o aaaa-mm-dd.
/// </summary>
public sealed class ArchivoPlantaCsv : ILectorPlanta
{
    public const int FilasMaximas = 5_000;
    public const string ContratoPorDefecto = "Término indefinido";

    private static readonly Dictionary<string, string[]> Columnas = new()
    {
        ["documento"] = ["documento", "cedula", "numero_documento", "identificacion"],
        ["nombre"] = ["nombre", "nombres_y_apellidos", "nombre_completo", "empleado"],
        ["cargo"] = ["cargo", "cargo_nombre", "nombre_cargo"],
        ["salario"] = ["salario", "salario_basico", "sueldo"],
        ["fecha_ingreso"] = ["fecha_ingreso", "ingreso", "fecha_de_ingreso"],
        ["area"] = ["area", "departamento", "dependencia"],
        ["centro_costo"] = ["centro_costo", "centro_de_costo", "centro_costo_nombre"],
        ["sede"] = ["sede", "sucursal"],
        ["tipo_contrato"] = ["tipo_contrato", "contrato", "tipo_de_contrato"],
        ["fecha_fin"] = ["fecha_fin", "fecha_fin_contrato", "fecha_terminacion"],
        ["correo"] = ["correo", "email", "correo_electronico"],
        ["celular"] = ["celular", "telefono", "movil"],
        ["ciudad"] = ["ciudad", "municipio"],
        ["eps"] = ["eps"],
        ["pension"] = ["pension", "fondo_pension", "afp", "fondo_de_pension"],
        ["arl"] = ["arl"],
        ["clase_riesgo"] = ["clase_riesgo", "riesgo_arl", "clase_de_riesgo"],
        ["caja"] = ["caja", "caja_compensacion", "caja_de_compensacion", "ccf"],
    };

    private static readonly string[] Obligatorias = ["documento", "nombre", "cargo", "salario", "fecha_ingreso"];
    private static readonly string[] FormatosFecha = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy"];

    public async Task<LecturaPlanta> LeerAsync(Stream contenido, CancellationToken cancellationToken)
    {
        using var lector = new StreamReader(contenido, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var encabezado = await lector.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(encabezado))
        {
            return new LecturaPlanta([], ["El archivo está vacío."]);
        }

        var separador = encabezado.Count(c => c == ';') > encabezado.Count(c => c == ',') ? ';' : ',';
        var nombres = ArchivoCsvConector.Dividir(encabezado, separador).Select(ArchivoCsvConector.Normalizar).ToList();
        var indices = Columnas.Select(c => (c.Key, Indice: nombres.FindIndex(n => c.Value.Contains(n)))).Where(x => x.Indice >= 0)
            .ToDictionary(x => x.Key, x => x.Indice);
        var faltantes = Obligatorias.Where(o => !indices.ContainsKey(o)).ToList();
        if (faltantes.Count > 0)
        {
            return new LecturaPlanta([], [$"Faltan columnas: {string.Join(", ", faltantes)}. Use la plantilla o revise el encabezado del archivo."]);
        }

        var empleados = new List<EmpleadoPlanta>();
        var errores = new List<string>();
        var fila = 1;
        while (await lector.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } linea)
        {
            fila++;
            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            if (empleados.Count + errores.Count >= FilasMaximas)
            {
                errores.Add($"El archivo pasa de {FilasMaximas} filas: divida la planta en varios archivos.");
                break;
            }

            var campos = ArchivoCsvConector.Dividir(linea, separador);
            string Campo(string clave) => indices.TryGetValue(clave, out var i) && i < campos.Count ? campos[i].Trim() : "";

            var documento = new string(Campo("documento").Where(char.IsLetterOrDigit).ToArray());
            var nombre = Campo("nombre");
            var cargo = Campo("cargo");
            var salario = Pesos(Campo("salario"));
            var ingreso = Fecha(Campo("fecha_ingreso"));
            var problemas = new List<string>();
            if (documento.Length == 0) problemas.Add("falta el documento");
            if (nombre.Length == 0) problemas.Add("falta el nombre");
            if (cargo.Length == 0) problemas.Add("falta el cargo");
            if (salario is null or <= 0) problemas.Add("el salario no es un número");
            if (ingreso is null) problemas.Add("la fecha de ingreso no se entiende (use dd/mm/aaaa)");
            var fin = Campo("fecha_fin") is { Length: > 0 } f ? Fecha(f) : null;
            if (Campo("fecha_fin").Length > 0 && fin is null) problemas.Add("la fecha de fin no se entiende");
            int? clase = int.TryParse(Campo("clase_riesgo"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) && c is >= 1 and <= 5 ? c : null;
            if (problemas.Count > 0)
            {
                errores.Add($"Fila {fila}: {string.Join(", ", problemas)}.");
                continue;
            }

            empleados.Add(new EmpleadoPlanta(fila, documento, nombre, cargo, Campo("area"), Campo("centro_costo"), Campo("sede"),
                Campo("tipo_contrato") is { Length: > 0 } t ? t : ContratoPorDefecto, salario!.Value, ingreso!.Value, fin, Campo("correo"), Campo("celular"),
                Campo("ciudad"), Campo("eps"), Campo("pension"), Campo("arl"), clase, Campo("caja")));
        }

        return new LecturaPlanta(empleados, errores);
    }

    /// <summary>«$1.900.000», «1900000» o «1,900,000»: solo cuentan los dígitos (los salarios en pesos no llevan centavos).</summary>
    internal static decimal? Pesos(string texto)
    {
        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        return digitos.Length is > 0 and <= 12 ? decimal.Parse(digitos, CultureInfo.InvariantCulture) : null;
    }

    internal static DateOnly? Fecha(string texto) =>
        DateOnly.TryParseExact(texto.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;
}
