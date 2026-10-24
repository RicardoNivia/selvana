using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Selvana.SharedKernel.Domain;

namespace Selvana.Core.Domain.Auditoria;

/// <summary>
/// Una línea de la bitácora de auditoría. Solo se agrega, nunca se cambia ni se borra. Cada línea guarda la huella
/// de la anterior de la misma empresa y su propia huella SHA-256: si alguien altera una línea, la cadena se rompe.
/// </summary>
public sealed class EntradaBitacora : Entity<Guid>
{
    private EntradaBitacora(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public DateTimeOffset Fecha { get; private init; }
    public string TipoActor { get; private init; } = "";
    public string Actor { get; private init; } = "";
    public string Accion { get; private init; } = "";
    public string Detalle { get; private init; } = "";
    public string? Nota { get; private init; }
    public string Modulo { get; private init; } = "";
    public string Entidad { get; private init; } = "";
    public Guid? EntidadId { get; private init; }
    public bool DatoSensible { get; private init; }
    public string HuellaAnterior { get; private init; } = "";
    public string Huella { get; private set; } = "";

    public static EntradaBitacora Crear(
        Guid id, Guid empresaId, DateTimeOffset fecha, string tipoActor, string actor, string accion, string detalle, string? nota,
        string modulo, string entidad, Guid? entidadId, bool datoSensible, string huellaAnterior)
    {
        var entrada = new EntradaBitacora(id)
        {
            EmpresaId = empresaId,
            // PostgreSQL guarda microsegundos: se trunca para que la huella sea la misma al releer
            Fecha = new DateTimeOffset(fecha.UtcTicks - fecha.UtcTicks % 10, TimeSpan.Zero),
            TipoActor = tipoActor,
            Actor = Cortar(actor, 200),
            Accion = Cortar(accion, 300),
            Detalle = Cortar(detalle, 1000),
            Nota = nota is null ? null : Cortar(nota, 500),
            Modulo = modulo,
            Entidad = entidad,
            EntidadId = entidadId,
            DatoSensible = datoSensible,
            HuellaAnterior = huellaAnterior,
        };
        entrada.Huella = entrada.CalcularHuella();
        return entrada;
    }

    /// <summary>La huella que debería tener la línea con su contenido actual; distinta de <see cref="Huella"/> si se alteró.</summary>
    public string CalcularHuella()
    {
        var contenido = string.Join('\u001f', Id.ToString(), EmpresaId.ToString(), Fecha.UtcTicks.ToString(CultureInfo.InvariantCulture), TipoActor, Actor,
            Accion, Detalle, Nota ?? "", Modulo, Entidad, EntidadId?.ToString() ?? "", DatoSensible ? "1" : "0", HuellaAnterior);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));
    }

    private static string Cortar(string texto, int maximo) => (texto ?? "").Length <= maximo ? texto ?? "" : texto![..maximo];
}

/// <summary>Resultado de recorrer la cadena de una empresa.</summary>
public sealed record VerificacionCadena(bool Integra, int Revisadas, Guid? PrimeraAlterada, DateTimeOffset? FechaAlterada);

public static class CadenaBitacora
{
    /// <summary>Revisa en orden que cada línea apunte a la anterior y que su huella corresponda a su contenido.</summary>
    public static VerificacionCadena Verificar(IEnumerable<EntradaBitacora> enOrden)
    {
        var anterior = "";
        var revisadas = 0;
        foreach (var e in enOrden)
        {
            revisadas++;
            if (e.HuellaAnterior != anterior || e.CalcularHuella() != e.Huella)
            {
                return new VerificacionCadena(false, revisadas, e.Id, e.Fecha);
            }

            anterior = e.Huella;
        }

        return new VerificacionCadena(true, revisadas, null, null);
    }
}
