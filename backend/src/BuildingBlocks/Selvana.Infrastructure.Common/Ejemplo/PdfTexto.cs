using System.Globalization;
using System.Text;

namespace Selvana.Infrastructure.Common.Ejemplo;

/// <summary>PDF de una página solo con texto (Helvetica, WinAnsi): lo justo para los archivos de los datos de ejemplo.</summary>
public static class PdfTexto
{
    public static byte[] Crear(IEnumerable<string> lineas)
    {
        static string Escapar(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal).Replace("—", "-", StringComparison.Ordinal).Replace("·", "-", StringComparison.Ordinal);

        var contenido = new StringBuilder("BT /F1 11 Tf 56 790 Td 15 TL\n");
        foreach (var l in lineas)
        {
            contenido.Append('(').Append(Escapar(l)).Append(") Tj T*\n");
        }

        contenido.Append("ET");
        var latin1 = Encoding.Latin1;
        var cuerpo = latin1.GetString(latin1.GetBytes(contenido.ToString()));
        string[] objetos =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {latin1.GetByteCount(cuerpo)} >>\nstream\n{cuerpo}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var posiciones = new List<int>();
        for (var i = 0; i < objetos.Length; i++)
        {
            posiciones.Add(latin1.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objetos[i]}\nendobj\n");
        }

        var xref = latin1.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objetos.Length + 1}\n0000000000 65535 f \n");
        foreach (var pos in posiciones)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{pos:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objetos.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return latin1.GetBytes(pdf.ToString());
    }
}
