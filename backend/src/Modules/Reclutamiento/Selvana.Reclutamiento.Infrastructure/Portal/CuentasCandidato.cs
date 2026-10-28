using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Infrastructure.Common.Seguridad;
using Selvana.Reclutamiento.Application.Portal;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure.Portal;

internal sealed class CuentasCandidatoRepositorio(ReclutamientoDbContext db) : ICuentasCandidato
{
    public Task<CuentaCandidato?> ObtenerAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<CuentaCandidato>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CuentaCandidato?> ObtenerPorCorreoAsync(string correo, CancellationToken cancellationToken) =>
        db.Set<CuentaCandidato>().FirstOrDefaultAsync(c => c.Correo == correo, cancellationToken);

    public void Agregar(CuentaCandidato cuenta) => db.Add(cuenta);
}

internal sealed class HasherClavesPortal : IHasherClavesPortal
{
    public string Calcular(string contrasena) => ContrasenasPbkdf2.Calcular(contrasena);

    public bool Verificar(string? hash, string contrasena) => ContrasenasPbkdf2.Verificar(hash, contrasena);
}

internal sealed class CuentaCandidatoConfiguration : IEntityTypeConfiguration<CuentaCandidato>
{
    public void Configure(EntityTypeBuilder<CuentaCandidato> builder)
    {
        builder.ToTable("cuentas_candidato");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Ignore(c => c.DomainEvents);
        builder.Property(c => c.Correo).HasMaxLength(200);
        builder.HasIndex(c => c.Correo).IsUnique();
        builder.Property(c => c.Documento).HasMaxLength(20);
        builder.HasIndex(c => c.Documento);
        builder.Property(c => c.ContrasenaHash).HasMaxLength(200);
        builder.Property(c => c.NombreArchivoHojaDeVida).HasMaxLength(260);
        builder.OwnsOne(c => c.Datos, d =>
        {
            d.ToJson();
            d.OwnsMany(x => x.Experiencia);
            d.OwnsMany(x => x.Formacion);
        });
        builder.OwnsOne(c => c.Autorizacion, a => a.ToJson());
    }
}
