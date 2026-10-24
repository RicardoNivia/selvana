using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.AI;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.AI.Gateway;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.Reclutamiento;

public sealed record ResultadoLectura(DatosHojaDeVida Datos, string Modelo);

public interface ILectorHojasDeVida
{
    Task<Result<ResultadoLectura>> LeerAsync(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, CancellationToken cancellationToken);
}

/// <summary>
/// Agente lector de hojas de vida: PDF y fotos se envían tal cual al modelo; de los Word se extrae el texto.
/// La hoja de vida tiene datos personales: solo modelos de pago (ADR 0007) y nunca se extraen variables protegidas.
/// </summary>
internal sealed partial class LectorHojasDeVidaAgente(IChatClient ia) : ILectorHojasDeVida
{
    public const string Nombre = "lector-hojas-de-vida";
    private const string TipoWord = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string TipoTexto = "text/plain";

    private const string Instrucciones = """
        Eres el lector de hojas de vida de Selvana, un software de selección para empresas colombianas.
        Extrae los datos de la hoja de vida que te envían, exactamente como aparecen. Si un dato no está, déjalo vacío: no lo inventes.

        - "documento": número de cédula u otro documento, solo dígitos.
        - "celular": celular colombiano de 10 dígitos, solo dígitos, sin el indicativo 57.
        - "ciudad": ciudad y departamento donde vive la persona (no la dirección).
        - "experiencia": del trabajo más reciente al más antiguo; "desde" y "hasta" como "AAAA" o "MM/AAAA"; si sigue trabajando, "hasta" es "hoy".
        - "formacion": títulos obtenidos o en curso, con institución y año.
        - "cursos": cursos y certificaciones con su vigencia si aparece (por ejemplo "Montacargas, SENA, vigente hasta 03/2027").
        - "habilidades": máximo 15, cortas: las técnicas (herramientas, oficio, idiomas con su nivel) y también las blandas que la persona mencione en su perfil o en cualquier parte, con sus palabras (por ejemplo "Responsable", "Puntual", "Trabajo en equipo").
        - "linkedIn": la dirección del perfil de LinkedIn si aparece (linkedin.com/in/...); los PDF exportados desde LinkedIn la traen en "Contactar". Si no aparece, vacío.
        - NUNCA extraigas edad, fecha de nacimiento, género, estado civil, hijos, religión, foto ni dirección exacta, aunque aparezcan.
        """;

    public async Task<Result<ResultadoLectura>> LeerAsync(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        List<AIContent> partes = [new TextContent($"Hoja de vida «{nombre}». Extrae los datos.")];
        if (tipoContenido == TipoTexto)
        {
            // Texto pegado o del banco de pruebas: va directo al modelo
            partes.Add(new TextContent(Encoding.UTF8.GetString(contenido)));
        }
        else if (tipoContenido == TipoWord)
        {
            var texto = TextoDeWord(contenido);
            if (string.IsNullOrWhiteSpace(texto))
            {
                return Error.Failure("Hv.WordVacio", "No encontramos texto en el archivo de Word. Súbelo en PDF o escribe tus datos.");
            }

            partes.Add(new TextContent(texto));
        }
        else
        {
            partes.Add(new DataContent(contenido, tipoContenido));
        }

        var mensajes = new List<ChatMessage> { new(ChatRole.System, Instrucciones), new(ChatRole.User, partes) };
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: true).AplicarA(new ChatOptions { MaxOutputTokens = 16_000 });

        try
        {
            var respuesta = await ia.GetResponseAsync<DatosHojaDeVida>(mensajes, opciones, cancellationToken: cancellationToken);
            if (!respuesta.TryGetResult(out var datos) || datos is null || string.IsNullOrWhiteSpace(datos.Nombre))
            {
                return Error.Failure("Hv.Ilegible", tipoContenido.StartsWith("image/", StringComparison.Ordinal)
                    ? "No pudimos leer la foto. Tómala con buena luz, sin sombras y que se vea toda la hoja, o escribe tus datos."
                    : "No pudimos leer tu hoja de vida. Escribe tus datos y envía tu postulación.");
            }

            return new ResultadoLectura(Normalizar(datos), respuesta.ModelId ?? "");
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ahora no pudimos leer tu hoja de vida. Escribe tus datos y envía tu postulación.");
        }
    }

    internal static DatosHojaDeVida Normalizar(DatosHojaDeVida d)
    {
        static string Digitos(string? s) => new([.. (s ?? "").Where(char.IsDigit)]);
        var celular = Digitos(d.Celular);
        if (celular.Length == 12 && celular.StartsWith("57", StringComparison.Ordinal))
        {
            celular = celular[2..];
        }

        return d with
        {
            Nombre = d.Nombre.Trim(),
            Documento = Digitos(d.Documento),
            Celular = celular,
            Correo = (d.Correo ?? "").Trim().ToLowerInvariant(),
            Ciudad = (d.Ciudad ?? "").Trim(),
            Experiencia = d.Experiencia ?? [],
            Formacion = d.Formacion ?? [],
            Cursos = d.Cursos ?? [],
            Habilidades = [.. (d.Habilidades ?? []).Take(15)],
            LinkedIn = PerfilLinkedIn(d.LinkedIn),
        };
    }

    /// <summary>Solo una dirección de perfil (linkedin.com/in/usuario), con https; cualquier otra cosa se descarta.</summary>
    internal static string? PerfilLinkedIn(string? texto)
    {
        var m = FormatoLinkedIn().Match(texto ?? "");
        return m.Success ? $"https://www.linkedin.com/in/{m.Groups[1].Value}" : null;
    }

    [GeneratedRegex(@"linkedin\.com/in/([A-Za-z0-9\-_%]{3,100})", RegexOptions.IgnoreCase)]
    private static partial Regex FormatoLinkedIn();

    /// <summary>Texto de un .docx: cada párrafo (w:p) en una línea.</summary>
    internal static string TextoDeWord(byte[] contenido)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(contenido), ZipArchiveMode.Read);
            var entrada = zip.GetEntry("word/document.xml");
            if (entrada is null)
            {
                return "";
            }

            using var flujo = entrada.Open();
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var documento = XDocument.Load(flujo);
            var texto = new StringBuilder();
            foreach (var parrafo in documento.Descendants(w + "p"))
            {
                texto.AppendLine(string.Concat(parrafo.Descendants(w + "t").Select(t => t.Value)));
            }

            return texto.ToString();
        }
        catch (InvalidDataException)
        {
            return "";
        }
    }
}
