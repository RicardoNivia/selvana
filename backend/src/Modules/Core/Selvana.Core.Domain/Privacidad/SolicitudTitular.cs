using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Domain.Privacidad;

/// <summary>Lo que pide el titular (Ley 1581 de 2012, arts. 14 y 15).</summary>
public enum TipoSolicitud
{
    /// <summary>Saber qué datos suyos tiene la empresa. 10 días hábiles.</summary>
    Consulta,

    /// <summary>Corregir o actualizar sus datos. Reclamo: 15 días hábiles.</summary>
    Actualizacion,

    /// <summary>Que se eliminen sus datos. Reclamo: 15 días hábiles.</summary>
    Supresion,

    /// <summary>Retirar la autorización de tratamiento. Reclamo: 15 días hábiles; implica suprimir.</summary>
    Revocatoria,
}

public enum EstadoSolicitud
{
    Abierta,
    Respondida,
}

public static class SolicitudErrors
{
    public static readonly Error NoExiste = Error.NotFound("Solicitud.NoExiste", "Esa solicitud no existe.");
    public static readonly Error YaRespondida = Error.Conflict("Solicitud.YaRespondida", "Esa solicitud ya se respondió.");
    public static readonly Error RespuestaVacia = Error.Validation("Solicitud.RespuestaVacia", "Escriba la respuesta para el titular.");
    public static readonly Error DocumentoInvalido = Error.Validation("Solicitud.Documento", "Escriba el número de documento sin puntos.");
    public static readonly Error SinContacto = Error.Validation("Solicitud.SinContacto", "Deje un correo o un celular para enviarle la respuesta.");
}

/// <summary>Una solicitud de un titular de datos, con su plazo legal en días hábiles.</summary>
public sealed class SolicitudTitular : AggregateRoot<Guid>
{
    private SolicitudTitular(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public TipoSolicitud Tipo { get; private init; }
    public string Titular { get; private init; } = "";
    public string Documento { get; private init; } = "";
    public string Contacto { get; private init; } = "";
    public string Canal { get; private init; } = "";
    public string Descripcion { get; private init; } = "";
    public DateTimeOffset RecibidaEn { get; private init; }
    public DateOnly Vence { get; private init; }
    public string RegistradaPor { get; private init; } = "";

    /// <summary>Respuesta preparada con los datos que la empresa tiene del titular; la persona la revisa antes de responder.</summary>
    public string BorradorRespuesta { get; private set; } = "";
    public EstadoSolicitud Estado { get; private set; }
    public string? Respuesta { get; private set; }
    public string? RespondidaPor { get; private set; }
    public DateTimeOffset? RespondidaEn { get; private set; }

    /// <summary>Se aprobó suprimir los datos (supresión o revocatoria).</summary>
    public bool SupresionAprobada { get; private set; }

    public static int PlazoDiasHabiles(TipoSolicitud tipo) => tipo == TipoSolicitud.Consulta ? 10 : 15;

    public bool EsSupresion => Tipo is TipoSolicitud.Supresion or TipoSolicitud.Revocatoria;

    public static Result<SolicitudTitular> Registrar(
        Guid empresaId, TipoSolicitud tipo, string titular, string documento, string contacto, string canal, string descripcion,
        string borrador, string registradaPor, DateTimeOffset ahora, TimeSpan zonaHoraria)
    {
        var digitos = new string([.. (documento ?? "").Where(char.IsDigit)]);
        if (digitos.Length is < 5 or > 12)
        {
            return SolicitudErrors.DocumentoInvalido;
        }

        if (string.IsNullOrWhiteSpace(contacto))
        {
            return SolicitudErrors.SinContacto;
        }

        var hoyLocal = DateOnly.FromDateTime(ahora.ToOffset(zonaHoraria).DateTime);
        return new SolicitudTitular(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            Tipo = tipo,
            Titular = (titular ?? "").Trim(),
            Documento = digitos,
            Contacto = contacto.Trim(),
            Canal = canal.Trim(),
            Descripcion = (descripcion ?? "").Trim(),
            RecibidaEn = ahora,
            Vence = CalendarioColombia.SumarDiasHabiles(hoyLocal, PlazoDiasHabiles(tipo)),
            RegistradaPor = registradaPor,
            BorradorRespuesta = borrador,
            Estado = EstadoSolicitud.Abierta,
        };
    }

    /// <summary>Responde al titular. En una supresión, <paramref name="aprobarSupresion"/> ordena eliminar sus datos.</summary>
    public Result Responder(string respuesta, bool aprobarSupresion, string usuario, DateTimeOffset ahora)
    {
        if (Estado == EstadoSolicitud.Respondida)
        {
            return Result.Failure(SolicitudErrors.YaRespondida);
        }

        if (string.IsNullOrWhiteSpace(respuesta))
        {
            return Result.Failure(SolicitudErrors.RespuestaVacia);
        }

        Respuesta = respuesta.Trim();
        RespondidaPor = usuario;
        RespondidaEn = ahora;
        SupresionAprobada = EsSupresion && aprobarSupresion;
        Estado = EstadoSolicitud.Respondida;
        return Result.Success();
    }
}
