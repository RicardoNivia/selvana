using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Reclutamiento.Application.Candidatos;
using Selvana.Reclutamiento.Application.Recontacto;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Recontacto;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Infrastructure.Recontacto;

internal sealed class SugerenciaBancoConfiguration : IEntityTypeConfiguration<SugerenciaBanco>
{
    public void Configure(EntityTypeBuilder<SugerenciaBanco> builder)
    {
        builder.ToTable("sugerencias_banco");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.Calculando);
        builder.Ignore(s => s.PuntajeOrden);
        builder.HasIndex(s => new { s.EmpresaId, s.VacanteId, s.CandidatoId }).IsUnique();
        builder.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.DecididaPor).HasMaxLength(200);
        builder.Property(s => s.Motivo).HasMaxLength(500);
        builder.OwnsMany(s => s.Coincidencias, c => c.ToJson());
        builder.OwnsOne(s => s.Afinidad, a =>
        {
            a.ToJson();
            a.Property(x => x.Estado).HasConversion<string>();
            a.OwnsMany(x => x.Razones);
            a.Ignore(x => x.Puntaje);
        });
    }
}

internal sealed class SugerenciasBancoRepositorio(ReclutamientoDbContext db) : ISugerenciasBanco
{
    public Task<SugerenciaBanco?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<SugerenciaBanco>().FirstOrDefaultAsync(s => s.EmpresaId == empresaId && s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SugerenciaBanco>> DeVacanteAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken) =>
        await db.Set<SugerenciaBanco>().Where(s => s.EmpresaId == empresaId && s.VacanteId == vacanteId).ToListAsync(cancellationToken);

    public void Agregar(SugerenciaBanco sugerencia) => db.Add(sugerencia);
}

internal sealed class BancoTalento(ReclutamientoDbContext db) : IBancoTalento
{
    public async Task<IReadOnlyList<Candidato>> PorCargoAsync(Guid empresaId, IReadOnlyList<string> terminos, int maximo, CancellationToken cancellationToken)
    {
        var q = db.Set<Candidato>().AsNoTracking().Include(c => c.Postulaciones).Where(c => c.EmpresaId == empresaId && c.AutorizacionRevocadaEn == null);
        foreach (var termino in terminos)
        {
            var patron = $"%{termino}%";
            q = q.Where(c => EF.Functions.ILike(c.TextoBusqueda, patron));
        }

        return await q.OrderByDescending(c => c.ActualizadoEn).Take(maximo).AsSplitQuery().ToListAsync(cancellationToken);
    }

    public Task<Candidato?> ObtenerAsync(Guid empresaId, Guid candidatoId, CancellationToken cancellationToken) =>
        db.Set<Candidato>().AsNoTracking().Include(c => c.Postulaciones).FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Id == candidatoId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> NombresAsync(Guid empresaId, IReadOnlyCollection<Guid> candidatoIds, CancellationToken cancellationToken) =>
        await db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId && candidatoIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Datos.Nombre }).ToDictionaryAsync(c => c.Id, c => c.Nombre, cancellationToken);
}

/// <summary>Sugerencias del banco para la vacante: primero las que esperan decisión, por puntaje. El contacto solo se ve al invitar.</summary>
internal sealed class LecturaRecontacto(ReclutamientoDbContext db) : ILecturaRecontacto
{
    public async Task<Pagina<SugerenciaBancoDto>> SugerenciasAsync(Guid empresaId, Guid vacanteId, PaginaSolicitada pagina, CancellationToken cancellationToken)
    {
        var sugerencias = await db.Set<SugerenciaBanco>().AsNoTracking().Where(s => s.EmpresaId == empresaId && s.VacanteId == vacanteId).ToListAsync(cancellationToken);
        var ids = sugerencias.Select(s => s.CandidatoId).ToList();
        var candidatos = await db.Set<Candidato>().AsNoTracking().Where(c => c.EmpresaId == empresaId && ids.Contains(c.Id))
            .Select(c => new
            {
                c.Id, c.Documento, c.Datos.Nombre, c.Datos.Ciudad, c.Datos.Celular, c.Datos.Correo, Suprimido = c.AutorizacionRevocadaEn != null,
                Postulaciones = c.Postulaciones.Count, YaSePostulo = c.Postulaciones.Any(p => p.VacanteId == vacanteId),
            })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var filas = sugerencias.Where(s => candidatos.ContainsKey(s.CandidatoId))
            .OrderBy(s => s.Estado == EstadoSugerenciaBanco.Sugerida ? 0 : 1).ThenByDescending(s => s.PuntajeOrden).ToList();
        return new Pagina<SugerenciaBancoDto>([.. filas.Skip(pagina.Omitir).Take(pagina.Tamano).Select(s =>
        {
            var c = candidatos[s.CandidatoId];
            var contacto = s.Estado == EstadoSugerenciaBanco.Invitada && !c.Suprimido;
            var a = s.Afinidad;
            return new SugerenciaBancoDto(s.Id, s.CandidatoId, c.Suprimido ? "Titular con datos suprimidos" : c.Nombre, FormatoCandidato.Enmascarar(c.Documento),
                c.Suprimido ? "" : c.Ciudad, s.PuntajeReglas, [.. s.Coincidencias.Select(x => new CoincidenciaBancoDto(x.Tipo, x.Texto, x.Evidencia))],
                a is null ? null : new AfinidadBancoDto(a.Estado.ToString(), a.PuntajeIa, [.. a.Razones.Select(r => new RazonBancoDto(r.Signo, r.Texto, r.Evidencia))],
                    a.Cumplidos, a.Faltantes, a.Modelo, a.Mensaje),
                s.Estado.ToString(), s.DecididaPor, s.DecididaEn, s.Motivo, c.YaSePostulo, c.Postulaciones - (c.YaSePostulo ? 1 : 0),
                contacto ? c.Celular : null, contacto ? c.Correo : null, s.CreadaEn);
        })], pagina.Numero, pagina.Tamano, filas.Count);
    }
}
