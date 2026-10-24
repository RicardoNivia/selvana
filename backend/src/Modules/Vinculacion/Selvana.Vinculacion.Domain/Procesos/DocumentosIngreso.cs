using Selvana.SharedKernel.Results;

namespace Selvana.Vinculacion.Domain.Procesos;

public enum TipoDocumentoIngreso
{
    Cedula,
    CertificadoBancario,
    CertificadoEps,
    CertificadoPension,
    SoporteEstudios,
    CertificadosLaborales,

    /// <summary>Carta de la institución educativa (contrato de aprendizaje).</summary>
    CartaInstitucion,

    /// <summary>Procuraduría, Contraloría y Policía: los carga la empresa.</summary>
    Antecedentes,

    /// <summary>Examen médico de ingreso: lo carga la empresa; el concepto es dato sensible (cifrado) y nunca pasa por la IA.</summary>
    ExamenMedico,
}

public enum EstadoDocumento
{
    Pendiente,

    /// <summary>La IA está leyendo el documento.</summary>
    Leyendo,
    PorRevisar,
    Aprobado,
    Rechazado,
}

public enum ResultadoAntecedentes
{
    SinAnotaciones,
    ConAnotaciones,
}

public enum ConceptoMedico
{
    Apto,
    AptoConRestricciones,
    NoApto,
}

public sealed class CampoLeido
{
    public string Campo { get; init; } = "";
    public string Valor { get; init; } = "";
}

/// <summary>Lo que leyó la IA de un documento: es una sugerencia hasta que RRHH lo aprueba.</summary>
public sealed class LecturaDocumento
{
    public string TipoDetectado { get; init; } = "";
    public bool Legible { get; init; }
    public List<CampoLeido> Campos { get; init; } = [];
    public List<string> Observaciones { get; init; } = [];
    public List<RazonOferta> Razones { get; init; } = [];

    /// <summary>Calculado por reglas (no por la IA): el número o el nombre del documento coinciden con los de la persona.</summary>
    public bool? CoincideTitular { get; init; }
    public bool TipoCorrecto { get; init; }
    public string? Modelo { get; init; }
    public decimal CostoUsd { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset LeidoEn { get; init; }
}

public sealed class DocumentoIngreso
{
    public TipoDocumentoIngreso Tipo { get; init; }
    public bool Obligatorio { get; init; }

    /// <summary>Lo sube el candidato por su enlace o lo carga la empresa (antecedentes y examen médico).</summary>
    public bool LoCargaEmpresa { get; init; }
    public EstadoDocumento Estado { get; set; }
    public Guid? ArchivoId { get; set; }
    public string? NombreArchivo { get; set; }
    public string? SubidoPor { get; set; }
    public DateTimeOffset? SubidoEn { get; set; }
    public int Versiones { get; set; }
    public LecturaDocumento? Lectura { get; set; }
    public string? RevisadoPor { get; set; }
    public DateTimeOffset? RevisadoEn { get; set; }
    public string? MotivoRechazo { get; set; }
    public ResultadoAntecedentes? Antecedentes { get; set; }

    /// <summary>Concepto médico cifrado (lo cifra la aplicación); el dominio nunca lo ve en claro.</summary>
    public string? ConceptoCifrado { get; set; }

    /// <summary>Sin el detalle: si el concepto pide revisión (apto con restricciones o no apto). Se puede mostrar sin revelar el concepto.</summary>
    public bool? ConceptoRequiereRevision { get; set; }
}

public static class DocumentosErrors
{
    public static readonly Error NoEnDocumentos = Error.Conflict("Documentos.NoEnEtapa", "Los documentos se piden después de aceptar la oferta.");
    public static readonly Error NoExiste = Error.NotFound("Documentos.NoExiste", "Ese documento no está en la lista de ingreso.");
    public static readonly Error LoCargaEmpresa = Error.Conflict("Documentos.LoCargaEmpresa", "Este documento lo carga la empresa, no el candidato.");
    public static readonly Error LoCargaCandidato = Error.Conflict("Documentos.LoCargaCandidato", "Este documento lo sube el candidato desde su enlace.");
    public static readonly Error YaAprobado = Error.Conflict("Documentos.YaAprobado", "Ese documento ya está aprobado.");
    public static readonly Error SinArchivo = Error.Conflict("Documentos.SinArchivo", "Aún no hay archivo para revisar.");
    public static readonly Error Leyendo = Error.Conflict("Documentos.Leyendo", "La IA está leyendo el documento: espere un momento.");
    public static readonly Error SinMotivo = Error.Validation("Documentos.SinMotivo", "Diga por qué rechaza el documento: el candidato lo verá para corregirlo.");
    public static readonly Error SinResultado = Error.Validation("Documentos.SinResultado", "Indique el resultado (antecedentes o concepto médico).");
}

/// <summary>Qué documentos pide cada tipo de contrato y quién los carga.</summary>
public static class RequisitosDocumentales
{
    public static string Nombre(TipoDocumentoIngreso t) => t switch
    {
        TipoDocumentoIngreso.Cedula => "Cédula de ciudadanía",
        TipoDocumentoIngreso.CertificadoBancario => "Certificado bancario",
        TipoDocumentoIngreso.CertificadoEps => "Certificado de afiliación a la EPS",
        TipoDocumentoIngreso.CertificadoPension => "Certificado del fondo de pensiones",
        TipoDocumentoIngreso.SoporteEstudios => "Diplomas o actas de grado",
        TipoDocumentoIngreso.CertificadosLaborales => "Certificados laborales",
        TipoDocumentoIngreso.CartaInstitucion => "Carta de la institución educativa",
        TipoDocumentoIngreso.Antecedentes => "Antecedentes (Procuraduría, Contraloría y Policía)",
        TipoDocumentoIngreso.ExamenMedico => "Examen médico de ingreso",
        _ => t.ToString(),
    };

    public static List<DocumentoIngreso> Para(TipoContrato tipo)
    {
        var aprendiz = tipo == TipoContrato.Aprendizaje;
        DocumentoIngreso D(TipoDocumentoIngreso t, bool obligatorio, bool empresa = false) => new() { Tipo = t, Obligatorio = obligatorio, LoCargaEmpresa = empresa };
        var lista = new List<DocumentoIngreso>
        {
            D(TipoDocumentoIngreso.Cedula, true),
            D(TipoDocumentoIngreso.CertificadoBancario, true),
            D(TipoDocumentoIngreso.CertificadoEps, true),
            D(TipoDocumentoIngreso.CertificadoPension, !aprendiz),
            D(TipoDocumentoIngreso.SoporteEstudios, false),
            D(TipoDocumentoIngreso.CertificadosLaborales, false),
        };
        if (aprendiz)
        {
            lista.Add(D(TipoDocumentoIngreso.CartaInstitucion, true));
        }

        lista.Add(D(TipoDocumentoIngreso.Antecedentes, true, empresa: true));
        lista.Add(D(TipoDocumentoIngreso.ExamenMedico, true, empresa: true));
        return lista;
    }

    /// <summary>
    /// ¿El documento es de la persona? Por reglas: algún número leído coincide con su documento o el nombre leído contiene
    /// sus apellidos. null si la IA no encontró número ni nombre.
    /// </summary>
    public static bool? CoincideTitular(IEnumerable<CampoLeido> campos, string nombre, string documento)
    {
        var lista = campos.ToList();
        var digitos = ProcesoVinculacion.SoloDigitos(documento);
        var numeros = lista.Select(c => ProcesoVinculacion.SoloDigitos(c.Valor)).Where(n => n.Length >= 5).ToList();
        if (digitos.Length > 0 && numeros.Contains(digitos))
        {
            return true;
        }

        var palabras = Simple(nombre).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length > 2).ToList();
        var nombres = lista.Where(c => c.Campo.Contains("nombre", StringComparison.OrdinalIgnoreCase) || c.Campo.Contains("titular", StringComparison.OrdinalIgnoreCase))
            .Select(c => Simple(c.Valor)).Where(v => v.Length > 0).ToList();
        if (nombres.Count > 0 && palabras.Count >= 2)
        {
            // Basta con que coincidan dos palabras del nombre (por ejemplo, los dos apellidos)
            return nombres.Any(n => palabras.Count(p => n.Split(' ').Contains(p)) >= 2);
        }

        return numeros.Count > 0 && digitos.Length > 0 ? false : null;
    }

    private static string Simple(string? texto)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in (texto ?? "").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.IsLetter(c) ? c : ' ');
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
