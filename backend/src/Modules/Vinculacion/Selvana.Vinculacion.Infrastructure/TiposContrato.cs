using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Selvana.Core.Contracts.Empresas;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Domain.Procesos;
using Selvana.Vinculacion.Domain.TiposContrato;
using Selvana.Vinculacion.Infrastructure.Persistence;

namespace Selvana.Vinculacion.Infrastructure;

internal sealed class TipoContratoConfiguration : IEntityTypeConfiguration<TipoContratoEmpresa>
{
    public void Configure(EntityTypeBuilder<TipoContratoEmpresa> builder)
    {
        builder.ToTable("tipos_contrato");
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Ignore(t => t.DomainEvents);
        builder.Property(t => t.Nombre).HasMaxLength(TipoContratoEmpresa.NombreMaximo);
        builder.Property(t => t.Modalidad).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Descripcion).HasMaxLength(TipoContratoEmpresa.DescripcionMaxima);
        builder.Property(t => t.ActualizadoPor).HasMaxLength(200);
        builder.Property(t => t.PlantillaNombre).HasMaxLength(200);
        builder.Property(t => t.PlantillaTipoContenido).HasMaxLength(100);
        builder.HasIndex(t => new { t.EmpresaId, t.Nombre }).IsUnique();
    }
}

internal sealed class TiposContratoRepositorio(VinculacionDbContext db) : ITiposContrato
{
    public Task<TipoContratoEmpresa?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<TipoContratoEmpresa>().FirstOrDefaultAsync(t => t.EmpresaId == empresaId && t.Id == id, cancellationToken);

    public Task<TipoContratoEmpresa?> ActivoDeModalidadAsync(Guid empresaId, TipoContrato modalidad, CancellationToken cancellationToken) =>
        db.Set<TipoContratoEmpresa>().AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && t.Activo && t.Modalidad == modalidad)
            .OrderByDescending(t => t.PorDefecto).ThenBy(t => t.Nombre)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExisteNombreAsync(Guid empresaId, string nombre, Guid? excepto, CancellationToken cancellationToken) =>
        db.Set<TipoContratoEmpresa>().AnyAsync(t => t.EmpresaId == empresaId && t.Id != excepto
            && EF.Functions.ILike(t.Nombre, nombre.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)),
            cancellationToken);

    public Task<int> ContarAsync(Guid empresaId, CancellationToken cancellationToken) =>
        db.Set<TipoContratoEmpresa>().CountAsync(t => t.EmpresaId == empresaId, cancellationToken);

    public void Agregar(TipoContratoEmpresa tipo) => db.Add(tipo);
}

internal sealed class LecturaTiposContrato(VinculacionDbContext db) : ILecturaTiposContrato
{
    public async Task<IReadOnlyList<TipoContratoDto>> DeLaEmpresaAsync(Guid empresaId, bool soloActivos, CancellationToken cancellationToken)
    {
        var filas = await db.Set<TipoContratoEmpresa>().AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && (!soloActivos || t.Activo))
            .OrderByDescending(t => t.Activo).ThenBy(t => t.Nombre)
            .ToListAsync(cancellationToken);
        return [.. filas.Select(t => new TipoContratoDto(t.Id, t.Nombre, t.Modalidad.ToString(), ReglasContrato.Nombre(t.Modalidad), t.Descripcion, t.Activo,
            t.PorDefecto, t.PlantillaNombre, t.ActualizadoEn, t.ActualizadoPor))];
    }
}

/// <summary>Siembra los tipos de contrato por defecto en cada empresa que aún no tiene ninguno (idempotente; corre al arrancar).</summary>
public static class SemillaTiposContrato
{
    public static async Task SembrarTiposContratoAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<VinculacionDbContext>();
        var ahora = sp.GetRequiredService<TimeProvider>().GetUtcNow();
        var conTipos = await db.Set<TipoContratoEmpresa>().Select(t => t.EmpresaId).Distinct().ToListAsync(cancellationToken);
        foreach (var (id, esTemporal) in await sp.GetRequiredService<IDirectorioEmpresas>().TodasAsync(cancellationToken))
        {
            if (!conTipos.Contains(id))
            {
                db.AddRange(TipoContratoEmpresa.PorDefectoPara(id, esTemporal, ahora));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
