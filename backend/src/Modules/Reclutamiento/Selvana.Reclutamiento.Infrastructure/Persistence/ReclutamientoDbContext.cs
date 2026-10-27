using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Infrastructure.Common.Persistence;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Postulaciones;
using Selvana.Reclutamiento.Domain.Vacantes;

namespace Selvana.Reclutamiento.Infrastructure.Persistence;

/// <summary>Unidad de trabajo del módulo Reclutamiento; sus tablas viven en el esquema "reclutamiento".</summary>
public sealed class ReclutamientoDbContext(DbContextOptions<ReclutamientoDbContext> options)
    : ModuleDbContext(options, SchemaName), IUnidadReclutamiento
{
    public const string SchemaName = "reclutamiento";
}

/// <summary>Hoja de vida guardada cifrada (Data Protection). Es un detalle de infraestructura: el dominio solo conoce su Id.</summary>
internal sealed class ArchivoHojaDeVida
{
    public Guid Id { get; init; }
    public Guid EmpresaId { get; init; }
    public string Nombre { get; init; } = "";
    public string TipoContenido { get; init; } = "";
    public int Tamano { get; init; }
    public byte[] ContenidoCifrado { get; init; } = [];
    public DateTimeOffset SubidoEn { get; init; }
}

internal sealed class ArchivoConfiguration : IEntityTypeConfiguration<ArchivoHojaDeVida>
{
    public void Configure(EntityTypeBuilder<ArchivoHojaDeVida> builder)
    {
        builder.ToTable("archivos_hojas_vida");
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Nombre).HasMaxLength(260);
        builder.Property(a => a.TipoContenido).HasMaxLength(120);
    }
}

internal sealed class VacanteConfiguration : IEntityTypeConfiguration<Vacante>
{
    public void Configure(EntityTypeBuilder<Vacante> builder)
    {
        builder.ToTable("vacantes");
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Ignore(v => v.DomainEvents);
        builder.Ignore(v => v.EmpresaVisible);
        builder.Ignore(v => v.RecibePostulaciones);
        builder.Property(v => v.Codigo).HasMaxLength(30);
        builder.HasIndex(v => v.Codigo).IsUnique();
        builder.HasIndex(v => new { v.Estado, v.PublicadaEn });
        builder.HasIndex(v => v.EmpresaId);
        builder.Property(v => v.EmpresaNombre).HasMaxLength(200);
        builder.Property(v => v.EmpresaUsuariaNombre).HasMaxLength(200);
        builder.Property(v => v.Frase).HasMaxLength(1000);
        builder.Property(v => v.Titulo).HasMaxLength(200);
        builder.Property(v => v.Cargo).HasMaxLength(200);
        builder.Property(v => v.Ciudad).HasMaxLength(100);
        builder.Property(v => v.CodigoMunicipio).HasMaxLength(5);
        builder.Property(v => v.Jornada).HasMaxLength(200);
        builder.Property(v => v.Descripcion).HasMaxLength(4000);
        builder.Property(v => v.JustificacionSalario).HasMaxLength(1000);
        builder.Property(v => v.SalarioMinimo).HasPrecision(14, 2);
        builder.Property(v => v.SalarioMaximo).HasPrecision(14, 2);
        builder.Property(v => v.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.CreadaPor).HasMaxLength(200);
        builder.Property(v => v.ModeloIa).HasMaxLength(100);
        builder.Property(v => v.MensajeIa).HasMaxLength(1000);
        builder.OwnsMany(v => v.Razones, r => r.ToJson());
        builder.OwnsOne(v => v.Filtros, f =>
        {
            f.ToJson();
            f.Ignore(x => x.Vacios);
        });
        builder.OwnsMany(v => v.Pruebas, p => p.ToJson());
        builder.OwnsMany(v => v.Aprobaciones, a =>
        {
            a.ToJson();
            a.Property(x => x.Etapa).HasConversion<string>();
        });
        builder.Property(v => v.EtapaPendiente).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.AprobacionPedidaPor).HasMaxLength(200);
        builder.Property(v => v.MotivoDevolucion).HasMaxLength(500);
    }
}

internal sealed class CandidatoConfiguration : IEntityTypeConfiguration<Candidato>
{
    public void Configure(EntityTypeBuilder<Candidato> builder)
    {
        builder.ToTable("candidatos");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Ignore(c => c.DomainEvents);
        builder.Property(c => c.Documento).HasMaxLength(20);
        builder.Property(c => c.TextoBusqueda).HasMaxLength(4000);
        builder.Ignore(c => c.Suprimido);
        builder.HasIndex(c => new { c.EmpresaId, c.Documento }).IsUnique();
        builder.OwnsOne(c => c.Datos, d =>
        {
            d.ToJson();
            d.OwnsMany(x => x.Experiencia);
            d.OwnsMany(x => x.Formacion);
        });
        builder.HasMany(c => c.Postulaciones).WithOne().HasForeignKey("CandidatoId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Postulaciones).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PostulacionConfiguration : IEntityTypeConfiguration<Postulacion>
{
    public void Configure(EntityTypeBuilder<Postulacion> builder)
    {
        builder.ToTable("postulaciones");
        builder.OwnsOne(p => p.Afinidad, a =>
        {
            a.ToJson();
            a.Property(x => x.Estado).HasConversion<string>();
            a.OwnsMany(x => x.Razones);
            a.Ignore(x => x.Puntaje);
        });
        builder.Property(p => p.Etapa).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EtapaPostulacion.Recibida);
        builder.Property(p => p.MotivoDescarte).HasMaxLength(500);
        builder.Property(p => p.ContratacionAprobadaPor).HasMaxLength(200);
        builder.Property(p => p.RescatadaPor).HasMaxLength(200);
        builder.OwnsOne(p => p.Retroalimentacion, r =>
        {
            r.ToJson();
            r.Property(x => x.Valor).HasConversion<string>();
        });
        builder.OwnsMany(p => p.Historial, h =>
        {
            h.ToJson();
            h.Property(x => x.De).HasConversion<string>();
            h.Property(x => x.A).HasConversion<string>();
        });
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Numero).HasMaxLength(20);
        builder.HasIndex(p => p.Numero).IsUnique();
        builder.HasIndex(p => new { p.VacanteId, p.RecibidaEn });
        builder.Property(p => p.Canal).HasConversion<string>().HasMaxLength(20);
        builder.OwnsMany(p => p.Respuestas, r => r.ToJson());
        builder.OwnsOne(p => p.Autorizacion, a => a.ToJson());
    }
}

internal sealed class BorradorConfiguration : IEntityTypeConfiguration<BorradorPostulacion>
{
    public void Configure(EntityTypeBuilder<BorradorPostulacion> builder)
    {
        builder.ToTable("borradores_postulacion");
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Ignore(b => b.DomainEvents);
        builder.Property(b => b.NombreArchivo).HasMaxLength(260);
        builder.Property(b => b.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Mensaje).HasMaxLength(1000);
        builder.Property(b => b.ModeloIa).HasMaxLength(100);
        builder.OwnsOne(b => b.DatosLeidos, d =>
        {
            d.ToJson();
            d.OwnsMany(x => x.Experiencia);
            d.OwnsMany(x => x.Formacion);
        });
    }
}

/// <summary>Solo para `dotnet ef`.</summary>
internal sealed class ReclutamientoDbContextFactory : IDesignTimeDbContextFactory<ReclutamientoDbContext>
{
    public ReclutamientoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Selvana")
            ?? "Host=localhost;Port=5432;Database=selvana;Username=postgres";
        var options = new DbContextOptionsBuilder<ReclutamientoDbContext>();
        options.UseModulePostgres(connectionString, ReclutamientoDbContext.SchemaName);
        return new ReclutamientoDbContext(options.Options);
    }
}
