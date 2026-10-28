using System.Net.Mail;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Portal;

public static class CuentaErrors
{
    public static readonly Error CorreoInvalido = Error.Validation("Cuenta.CorreoInvalido", "Escribe un correo válido, por ejemplo nombre@correo.com.");
    public static readonly Error DocumentoInvalido = Error.Validation("Cuenta.DocumentoInvalido", "Escribe tu número de identificación (entre 5 y 15 caracteres; el pasaporte puede llevar letras).");
    public static readonly Error TipoDocumentoInvalido = Error.Validation("Cuenta.TipoDocumentoInvalido", "Elige el tipo de identificación.");
    public static readonly Error DocumentoYaRegistrado = Error.Conflict("Cuenta.DocumentoYaRegistrado", "Ya existe una cuenta con esa identificación. Por ahora, contacta a soporte para recuperarla.");
    public static readonly Error SinAutorizacion = Error.Validation("Cuenta.SinAutorizacion", "Para crear tu cuenta debes aceptar la autorización de tratamiento de datos.");
    public static readonly Error YaExiste = Error.Conflict("Cuenta.YaExiste", "Ya hay una cuenta con ese correo. Entra con tu contraseña.");
    public static readonly Error CredencialesInvalidas = Error.Validation("Cuenta.Credenciales", "El correo o la contraseña no coinciden. Revísalos e intenta de nuevo.");
    public static readonly Error Bloqueada = Error.Validation("Cuenta.Bloqueada", "Por seguridad bloqueamos tu cuenta 15 minutos después de varios intentos. Intenta más tarde.");
    public static readonly Error NoExiste = Error.NotFound("Cuenta.NoExiste", "Tu sesión terminó. Vuelve a entrar.");
}

/// <summary>
/// Cuenta de la persona en el portal (ADR 0013). No es un usuario de la consola: no pertenece a una empresa.
/// Sus postulaciones en cualquier empresa se le muestran solo si el correo y la cédula coinciden.
/// </summary>
public sealed class CuentaCandidato : AggregateRoot<Guid>
{
    public const int IntentosAntesDeBloquear = 5;
    public static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);

    private CuentaCandidato(Guid id) : base(id)
    {
    }

    /// <summary>En minúsculas y sin espacios: es la llave para entrar.</summary>
    public string Correo { get; private set; } = "";

    /// <summary>CC, CE, TI, PA (pasaporte) o PPT (permiso por protección temporal).</summary>
    public string TipoDocumento { get; private set; } = "CC";

    /// <summary>Solo dígitos; el pasaporte, letras y dígitos en mayúscula.</summary>
    public string Documento { get; private set; } = "";
    public string ContrasenaHash { get; private set; } = "";

    /// <summary>Lo que la persona confirmó de su hoja de vida (nombre, contacto, experiencia, estudios).</summary>
    public DatosCandidato Datos { get; private set; } = new();
    public Guid? ArchivoHojaDeVidaId { get; private set; }
    public string NombreArchivoHojaDeVida { get; private set; } = "";
    public DateTimeOffset? HojaDeVidaActualizadaEn { get; private set; }

    /// <summary>Autorización Ley 1581 a Selvana para el perfil del portal.</summary>
    public AutorizacionDatos Autorizacion { get; private set; } = new();
    public DateTimeOffset CreadaEn { get; private init; }
    public DateTimeOffset? UltimoIngreso { get; private set; }
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadaHasta { get; private set; }

    public static readonly IReadOnlyList<string> TiposDocumento = ["CC", "CE", "TI", "PA", "PPT"];

    public static string NormalizarCorreo(string correo) => (correo ?? "").Trim().ToLowerInvariant();

    /// <summary>Igual que en la postulación (solo dígitos), salvo el pasaporte, que puede llevar letras.</summary>
    public static string NormalizarDocumento(string tipo, string numero) => tipo == "PA"
        ? new string([.. (numero ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)])
        : Candidato.NormalizarDocumento(numero);

    /// <summary>Se registra con lo mínimo; la hoja de vida la sube después en su portal y la IA la lee.</summary>
    public static Result<CuentaCandidato> Registrar(
        string correo, string contrasenaHash, string nombre, string tipoDocumento, string numeroDocumento, AutorizacionDatos? autorizacion, DateTimeOffset ahora)
    {
        var c = NormalizarCorreo(correo);
        if (!EsCorreo(c))
        {
            return CuentaErrors.CorreoInvalido;
        }

        var tipo = (tipoDocumento ?? "").Trim().ToUpperInvariant();
        if (!TiposDocumento.Contains(tipo))
        {
            return CuentaErrors.TipoDocumentoInvalido;
        }

        var documento = NormalizarDocumento(tipo, numeroDocumento);
        if (documento.Length is < 5 or > 15)
        {
            return CuentaErrors.DocumentoInvalido;
        }

        if (autorizacion is null)
        {
            return CuentaErrors.SinAutorizacion;
        }

        return new CuentaCandidato(Guid.CreateVersion7())
        {
            Correo = c,
            TipoDocumento = tipo,
            Documento = documento,
            ContrasenaHash = contrasenaHash,
            Datos = ConCorreoYDocumento(new DatosCandidato { Nombre = nombre }, c, documento),
            Autorizacion = autorizacion,
            CreadaEn = ahora,
        };
    }

    /// <summary>Resultado de un intento de ingreso: bloquea 15 minutos tras 5 fallos seguidos.</summary>
    public Result Ingresar(bool contrasenaValida, DateTimeOffset ahora)
    {
        if (BloqueadaHasta is { } hasta && hasta > ahora)
        {
            return Result.Failure(CuentaErrors.Bloqueada);
        }

        if (!contrasenaValida)
        {
            IntentosFallidos++;
            if (IntentosFallidos >= IntentosAntesDeBloquear)
            {
                BloqueadaHasta = ahora + Bloqueo;
                IntentosFallidos = 0;
            }

            return Result.Failure(CuentaErrors.CredencialesInvalidas);
        }

        IntentosFallidos = 0;
        BloqueadaHasta = null;
        UltimoIngreso = ahora;
        return Result.Success();
    }

    /// <summary>
    /// La persona corrige sus datos. El correo y la cédula no cambian aquí: son los que unen la cuenta con sus postulaciones.
    /// </summary>
    public void ActualizarDatos(DatosCandidato datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        Datos = ConCorreoYDocumento(datos, Correo, Documento);
    }

    /// <summary>Sube una hoja de vida nueva: reemplaza el archivo y lo que confirmó de ella.</summary>
    public void ActualizarHojaDeVida(Guid archivoId, string nombreArchivo, DatosCandidato datos, DateTimeOffset ahora)
    {
        ArchivoHojaDeVidaId = archivoId;
        NombreArchivoHojaDeVida = nombreArchivo;
        HojaDeVidaActualizadaEn = ahora;
        ActualizarDatos(datos);
    }

    private static DatosCandidato ConCorreoYDocumento(DatosCandidato d, string correo, string documento) => new()
    {
        Nombre = d.Nombre.Trim(),
        Documento = documento,
        Celular = d.Celular.Trim(),
        Correo = correo,
        Ciudad = d.Ciudad.Trim(),
        Experiencia = d.Experiencia,
        Formacion = d.Formacion,
        Cursos = d.Cursos,
        Habilidades = d.Habilidades,
        LinkedIn = Candidato.NormalizarLinkedIn(d.LinkedIn),
    };

    private static bool EsCorreo(string correo) =>
        correo.Length is > 4 and <= 200 && MailAddress.TryCreate(correo, out var m) && m.Address == correo && correo.Contains('.', StringComparison.Ordinal);
}
