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

    /// <summary>
    /// Empresas con las que la persona decidió compartir su hoja de vida del portal (por ejemplo, su empleador, para su expediente).
    /// Sin esto, su empresa no la ve: la autorización del portal es con Selvana, no con cada empresa (Ley 1581).
    /// </summary>
    public List<Guid> HojaDeVidaCompartidaCon { get; private set; } = [];

    public void CompartirHojaDeVida(Guid empresaId, bool compartir)
    {
        HojaDeVidaCompartidaCon = compartir
            ? [.. HojaDeVidaCompartidaCon.Append(empresaId).Distinct()]
            : [.. HojaDeVidaCompartidaCon.Where(e => e != empresaId)];
    }

    /// <summary>P4 · Mis documentos: uno por tipo, cifrados, listos para entregar a los procesos que los pidan.</summary>
    public List<DocumentoPersona> Documentos { get; private set; } = [];

    public static readonly IReadOnlyList<string> TiposDocumento = ["CC", "CE", "TI", "PA", "PPT"];

    /// <summary>Guarda un documento; si ya había uno del mismo tipo lo reemplaza y devuelve el archivo anterior para borrarlo.</summary>
    public Result<(DocumentoPersona Documento, Guid? ArchivoAnterior)> GuardarDocumento(string tipo, Guid archivoId, string nombreArchivo, string tipoContenido,
        DateOnly? venceEn, DateTimeOffset ahora)
    {
        if (TiposDocumentoPersona.Nombre(tipo) is null)
        {
            return DocumentosPersonaErrors.TipoInvalido;
        }

        var d = Documentos.FirstOrDefault(x => x.Tipo == tipo);
        Guid? anterior = null;
        if (d is null)
        {
            d = new DocumentoPersona { Id = Guid.CreateVersion7(), Tipo = tipo };
            Documentos.Add(d);
        }
        else
        {
            anterior = d.ArchivoId;
        }

        d.ArchivoId = archivoId;
        d.NombreArchivo = nombreArchivo;
        d.TipoContenido = tipoContenido;
        d.VenceEn = venceEn;
        d.SubidoEn = ahora;
        // La lista se reemplaza para que EF Core note el cambio en la columna JSON
        Documentos = [.. Documentos];
        return (d, anterior);
    }

    public Result<Guid> QuitarDocumento(Guid id)
    {
        var d = Documentos.FirstOrDefault(x => x.Id == id);
        if (d is null)
        {
            return DocumentosPersonaErrors.NoExiste;
        }

        Documentos = [.. Documentos.Where(x => x.Id != id)];
        return d.ArchivoId;
    }

    /// <summary>El documento vigente de ese tipo, listo para entregar; falla si no existe o ya venció.</summary>
    public Result<DocumentoPersona> DocumentoParaEnviar(string tipo, DateOnly hoy)
    {
        var d = Documentos.FirstOrDefault(x => x.Tipo == tipo);
        if (d is null)
        {
            return DocumentosPersonaErrors.NoExiste;
        }

        return d.VenceEn is { } vence && vence < hoy ? DocumentosPersonaErrors.Vencido : d;
    }

    public void RegistrarEnvio(Guid documentoId, Guid procesoId, string empresa, DateTimeOffset ahora)
    {
        if (Documentos.FirstOrDefault(x => x.Id == documentoId) is { } d)
        {
            d.Envios = [.. d.Envios, new EnvioDocumento { ProcesoId = procesoId, Empresa = empresa, En = ahora }];
            Documentos = [.. Documentos];
        }
    }

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
