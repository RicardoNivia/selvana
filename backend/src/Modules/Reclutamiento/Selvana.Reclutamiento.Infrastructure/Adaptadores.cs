using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Postulaciones;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Reclutamiento.Infrastructure;

internal sealed class VacantesRepositorio(ReclutamientoDbContext db) : IVacantes
{
    public Task<Vacante?> ObtenerAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Vacante>().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<Vacante?> ObtenerPorCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        var normalizado = codigo.Trim().ToUpperInvariant();
        return db.Set<Vacante>().FirstOrDefaultAsync(v => v.Codigo == normalizado, cancellationToken);
    }

    public void Agregar(Vacante vacante) => db.Add(vacante);
}

internal sealed class CandidatosRepositorio(ReclutamientoDbContext db) : ICandidatos
{
    public Task<Candidato?> ObtenerPorDocumentoAsync(Guid empresaId, string documento, CancellationToken cancellationToken) =>
        db.Set<Candidato>().Include(c => c.Postulaciones)
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Documento == documento, cancellationToken);

    public void Agregar(Candidato candidato) => db.Add(candidato);

    public Task<Candidato?> ObtenerPorPostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken) =>
        db.Set<Candidato>().Include(c => c.Postulaciones)
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Postulaciones.Any(p => p.Id == postulacionId), cancellationToken);

    public async Task<IReadOnlyList<Candidato>> ConPostulacionesAAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken) =>
        await db.Set<Candidato>().Include(c => c.Postulaciones)
            .Where(c => c.EmpresaId == empresaId && c.Postulaciones.Any(p => p.VacanteId == vacanteId)).ToListAsync(cancellationToken);
}

internal sealed class BorradoresRepositorio(ReclutamientoDbContext db) : IBorradoresPostulacion
{
    public Task<BorradorPostulacion?> ObtenerAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<BorradorPostulacion>().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void Agregar(BorradorPostulacion borrador) => db.Add(borrador);

    public void Quitar(BorradorPostulacion borrador) => db.Remove(borrador);
}

internal sealed class EventosReclutamiento(ReclutamientoDbContext db) : IEventosReclutamiento
{
    public void Publicar(IIntegrationEvent evento) => db.AddIntegrationEvent(evento);
}

/// <summary>Las hojas de vida se guardan cifradas con ASP.NET Data Protection (historia 188). Nunca en texto plano.</summary>
internal sealed class AlmacenHojasDeVida(ReclutamientoDbContext db, IDataProtectionProvider proteccion) : IAlmacenHojasDeVida
{
    private readonly IDataProtector _protector = proteccion.CreateProtector("Selvana.Reclutamiento.HojasDeVida.v1");

    public Guid Guardar(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, DateTimeOffset ahora)
    {
        var archivo = new ArchivoHojaDeVida
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            Nombre = nombre,
            TipoContenido = tipoContenido,
            Tamano = contenido.Length,
            ContenidoCifrado = _protector.Protect(contenido),
            SubidoEn = ahora,
        };
        db.Add(archivo);
        return archivo.Id;
    }

    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> LeerAsync(Guid id, CancellationToken cancellationToken)
    {
        var archivo = await db.Set<ArchivoHojaDeVida>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return archivo is null ? null : (archivo.Nombre, archivo.TipoContenido, _protector.Unprotect(archivo.ContenidoCifrado));
    }

    public async Task EliminarAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var archivos = await db.Set<ArchivoHojaDeVida>().Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken);
        db.RemoveRange(archivos);
    }
}

internal sealed class Bitacora(IEventosReclutamiento eventos, TimeProvider reloj) : IBitacora
{
    public void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false) =>
        eventos.Publicar(new AccionAuditada(Guid.NewGuid(), reloj.GetUtcNow(), empresaId, tipoActor, actor, accion, detalle, nota,
            "Reclutamiento", entidad, entidadId, datoSensible));
}

/// <summary>Lo que Reclutamiento expone a otros módulos sobre un titular (Contracts).</summary>
internal sealed class DatosTitulares(ReclutamientoDbContext db) : IDatosTitulares
{
    public async Task<DatosTitularReclutamiento?> ObtenerAsync(Guid empresaId, string documento, CancellationToken cancellationToken)
    {
        var digitos = Candidato.NormalizarDocumento(documento);
        var c = await db.Set<Candidato>().AsNoTracking().Include(x => x.Postulaciones)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Documento == digitos, cancellationToken);
        if (c is null)
        {
            return null;
        }

        var ids = c.Postulaciones.Select(p => p.VacanteId).ToList();
        var vacantes = await db.Set<Vacante>().AsNoTracking().Where(v => ids.Contains(v.Id)).Select(v => v.Titulo).ToListAsync(cancellationToken);
        var ultima = c.Postulaciones.OrderByDescending(p => p.RecibidaEn).FirstOrDefault();
        return new DatosTitularReclutamiento(c.Datos.Nombre, c.Datos.Celular, c.Datos.Correo, c.Datos.Ciudad, c.Postulaciones.Count, vacantes,
            c.Postulaciones.Any(p => p.ArchivoHojaDeVidaId != Guid.Empty), ultima?.RecibidaEn, ultima?.Autorizacion.Version, c.Suprimido);
    }

    public async Task<IReadOnlyList<Guid>> EmpresasConDatosAsync(string documento, CancellationToken cancellationToken)
    {
        var digitos = Candidato.NormalizarDocumento(documento);
        return digitos.Length == 0 ? [] : await db.Set<Candidato>().AsNoTracking().Where(c => c.Documento == digitos).Select(c => c.EmpresaId).Distinct().ToListAsync(cancellationToken);
    }
}
