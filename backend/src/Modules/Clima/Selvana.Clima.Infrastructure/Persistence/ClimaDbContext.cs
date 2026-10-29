using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Clima.Application.Abstracciones;
using Selvana.Clima.Domain.Encuestas;
using Selvana.Clima.Domain.Pulsos;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Clima.Infrastructure.Persistence;

/// <summary>Unidad de trabajo del módulo Clima; sus tablas viven en el esquema "clima".</summary>
public sealed class ClimaDbContext(DbContextOptions<ClimaDbContext> options) : ModuleDbContext(options, SchemaName), IUnidadClima
{
    public const string SchemaName = "clima";
}

internal sealed class EncuestaConfiguration : IEntityTypeConfiguration<Encuesta>
{
    public void Configure(EntityTypeBuilder<Encuesta> builder)
    {
        builder.ToTable("encuestas");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.DomainEvents);
        builder.HasIndex(e => new { e.EmpresaId, e.CreadaEn });
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.Property(e => e.Titulo).HasMaxLength(150);
        builder.Property(e => e.PreguntaAbierta).HasMaxLength(300);
        builder.Property(e => e.TokenHash).HasMaxLength(100);
        builder.Property(e => e.CreadaPor).HasMaxLength(200);
        builder.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);
        builder.OwnsMany(e => e.Preguntas, p => p.ToJson());
        builder.OwnsOne(e => e.Analisis, a =>
        {
            a.ToJson();
            a.OwnsMany(x => x.Temas);
        });
    }
}

internal sealed class RespuestaClimaConfiguration : IEntityTypeConfiguration<RespuestaClima>
{
    public void Configure(EntityTypeBuilder<RespuestaClima> builder)
    {
        builder.ToTable("respuestas");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Ignore(r => r.DomainEvents);
        builder.HasIndex(r => r.EncuestaId);
        builder.Property(r => r.Area).HasMaxLength(200);
        builder.Property(r => r.Sede).HasMaxLength(200);
        builder.Property(r => r.Comentario).HasMaxLength(1000);
        builder.OwnsMany(r => r.Puntajes, p => p.ToJson());
    }
}

internal sealed class PulsoConfiguration : IEntityTypeConfiguration<Pulso>
{
    public void Configure(EntityTypeBuilder<Pulso> builder)
    {
        builder.ToTable("pulsos");
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Promedio);
        builder.HasIndex(p => new { p.EmpresaId, p.EmpleadoId, p.Hito }).IsUnique();
        builder.HasIndex(p => p.Documento);
        builder.Property(p => p.Nombre).HasMaxLength(200);
        builder.Property(p => p.Documento).HasMaxLength(20);
        builder.Property(p => p.Correo).HasMaxLength(200);
        builder.Property(p => p.Cargo).HasMaxLength(200);
        builder.Property(p => p.Area).HasMaxLength(200);
        builder.OwnsOne(p => p.Respuesta, r => r.ToJson());
        builder.OwnsOne(p => p.Seguimiento, s => s.ToJson());
    }
}

/// <summary>Solo para `dotnet ef`.</summary>
internal sealed class ClimaDbContextFactory : IDesignTimeDbContextFactory<ClimaDbContext>
{
    public ClimaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Selvana")
            ?? "Host=localhost;Port=5432;Database=selvana;Username=postgres";
        var options = new DbContextOptionsBuilder<ClimaDbContext>();
        options.UseModulePostgres(connectionString, ClimaDbContext.SchemaName);
        return new ClimaDbContext(options.Options);
    }
}
