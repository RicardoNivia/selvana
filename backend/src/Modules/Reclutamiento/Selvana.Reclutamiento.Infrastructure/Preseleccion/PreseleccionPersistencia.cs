using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Reclutamiento.Application.Preseleccion;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure.Preseleccion;

internal sealed class ConversacionPreseleccionConfiguration : IEntityTypeConfiguration<ConversacionPreseleccion>
{
    public void Configure(EntityTypeBuilder<ConversacionPreseleccion> builder)
    {
        builder.ToTable("conversaciones_preseleccion");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Activa);
        builder.Property(c => c.TokenHash).HasMaxLength(64);
        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => new { c.EmpresaId, c.PostulacionId }).IsUnique();
        builder.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.MotivoEscalamiento).HasMaxLength(300);
        builder.Property(c => c.Modelo).HasMaxLength(100);
        builder.Property(c => c.InvitadaPor).HasMaxLength(200);
        builder.OwnsMany(c => c.Mensajes, m =>
        {
            m.ToJson();
            m.Property(x => x.Autor).HasConversion<string>();
        });
        builder.OwnsOne(c => c.Resumen, r =>
        {
            r.ToJson();
            r.OwnsMany(x => x.Respuestas);
        });
    }
}

internal sealed class ConversacionesRepositorio(ReclutamientoDbContext db) : IConversaciones
{
    public Task<ConversacionPreseleccion?> PorTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Set<ConversacionPreseleccion>().FirstOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);

    public Task<ConversacionPreseleccion?> PorIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<ConversacionPreseleccion>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<ConversacionPreseleccion?> PorPostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken) =>
        db.Set<ConversacionPreseleccion>().FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.PostulacionId == postulacionId, cancellationToken);

    public async Task<IReadOnlyList<ConversacionPreseleccion>> DeVacanteAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken) =>
        await db.Set<ConversacionPreseleccion>().AsNoTracking().Where(c => c.EmpresaId == empresaId && c.VacanteId == vacanteId).ToListAsync(cancellationToken);

    public void Agregar(ConversacionPreseleccion conversacion) => db.Add(conversacion);
}
