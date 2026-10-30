using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Expediente.Infrastructure.Persistence;

/// <summary>Unidad de trabajo del módulo Expediente; sus tablas viven en el esquema "expediente".</summary>
public sealed class ExpedienteDbContext(DbContextOptions<ExpedienteDbContext> options) : ModuleDbContext(options, SchemaName), IUnidadExpediente
{
    public const string SchemaName = "expediente";
}

internal sealed class EmpleadoConfiguration : IEntityTypeConfiguration<Empleado>
{
    public void Configure(EntityTypeBuilder<Empleado> builder)
    {
        builder.ToTable("empleados");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.Situacion);
        builder.Property(e => e.Codigo).HasMaxLength(20);
        builder.HasIndex(e => e.Codigo).IsUnique();
        builder.HasIndex(e => new { e.EmpresaId, e.Estado, e.Nombre });
        builder.HasIndex(e => new { e.EmpresaId, e.Documento });
        builder.HasIndex(e => e.ProcesoVinculacionId);
        builder.Property(e => e.Documento).HasMaxLength(20);
        builder.Property(e => e.Nombre).HasMaxLength(200);
        builder.Property(e => e.Correo).HasMaxLength(200);
        builder.Property(e => e.Celular).HasMaxLength(30);
        builder.Property(e => e.Ciudad).HasMaxLength(120);
        builder.Property(e => e.Origen).HasMaxLength(30);
        builder.Property(e => e.Cargo).HasMaxLength(200);
        builder.Property(e => e.Area).HasMaxLength(200);
        builder.Property(e => e.CentroCosto).HasMaxLength(200);
        builder.Property(e => e.Sede).HasMaxLength(200);
        builder.Property(e => e.EmpresaUsuaria).HasMaxLength(200);
        builder.Property(e => e.TipoContrato).HasMaxLength(100);
        builder.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.MotivoRetiro).HasMaxLength(500);
        builder.Property(e => e.LiderNombre).HasMaxLength(200);
        builder.HasIndex(e => new { e.EmpresaId, e.LiderId });
        builder.OwnsOne(e => e.Afiliaciones, x => x.ToJson());
        builder.OwnsOne(e => e.CambioPropuesto, x => x.ToJson());
        builder.HasIndex(e => e.Documento);
        builder.OwnsMany(e => e.Novedades, x =>
        {
            x.ToJson();
            x.Property(n => n.Tipo).HasConversion<string>();
        });
        builder.OwnsMany(e => e.Documentos, x =>
        {
            x.ToJson();
            x.Ignore(d => d.Vigente);
            x.OwnsMany(d => d.Versiones, v => v.OwnsOne(y => y.Lectura, l =>
            {
                l.Property(z => z.Estado).HasConversion<string>();
                l.OwnsMany(z => z.Campos);
                l.OwnsMany(z => z.Razones);
                l.OwnsMany(z => z.Cambios);
            }));
        });
    }
}

/// <summary>Solo para `dotnet ef`.</summary>
internal sealed class ExpedienteDbContextFactory : IDesignTimeDbContextFactory<ExpedienteDbContext>
{
    public ExpedienteDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Selvana")
            ?? "Host=localhost;Port=5432;Database=selvana;Username=postgres";
        var options = new DbContextOptionsBuilder<ExpedienteDbContext>();
        options.UseModulePostgres(connectionString, ExpedienteDbContext.SchemaName);
        return new ExpedienteDbContext(options.Options);
    }
}
