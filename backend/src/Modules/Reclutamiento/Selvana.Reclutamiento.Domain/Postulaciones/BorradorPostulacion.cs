using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Postulaciones;

public enum EstadoLectura
{
    Leyendo,
    Leida,
    Fallida,
}

public static class BorradorErrors
{
    public static readonly Error NoExiste = Error.NotFound("Borrador.NoExiste", "Esta postulación ya no está disponible. Vuelve a subir tu hoja de vida.");
    public static readonly Error Leyendo = Error.Conflict("Borrador.Leyendo", "Todavía estamos leyendo tu hoja de vida.");
}

/// <summary>
/// Postulación en curso en el portal, antes de enviarla: guarda la hoja de vida subida y lo que la IA leyó.
/// Su Id es el token que el candidato usa para volver; caduca a las 24 horas.
/// </summary>
public sealed class BorradorPostulacion : AggregateRoot<Guid>
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(24);

    private BorradorPostulacion(Guid id) : base(id)
    {
    }

    public Guid VacanteId { get; private init; }
    public Guid EmpresaId { get; private init; }
    public Guid ArchivoId { get; private init; }
    public string NombreArchivo { get; private init; } = "";
    public EstadoLectura Estado { get; private set; }
    public DatosCandidato? DatosLeidos { get; private set; }
    public string? Mensaje { get; private set; }
    public string? ModeloIa { get; private set; }
    public DateTimeOffset CreadoEn { get; private init; }
    public DateTimeOffset? LeidoEn { get; private set; }

    public bool Vencido(DateTimeOffset ahora) => ahora - CreadoEn > Vigencia;

    /// <summary>El Id es aleatorio (no secuencial) porque funciona como token para el candidato.</summary>
    public static BorradorPostulacion Crear(Guid vacanteId, Guid empresaId, Guid archivoId, string nombreArchivo, DateTimeOffset ahora) =>
        new(Guid.NewGuid())
        {
            VacanteId = vacanteId,
            EmpresaId = empresaId,
            ArchivoId = archivoId,
            NombreArchivo = nombreArchivo,
            Estado = EstadoLectura.Leyendo,
            CreadoEn = ahora,
        };

    public void RegistrarLectura(DatosCandidato datos, string modelo, DateTimeOffset ahora)
    {
        DatosLeidos = datos;
        ModeloIa = modelo;
        Mensaje = null;
        Estado = EstadoLectura.Leida;
        LeidoEn = ahora;
    }

    /// <summary>No se pudo leer: el candidato llena sus datos a mano con el mensaje que explica por qué.</summary>
    public void RegistrarFalla(string mensaje, DateTimeOffset ahora)
    {
        Mensaje = mensaje;
        Estado = EstadoLectura.Fallida;
        LeidoEn = ahora;
    }
}
