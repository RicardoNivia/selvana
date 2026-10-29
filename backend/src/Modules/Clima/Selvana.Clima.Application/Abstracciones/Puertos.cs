using Selvana.Clima.Domain.Encuestas;
using Selvana.Clima.Domain.Pulsos;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Application.Abstracciones;

/// <summary>Quién hace la petición en la consola y en qué empresa trabaja.</summary>
public interface IUsuarioActual
{
    Guid EmpresaId { get; }
    string Nombre { get; }

    /// <summary>Empresas usuarias que ve; null = todas.</summary>
    IReadOnlyCollection<Guid>? EmpresasUsuarias { get; }
}

public static class AlcanceUsuario
{
    public static bool Ve(this IUsuarioActual usuario, Guid? empresaUsuariaId) =>
        empresaUsuariaId is not { } id || usuario.EmpresasUsuarias is null || usuario.EmpresasUsuarias.Contains(id);
}

/// <summary>Unidad de trabajo del módulo (esquema "clima").</summary>
public interface IUnidadClima
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IEncuestas
{
    Task<Encuesta?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    Task<Encuesta?> PorIdInternoAsync(Guid id, CancellationToken cancellationToken);

    Task<Encuesta?> PorTokenAsync(string tokenHash, CancellationToken cancellationToken);

    void Agregar(Encuesta encuesta);

    void AgregarRespuesta(RespuestaClima respuesta);

    Task<IReadOnlyList<RespuestaClima>> RespuestasAsync(Guid encuestaId, CancellationToken cancellationToken);
}

public interface IPulsos
{
    Task<Pulso?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    /// <summary>Para el portal, que llega sin empresa: el handler comprueba la cédula y el correo.</summary>
    Task<Pulso?> PorIdInternoAsync(Guid id, CancellationToken cancellationToken);

    Task<HashSet<(Guid EmpleadoId, int Hito)>> ProgramadosAsync(Guid empresaId, CancellationToken cancellationToken);

    /// <summary>El último pulso respondido de cada empleado (para el riesgo de rotación).</summary>
    Task<IReadOnlyList<Pulso>> UltimosRespondidosAsync(Guid empresaId, CancellationToken cancellationToken);

    void Agregar(Pulso pulso);
}

/// <summary>Enlaces de un solo uso por encuesta: se entrega el token y se guarda solo su huella.</summary>
public interface ITokensEncuesta
{
    (string Token, string Huella) Generar();

    string Huella(string token);
}

public interface IEventosClima
{
    void Publicar(IIntegrationEvent evento);
}

/// <summary>Deja una acción en la bitácora de la empresa (Core), por el outbox del módulo y en la misma transacción.</summary>
public interface IBitacora
{
    void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false);
}

public sealed record FilaEncuestaDto(Guid Id, string Titulo, string Estado, int Preguntas, int Respuestas, DateTimeOffset? AbiertaEn, DateTimeOffset? CerradaEn);

public sealed record FilaPulsoDto(
    Guid Id, Guid EmpleadoId, string Nombre, string Cargo, string Area, int Hito, DateOnly PendienteDesde, bool Respondido, decimal? Promedio, int? Recomendaria,
    string? Comentario, bool Alerta, string? Seguimiento, string? SeguimientoPor);

public interface ILecturaClima
{
    Task<Pagina<FilaEncuestaDto>> EncuestasAsync(Guid empresaId, PaginaSolicitada pagina, CancellationToken cancellationToken);

    Task<Pagina<FilaPulsoDto>> PulsosAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina, string? filtro, CancellationToken cancellationToken);

    /// <summary>La última encuesta con respuestas, para el clima por área en el riesgo de rotación.</summary>
    Task<Encuesta?> UltimaConRespuestasAsync(Guid empresaId, CancellationToken cancellationToken);
}
