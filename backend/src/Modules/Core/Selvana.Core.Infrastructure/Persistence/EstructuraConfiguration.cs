using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Core.Domain.Estructura;

namespace Selvana.Core.Infrastructure.Persistence;

internal sealed class EstructuraEmpresaConfiguration : IEntityTypeConfiguration<EstructuraEmpresa>
{
    public void Configure(EntityTypeBuilder<EstructuraEmpresa> builder)
    {
        builder.ToTable("estructuras");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.DomainEvents);

        builder.HasMany(e => e.Sedes).WithOne().HasForeignKey("EstructuraId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Areas).WithOne().HasForeignKey("EstructuraId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.CentrosCosto).WithOne().HasForeignKey("EstructuraId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Cargos).WithOne().HasForeignKey("EstructuraId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Importaciones).WithOne().HasForeignKey("EstructuraId").OnDelete(DeleteBehavior.Cascade);

        foreach (var navegacion in new[] { nameof(EstructuraEmpresa.Sedes), nameof(EstructuraEmpresa.Areas),
            nameof(EstructuraEmpresa.CentrosCosto), nameof(EstructuraEmpresa.Cargos), nameof(EstructuraEmpresa.Importaciones) })
        {
            builder.Navigation(navegacion).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}

internal sealed class SedeConfiguration : IEntityTypeConfiguration<Sede>
{
    public void Configure(EntityTypeBuilder<Sede> builder)
    {
        builder.ToTable("sedes");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Nombre).HasMaxLength(200);
        builder.Property(s => s.NombreNomina).HasMaxLength(200);
        builder.Property(s => s.Activo).HasDefaultValue(true);
    }
}

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("areas");
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Nombre).HasMaxLength(200);
        builder.Property(a => a.NombreNomina).HasMaxLength(200);
        builder.Property(a => a.Activo).HasDefaultValue(true);
    }
}

internal sealed class CentroCostoConfiguration : IEntityTypeConfiguration<CentroCosto>
{
    public void Configure(EntityTypeBuilder<CentroCosto> builder)
    {
        builder.ToTable("centros_costo");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Codigo).HasMaxLength(50);
        builder.Property(c => c.Nombre).HasMaxLength(200);
        builder.Property(c => c.Sede).HasMaxLength(200);
        builder.Property(c => c.NombreNomina).HasMaxLength(200);
        builder.Property(c => c.Activo).HasDefaultValue(true);
    }
}

internal sealed class CargoConfiguration : IEntityTypeConfiguration<Cargo>
{
    public void Configure(EntityTypeBuilder<Cargo> builder)
    {
        builder.ToTable("cargos");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Nombre).HasMaxLength(200);
        builder.Property(c => c.Area).HasMaxLength(200);
        builder.Property(c => c.EstadoPerfil).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ModeloIa).HasMaxLength(100);
        builder.Property(c => c.AprobadoPor).HasMaxLength(200);
        builder.Property(c => c.Activo).HasDefaultValue(true);
        builder.OwnsOne(c => c.Perfil, p => p.ToJson());
        builder.OwnsMany(c => c.Razones, r => r.ToJson());
    }
}

internal sealed class ImportacionNominaConfiguration : IEntityTypeConfiguration<ImportacionNomina>
{
    public void Configure(EntityTypeBuilder<ImportacionNomina> builder)
    {
        builder.ToTable("importaciones_nomina");
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Fuente).HasMaxLength(100);
        builder.Property(i => i.Estado).HasConversion<string>().HasMaxLength(30);
        builder.Property(i => i.MensajeIa).HasMaxLength(1000);
        builder.Property(i => i.ModeloIa).HasMaxLength(100);
        builder.Property(i => i.AplicadaPor).HasMaxLength(200);
        builder.OwnsMany(i => i.Sedes, b => b.ToJson("sedes_leidas"));
        builder.OwnsMany(i => i.Areas, b => b.ToJson("areas_leidas"));
        builder.OwnsMany(i => i.CentrosCosto, b => b.ToJson("centros_costo_leidos"));
        builder.OwnsMany(i => i.Cargos, b => b.ToJson("cargos_leidos"));
        builder.HasMany(i => i.Propuestas).WithOne().HasForeignKey("ImportacionId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Propuestas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PropuestaConfiguration : IEntityTypeConfiguration<Propuesta>
{
    public void Configure(EntityTypeBuilder<Propuesta> builder)
    {
        builder.ToTable("propuestas_importacion");
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Origen).HasConversion<string>().HasMaxLength(10);
        builder.Property(p => p.Titulo).HasMaxLength(300);
        builder.Property(p => p.Porque).HasMaxLength(1000);
        builder.Property(p => p.Evidencia).HasMaxLength(300);
        builder.Property(p => p.Valor).HasMaxLength(200);
    }
}
