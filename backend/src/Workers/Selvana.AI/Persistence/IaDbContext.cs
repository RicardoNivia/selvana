using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.AI.Evaluacion;
using Selvana.AI.Gateway;
using Selvana.AI.Motor;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.AI.Persistence;

/// <summary>Datos de la plataforma de IA; viven en el esquema "ia".</summary>
public sealed class IaDbContext(DbContextOptions<IaDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "ia";

    public DbSet<UsoModelo> UsosModelo => Set<UsoModelo>();

    public DbSet<EvaluacionAgente> Evaluaciones => Set<EvaluacionAgente>();

    public DbSet<MemoriaCaso> MemoriasCaso => Set<MemoriaCaso>();

    public DbSet<AccionAgente> AccionesAgente => Set<AccionAgente>();
}

internal sealed class MemoriaCasoConfiguration : IEntityTypeConfiguration<MemoriaCaso>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<MemoriaCaso> builder)
    {
        builder.ToTable("memorias_caso");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Agente).HasMaxLength(100);
        builder.Property(m => m.Caso).HasMaxLength(200);
        builder.Property(m => m.Hechos).HasColumnType("jsonb").HasConversion(
            d => JsonSerializer.Serialize(d, Json),
            t => JsonSerializer.Deserialize<Dictionary<string, string>>(t, Json) ?? new(),
            new ValueComparer<Dictionary<string, string>>(
                (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                d => JsonSerializer.Serialize(d, Json).GetHashCode(StringComparison.Ordinal),
                d => new Dictionary<string, string>(d)));
        builder.HasIndex(m => new { m.EmpresaId, m.Agente, m.Caso }).IsUnique();
    }
}

internal sealed class AccionAgenteConfiguration : IEntityTypeConfiguration<AccionAgente>
{
    public void Configure(EntityTypeBuilder<AccionAgente> builder)
    {
        builder.ToTable("acciones_agentes");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Agente).HasMaxLength(100);
        builder.Property(a => a.Caso).HasMaxLength(200);
        builder.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Nombre).HasMaxLength(100);
        builder.Property(a => a.Detalle).HasMaxLength(1000);
        builder.Property(a => a.CostoUsd).HasPrecision(18, 8);
        builder.HasIndex(a => new { a.EmpresaId, a.Caso, a.OcurridoEn });
        builder.HasIndex(a => a.EjecucionId);
    }
}

internal sealed class MemoriaCasosPostgres(IDbContextFactory<IaDbContext> fabrica) : IMemoriaCasos
{
    public async Task<MemoriaCaso?> BuscarAsync(Guid empresaId, string agente, string caso, CancellationToken cancellationToken)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.MemoriasCaso.AsNoTracking()
            .FirstOrDefaultAsync(m => m.EmpresaId == empresaId && m.Agente == agente && m.Caso == caso, cancellationToken).ConfigureAwait(false);
    }

    public async Task GuardarAsync(MemoriaCaso memoria, CancellationToken cancellationToken)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existe = await db.MemoriasCaso.AnyAsync(m => m.Id == memoria.Id, cancellationToken).ConfigureAwait(false);
        if (existe)
        {
            db.MemoriasCaso.Update(memoria);
        }
        else
        {
            db.MemoriasCaso.Add(memoria);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class RegistroAccionesAgentePostgres(IDbContextFactory<IaDbContext> fabrica) : IRegistroAccionesAgente
{
    public async Task RegistrarAsync(IReadOnlyList<AccionAgente> acciones, CancellationToken cancellationToken)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.AccionesAgente.AddRange(acciones);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class EvaluacionAgenteConfiguration : IEntityTypeConfiguration<EvaluacionAgente>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<EvaluacionAgente> builder)
    {
        builder.ToTable("evaluaciones_agentes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Agente).HasMaxLength(100);
        builder.Property(e => e.Modelos).HasMaxLength(300);
        builder.Property(e => e.CostoUsd).HasPrecision(18, 8);
        var comparador = new ValueComparer<Dictionary<string, double>>(
            (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            d => JsonSerializer.Serialize(d, Json).GetHashCode(StringComparison.Ordinal),
            d => new Dictionary<string, double>(d));
        foreach (var propiedad in new[] { nameof(EvaluacionAgente.Metricas), nameof(EvaluacionAgente.Metas) })
        {
            builder.Property<Dictionary<string, double>>(propiedad).HasColumnType("jsonb")
                .HasConversion(d => JsonSerializer.Serialize(d, Json), t => JsonSerializer.Deserialize<Dictionary<string, double>>(t, Json) ?? new(), comparador);
        }

        builder.HasIndex(e => new { e.Agente, e.Fecha });
    }
}

internal sealed class UsoModeloConfiguration : IEntityTypeConfiguration<UsoModelo>
{
    public void Configure(EntityTypeBuilder<UsoModelo> builder)
    {
        builder.ToTable("usos_modelo");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Agente).HasMaxLength(100);
        builder.Property(u => u.Proveedor).HasMaxLength(50);
        builder.Property(u => u.Modelo).HasMaxLength(100);
        builder.Property(u => u.Nivel).HasConversion<string>().HasMaxLength(10);
        builder.Property(u => u.Falla).HasMaxLength(500);
        builder.Property(u => u.CostoUsd).HasPrecision(18, 8);
        builder.HasIndex(u => new { u.EmpresaId, u.OcurridoEn });
    }
}

internal sealed class RegistroUsoIaPostgres(IDbContextFactory<IaDbContext> fabrica) : IRegistroUsoIa
{
    public async Task RegistrarAsync(UsoModelo uso, CancellationToken cancellationToken)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.UsosModelo.Add(uso);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Solo para `dotnet ef`.</summary>
internal sealed class IaDbContextFactory : IDesignTimeDbContextFactory<IaDbContext>
{
    public IaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Selvana")
            ?? "Host=localhost;Port=5432;Database=selvana;Username=postgres";

        var options = new DbContextOptionsBuilder<IaDbContext>();
        options.UseModulePostgres(connectionString, IaDbContext.SchemaName);
        return new IaDbContext(options.Options);
    }
}
