using System.Globalization;
using System.Text;

namespace Selvana.Integrations.Nomina;

/// <summary>Contrato canónico de un ingreso: lo que cualquier ERP de nómina necesita para crear al empleado.</summary>
public sealed record AltaEmpleadoNomina(
    string Documento, string Nombre, string Correo, string Celular, string Ciudad, string Cargo, string Area, string CentroCosto, string Sede,
    string TipoContrato, decimal SalarioCop, bool AuxilioTransporte, DateOnly FechaIngreso, DateOnly? FechaFin, int PeriodoPruebaDias,
    string Eps, string FondoPension, string Arl, int? ClaseRiesgoArl, string CajaCompensacion, string Banco, string TipoCuenta, string NumeroCuenta,
    string? EmpresaUsuaria);

/// <summary>
/// Archivo de alta para cualquier ERP (nivel «archivo programado o descarga manual», ADR 0004): CSV en UTF-8 con BOM,
/// separado por punto y coma (lo abre Excel en español) y fechas AAAA-MM-DD. El conector directo llega con las integraciones.
/// </summary>
public static class ArchivoCsvAltas
{
    private static readonly string[] Encabezado =
    [
        "documento", "nombre", "correo", "celular", "ciudad", "cargo", "area", "centro_costo", "sede", "tipo_contrato", "salario", "auxilio_transporte",
        "fecha_ingreso", "fecha_fin", "periodo_prueba_dias", "eps", "fondo_pension", "arl", "clase_riesgo_arl", "caja_compensacion", "banco", "tipo_cuenta",
        "numero_cuenta", "empresa_usuaria",
    ];

    public static byte[] Generar(IEnumerable<AltaEmpleadoNomina> altas)
    {
        ArgumentNullException.ThrowIfNull(altas);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Encabezado));
        foreach (var a in altas)
        {
            string[] valores =
            [
                a.Documento, a.Nombre, a.Correo, a.Celular, a.Ciudad, a.Cargo, a.Area, a.CentroCosto, a.Sede, a.TipoContrato,
                a.SalarioCop.ToString("0", CultureInfo.InvariantCulture), a.AuxilioTransporte ? "si" : "no", a.FechaIngreso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                a.FechaFin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", a.PeriodoPruebaDias.ToString(CultureInfo.InvariantCulture), a.Eps, a.FondoPension,
                a.Arl, a.ClaseRiesgoArl?.ToString(CultureInfo.InvariantCulture) ?? "", a.CajaCompensacion, a.Banco, a.TipoCuenta, a.NumeroCuenta, a.EmpresaUsuaria ?? "",
            ];
            sb.AppendLine(string.Join(';', valores.Select(Celda)));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    /// <summary>Entre comillas si hace falta; y sin fórmulas (una celda que empieza con = + - @ se escapa).</summary>
    private static string Celda(string valor)
    {
        var v = (valor ?? "").Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        if (v.Length > 0 && "=+-@".Contains(v[0], StringComparison.Ordinal))
        {
            v = "'" + v;
        }

        return v.Contains(';', StringComparison.Ordinal) || v.Contains('"', StringComparison.Ordinal) ? $"\"{v.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : v;
    }
}
