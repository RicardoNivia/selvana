using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Desempeno.Application.Abstracciones;
using Selvana.Desempeno.Domain.Ciclos;
using Selvana.Desempeno.Domain.Evaluaciones;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Desempeno.Infrastructure.Persistence;

/// <summary>Unidad de trabajo del módulo Desempeño; sus tablas viven en el esquema "desempeno".</summary>
public sealed class DesempenoDbContext(DbContextOptions<DesempenoDbContext> options) : ModuleDbContext(options, SchemaName), IUnidadDesempeno
{
    public const string SchemaName = "desempeno";
}

internal sealed class CicloConfiguration : IEntityTypeConfiguration<Ciclo>
{
    public void Configure(EntityTypeBuilder<Ciclo> builder)
    {
        builder.ToTable("ciclos");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Ignore(c => c.DomainEvents);
        builder.HasIndex(c => new { c.EmpresaId, c.CreadoEn });
        builder.Property(c => c.Nombre).HasMaxLength(150);
        builder.Property(c => c.CreadoPor).HasMaxLength(200);
        builder.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Alcance).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
        builder.OwnsMany(c => c.Formularios, f =>
        {
            f.ToJson();
            f.OwnsMany(x => x.Competencias);
            f.OwnsMany(x => x.Objetivos);
            f.OwnsMany(x => x.Razones);
        });
    }
}

internal sealed class EvaluacionConfiguration : IEntityTypeConfiguration<Evaluacion>
{
    public void Configure(EntityTypeBuilder<Evaluacion> builder)
    {
        builder.ToTable("evaluaciones");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.Caja);
        builder.Ignore(e => e.LlevaAutoevaluacion);
        builder.HasIndex(e => new { e.EmpresaId, e.CicloId, e.Nombre });
        builder.HasIndex(e => new { e.EmpresaId, e.EmpleadoId });
        builder.HasIndex(e => e.Documento);
        builder.Property(e => e.Codigo).HasMaxLength(20);
        builder.Property(e => e.Nombre).HasMaxLength(200);
        builder.Property(e => e.Documento).HasMaxLength(20);
        builder.Property(e => e.Correo).HasMaxLength(200);
        builder.Property(e => e.Cargo).HasMaxLength(200);
        builder.Property(e => e.Area).HasMaxLength(200);
        builder.Property(e => e.Sede).HasMaxLength(200);
        builder.Property(e => e.CalificadaPor).HasMaxLength(200);
        builder.Property(e => e.Puntaje).HasPrecision(4, 2);
        builder.Property(e => e.Alcance).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Potencial).HasConversion<string>().HasMaxLength(10);
        builder.OwnsMany(e => e.Objetivos, o =>
        {
            o.ToJson();
            o.OwnsMany(x => x.Evidencias);
        });
        builder.OwnsMany(e => e.Competencias, c => c.ToJson());
        builder.OwnsOne(e => e.Borrador, b =>
        {
            b.ToJson();
            b.OwnsMany(x => x.Razones);
        });
        builder.OwnsOne(e => e.Ajuste, a => a.ToJson());
        builder.OwnsMany(e => e.Plan, p => p.ToJson());
    }
}

/// <summary>Solo para `dotnet ef`.</summary>
internal sealed class DesempenoDbContextFactory : IDesignTimeDbContextFactory<DesempenoDbContext>
{
    public DesempenoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Selvana")
            ?? "Host=localhost;Port=5432;Database=selvana;Username=postgres";
        var options = new DbContextOptionsBuilder<DesempenoDbContext>();
        options.UseModulePostgres(connectionString, DesempenoDbContext.SchemaName);
        return new DesempenoDbContext(options.Options);
    }
}
