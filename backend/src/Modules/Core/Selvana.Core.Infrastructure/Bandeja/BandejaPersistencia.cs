using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Core.Application.Bandeja;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Domain.Auditoria;
using Selvana.Core.Domain.Bandeja;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Infrastructure.Bandeja;

internal sealed class PropuestaBandejaConfiguration : IEntityTypeConfiguration<PropuestaBandeja>
{
    public void Configure(EntityTypeBuilder<PropuestaBandeja> builder)
    {
        builder.ToTable("propuestas_bandeja");
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.EsDeIa);
        builder.Property(p => p.Modulo).HasMaxLength(40);
        builder.Property(p => p.Tipo).HasMaxLength(60);
        builder.Property(p => p.Titulo).HasMaxLength(300);
        builder.Property(p => p.Descripcion).HasMaxLength(1000);
        builder.Property(p => p.Agente).HasMaxLength(120);
        builder.Property(p => p.Modelo).HasMaxLength(120);
        builder.Property(p => p.Revisar).HasMaxLength(300);
        builder.Property(p => p.Decision).HasMaxLength(400);
        builder.Property(p => p.DecididaPor).HasMaxLength(200);
        builder.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
        builder.OwnsMany(p => p.Razones, r => r.ToJson());
        builder.OwnsMany(p => p.Campos, c => c.ToJson());
        builder.OwnsOne(p => p.Accion, a => a.ToJson());
        // Una sola propuesta por entidad y tipo: si se vuelve a publicar, se actualiza
        builder.HasIndex(p => new { p.EmpresaId, p.Tipo, p.EntidadId }).IsUnique();
        builder.HasIndex(p => new { p.EmpresaId, p.Estado });
    }
}

internal sealed class PropuestasRepositorio(CoreDbContext db) : IPropuestas
{
    public Task<PropuestaBandeja?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<PropuestaBandeja>().FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.Id == id, cancellationToken);

    public async Task<PropuestaBandeja?> ObtenerPorEntidadAsync(Guid empresaId, string tipo, Guid entidadId, CancellationToken cancellationToken) =>
        db.Set<PropuestaBandeja>().Local.FirstOrDefault(p => p.EmpresaId == empresaId && p.Tipo == tipo && p.EntidadId == entidadId)
        ?? await db.Set<PropuestaBandeja>().FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.Tipo == tipo && p.EntidadId == entidadId, cancellationToken);

    public void Agregar(PropuestaBandeja propuesta) => db.Add(propuesta);
}

internal sealed class LecturaBandeja(CoreDbContext db) : ILecturaBandeja
{
    public async Task<Pagina<PropuestaDto>> PropuestasAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, string rol, string estado, PaginaSolicitada pagina, DateOnly hoy,
        CancellationToken cancellationToken)
    {
        var q = Visibles(empresaId, alcance, rol);
        if (Enum.TryParse<EstadoPropuesta>(estado, ignoreCase: true, out var e))
        {
            q = q.Where(p => p.Estado == e);
        }

        var total = await q.CountAsync(cancellationToken);
        // Primero lo que vence (las que tienen fecha, de la más próxima a la más lejana) y después lo más reciente
        var filas = await q.OrderBy(p => p.VenceEl == null).ThenBy(p => p.VenceEl).ThenByDescending(p => p.ActualizadaEn)
            .Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);
        var items = filas.Select(p => new PropuestaDto(
            p.Id, p.Modulo, p.Tipo, p.EntidadId, p.EmpresaUsuariaId, p.Titulo, p.Descripcion, p.Agente, p.Modelo,
            [.. p.Razones.Select(r => new RazonPropuesta(r.Signo, r.Texto, r.Fuente))], [.. p.Campos.Select(c => new CampoPropuesta(c.Etiqueta, c.Valor))],
            p.Accion is null ? null : new AccionPropuesta(p.Accion.Etiqueta, p.Accion.Metodo, p.Accion.Ruta), p.Revisar, p.AfectaPersona, p.VenceEl,
            p.EsUrgente(hoy), p.ActualizadaEn, p.Estado.ToString(), p.Decision, p.DecididaPor, p.DecididaEn)).ToList();
        return new Pagina<PropuestaDto>(items, pagina.Numero, pagina.Tamano, total);
    }

    public async Task<ResumenBandejaDto> ResumenAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, string rol, DateOnly hoy, CancellationToken cancellationToken)
    {
        var pendientes = await Visibles(empresaId, alcance, rol).Where(p => p.Estado == EstadoPropuesta.Pendiente)
            .Select(p => new { p.VenceEl, p.AfectaPersona }).ToListAsync(cancellationToken);
        return new ResumenBandejaDto(pendientes.Count, pendientes.Count(p => p.VenceEl is { } v && v.DayNumber - hoy.DayNumber <= 2), pendientes.Count(p => p.AfectaPersona));
    }

    public async Task<IReadOnlyList<ActividadAgenteDto>> ActividadAgentesAsync(Guid empresaId, DateTimeOffset desde, CancellationToken cancellationToken)
    {
        var acciones = await db.Set<EntradaBitacora>().AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && e.TipoActor == TipoActor.Ia && e.Fecha >= desde)
            .OrderByDescending(e => e.Fecha).Select(e => new { e.Actor, e.Accion, e.Fecha }).ToListAsync(cancellationToken);
        var esperan = await db.Set<PropuestaBandeja>().AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Estado == EstadoPropuesta.Pendiente && p.Agente != null)
            .GroupBy(p => p.Agente!).Select(g => new { Agente = g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Agente, x => x.Total, cancellationToken);

        return [.. acciones.GroupBy(a => a.Actor).Select(g => new ActividadAgenteDto(g.Key, g.Count(), g.First().Accion, g.First().Fecha, esperan.GetValueOrDefault(g.Key)))
            .OrderByDescending(a => a.UltimaVez)];
    }

    /// <summary>
    /// Las de la propia empresa (sin empresa usuaria) las ven todos; las de un cliente, quien lo tiene asignado.
    /// Las dirigidas a ciertos roles (la etapa de gerencia de una requisición) solo las ven esos roles.
    /// </summary>
    private IQueryable<PropuestaBandeja> Visibles(Guid empresaId, IReadOnlyCollection<Guid>? alcance, string rol)
    {
        var q = db.Set<PropuestaBandeja>().AsNoTracking().Where(p => p.EmpresaId == empresaId && (p.ParaRoles.Count == 0 || p.ParaRoles.Contains(rol)));
        if (alcance is not null)
        {
            var ids = alcance.ToList();
            q = q.Where(p => p.EmpresaUsuariaId == null || ids.Contains(p.EmpresaUsuariaId.Value));
        }

        return q;
    }
}
