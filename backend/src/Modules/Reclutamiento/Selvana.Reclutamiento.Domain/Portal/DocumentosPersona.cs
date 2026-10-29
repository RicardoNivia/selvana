using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Portal;

/// <summary>A qué proceso entregó la persona un documento suyo y cuándo.</summary>
public sealed class EnvioDocumento
{
    public Guid ProcesoId { get; init; }
    public string Empresa { get; init; } = "";
    public DateTimeOffset En { get; init; }
}

/// <summary>
/// P4 · Un documento de la persona en su portal (cédula, certificados…): lo sube una vez, cifrado, y lo entrega a los procesos
/// que se lo pidan. Hay uno por tipo: subir otro del mismo tipo lo reemplaza.
/// </summary>
public sealed class DocumentoPersona
{
    public Guid Id { get; init; }

    /// <summary>Clave del tipo (Cedula, CertificadoBancario…), la misma de los documentos de ingreso.</summary>
    public string Tipo { get; init; } = "";
    public Guid ArchivoId { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string TipoContenido { get; set; } = "";
    public DateOnly? VenceEn { get; set; }
    public DateTimeOffset SubidoEn { get; set; }
    public List<EnvioDocumento> Envios { get; set; } = [];
}

public static class DocumentosPersonaErrors
{
    public static readonly Error TipoInvalido = Error.Validation("MisDocumentos.Tipo", "Elige qué documento es.");
    public static readonly Error NoExiste = Error.NotFound("MisDocumentos.NoExiste", "Ese documento ya no está en tu portal. Súbelo de nuevo.");
    public static readonly Error Vencido = Error.Validation("MisDocumentos.Vencido", "Ese documento ya venció. Sube uno vigente antes de enviarlo.");
}

/// <summary>Los documentos que la persona puede guardar en su portal. Las claves son las de los documentos de ingreso.</summary>
public static class TiposDocumentoPersona
{
    public static readonly IReadOnlyList<(string Clave, string Nombre, bool Vence)> Todos =
    [
        ("Cedula", "Cédula de ciudadanía", false),
        ("CertificadoBancario", "Certificado bancario", true),
        ("CertificadoEps", "Certificado de afiliación a EPS", true),
        ("CertificadoPension", "Certificado de fondo de pensiones", true),
        ("SoporteEstudios", "Diploma o acta de grado", false),
        ("CertificadosLaborales", "Certificados laborales", false),
        ("CartaInstitucion", "Carta de la institución educativa", true),
    ];

    public static string? Nombre(string clave) => Todos.FirstOrDefault(t => t.Clave == clave).Nombre;
}
