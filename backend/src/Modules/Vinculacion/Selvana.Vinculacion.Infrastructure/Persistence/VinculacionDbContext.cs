using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Infrastructure.Common.Persistence;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Infrastructure.Persistence;

/// <summary>Unidad de trabajo del módulo Vinculación; sus tablas viven en el esquema "vinculacion".</summary>
public sealed class VinculacionDbContext(DbContextOptions<VinculacionDbContext> options)
    : ModuleDbContext(options, SchemaName), IUnidadVinculacion
{
    public const string SchemaName = "vinculacion";
}

internal sealed class ProcesoVinculacionConfiguration : IEntityTypeConfiguration<ProcesoVinculacion>
{
    public void Configure(EntityTypeBuilder<ProcesoVinculacion> builder)
    {
        builder.ToTable("procesos");
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Abierto);
        builder.Ignore(p => p.SinObservaciones);
        builder.Ignore(p => p.CandidatoCompleto);
        builder.Property(p => p.Codigo).HasMaxLength(30);
        builder.HasIndex(p => p.Codigo).IsUnique();
        builder.HasIndex(p => new { p.EmpresaId, p.ActualizadoEn });
        builder.Property(p => p.Empresa).HasMaxLength(200);
        builder.Property(p => p.EmpresaUsuaria).HasMaxLength(200);
        builder.Property(p => p.Etapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.MotivoCancelacion).HasMaxLength(500);
        builder.Property(p => p.AprobadaPor).HasMaxLength(200);
        builder.OwnsOne(p => p.Persona, x => x.ToJson());
        builder.OwnsOne(p => p.Vacante, x => x.ToJson());
        builder.OwnsOne(p => p.Condiciones, x =>
        {
            x.ToJson();
            x.Property(c => c.Tipo).HasConversion<string>();
        });
        builder.OwnsOne(p => p.Oferta, x =>
        {
            x.ToJson();
            x.Property(o => o.Estado).HasConversion<string>();
            x.OwnsMany(o => o.Clausulas);
            x.OwnsMany(o => o.Razones);
            x.OwnsOne(o => o.Respuesta);
        });
        builder.OwnsMany(p => p.Documentos, x =>
        {
            x.ToJson();
            x.Property(d => d.Tipo).HasConversion<string>();
            x.Property(d => d.Estado).HasConversion<string>();
            x.Property(d => d.Antecedentes).HasConversion<string>();
            x.OwnsOne(d => d.Lectura, l =>
            {
                l.OwnsMany(y => y.Campos);
                l.OwnsMany(y => y.Razones);
            });
        });
        builder.OwnsMany(p => p.Afiliaciones, x =>
        {
            x.ToJson();
            x.Property(a => a.Tipo).HasConversion<string>();
            x.Property(a => a.Estado).HasConversion<string>();
        });
        builder.OwnsOne(p => p.Alta, x => x.ToJson());
        builder.OwnsOne(p => p.Induccion, x =>
        {
            x.ToJson();
            x.Property(i => i.Estado).HasConversion<string>();
            x.Ignore(i => i.Avance);
            x.OwnsMany(i => i.Tareas, t => t.Property(y => y.Momento).HasConversion<string>());
            x.OwnsMany(i => i.Razones);
        });
        builder.Property(p => p.EmpleadoCreadoPor).HasMaxLength(200);
    }
}

/// <summary>Solo para `dotnet ef`.</summary>
internal sealed class VinculacionDbContextFactory : IDesignTimeDbContextFactory<VinculacionDbContext>
{
    public VinculacionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Selvana")
            ?? "Host=localhost;Port=5432;Database=selvana;Username=postgres";
        var options = new DbContextOptionsBuilder<VinculacionDbContext>();
        options.UseModulePostgres(connectionString, VinculacionDbContext.SchemaName);
        return new VinculacionDbContext(options.Options);
    }
}
