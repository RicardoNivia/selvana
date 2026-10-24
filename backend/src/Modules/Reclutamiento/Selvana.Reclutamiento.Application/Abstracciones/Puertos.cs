using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Postulaciones;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Reclutamiento.Application.Abstracciones;

/// <summary>Quién hace la petición en la consola y en qué empresa trabaja.</summary>
public interface IUsuarioActual
{
    Guid EmpresaId { get; }
    string Nombre { get; }

    /// <summary>Rol de la matriz de permisos: decide qué etapa de una requisición puede aprobar.</summary>
    string Rol { get; }

    /// <summary>Empresas usuarias que ve; null = todas. Las vacantes de la propia empresa (sin empresa usuaria) las ven todos.</summary>
    IReadOnlyCollection<Guid>? EmpresasUsuarias { get; }
}

public static class AlcanceUsuario
{
    public static bool Ve(this IUsuarioActual usuario, Guid? empresaUsuariaId) =>
        empresaUsuariaId is not { } id || usuario.EmpresasUsuarias is null || usuario.EmpresasUsuarias.Contains(id);
}

/// <summary>Unidad de trabajo del módulo (esquema "reclutamiento").</summary>
public interface IUnidadReclutamiento
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IVacantes
{
    Task<Vacante?> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    Task<Vacante?> ObtenerPorCodigoAsync(string codigo, CancellationToken cancellationToken);

    void Agregar(Vacante vacante);
}

public interface ICandidatos
{
    Task<Candidato?> ObtenerPorDocumentoAsync(Guid empresaId, string documento, CancellationToken cancellationToken);

    Task<Candidato?> ObtenerPorPostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken);

    /// <summary>Candidatos con al menos una postulación a la vacante (con todas sus postulaciones cargadas).</summary>
    Task<IReadOnlyList<Candidato>> ConPostulacionesAAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken);

    void Agregar(Candidato candidato);
}

public interface IBorradoresPostulacion
{
    Task<BorradorPostulacion?> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(BorradorPostulacion borrador);

    void Quitar(BorradorPostulacion borrador);
}

/// <summary>Guarda hojas de vida cifradas. Solo se leen descifradas por el lector de IA y por quien tenga permiso.</summary>
public interface IAlmacenHojasDeVida
{
    Guid Guardar(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, DateTimeOffset ahora);

    /// <summary>Descifra la hoja de vida: la usan el lector de IA y quien la abre en la consola (con registro en la bitácora).</summary>
    Task<(string Nombre, string TipoContenido, byte[] Contenido)?> LeerAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Borra las hojas de vida (supresión pedida por el titular).</summary>
    Task EliminarAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}

public interface IEventosReclutamiento
{
    void Publicar(IIntegrationEvent evento);
}

/// <summary>
/// Deja una acción en la bitácora de auditoría de la empresa (Core). Va por el outbox del módulo, en la misma
/// transacción que el cambio: si el cambio no se guarda, la línea tampoco.
/// </summary>
public interface IBitacora
{
    void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false);
}

/// <summary>Texto de la autorización de tratamiento de datos que acepta el candidato. Cambiar el texto = nueva versión.</summary>
public static class PoliticaDatos
{
    public const string Version = "v3";

    public static string Texto(string empresa) =>
        $"Autorizo a {empresa} y a Selvana a tratar mis datos personales (hoja de vida, datos de contacto y respuestas) para este " +
        "proceso de selección y para ofrecerme vacantes similares, según la Ley 1581 de 2012. Puedo consultar, actualizar o pedir " +
        "que se supriman mis datos en cualquier momento.";
}
