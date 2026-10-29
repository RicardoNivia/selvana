using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure.Pruebas;

internal sealed class BateriaCargoConfiguration : IEntityTypeConfiguration<BateriaCargo>
{
    public void Configure(EntityTypeBuilder<BateriaCargo> builder)
    {
        builder.ToTable("baterias_cargo");
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Ignore(b => b.DomainEvents);
        builder.Property(b => b.ClaveCargo).HasMaxLength(200);
        builder.Property(b => b.CargoNombre).HasMaxLength(200);
        builder.Property(b => b.ActualizadaPor).HasMaxLength(200);
        builder.OwnsMany(b => b.Pruebas, p => p.ToJson());
        builder.HasIndex(b => new { b.EmpresaId, b.ClaveCargo }).IsUnique();
    }
}

internal sealed class AplicacionPruebasConfiguration : IEntityTypeConfiguration<AplicacionPruebas>
{
    public void Configure(EntityTypeBuilder<AplicacionPruebas> builder)
    {
        builder.ToTable("aplicaciones_pruebas");
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Ignore(a => a.DomainEvents);
        builder.Property(a => a.Titular).HasMaxLength(200);
        builder.Property(a => a.TokenHuella).HasMaxLength(64);
        builder.Property(a => a.EnviadaPor).HasMaxLength(200);
        builder.Property(a => a.Estado).HasConversion<string>().HasMaxLength(20);
        builder.OwnsMany(a => a.Pruebas, p =>
        {
            p.ToJson();
            p.Property(x => x.Estado).HasConversion<string>();
            p.OwnsMany(x => x.Respuestas);
            p.OwnsMany(x => x.Dimensiones);
        });
        builder.HasIndex(a => a.TokenHuella).IsUnique();
        builder.HasIndex(a => new { a.EmpresaId, a.PostulacionId }).IsUnique();
        builder.HasIndex(a => new { a.EmpresaId, a.VacanteId });
    }
}

internal sealed class BateriasCargoRepositorio(ReclutamientoDbContext db) : IBateriasCargo
{
    public Task<BateriaCargo?> ObtenerAsync(Guid empresaId, string claveCargo, CancellationToken cancellationToken) =>
        db.Set<BateriaCargo>().FirstOrDefaultAsync(b => b.EmpresaId == empresaId && b.ClaveCargo == claveCargo, cancellationToken);

    public void Agregar(BateriaCargo bateria) => db.Add(bateria);
}

internal sealed class AplicacionesPruebasRepositorio(ReclutamientoDbContext db) : IAplicacionesPruebas
{
    public Task<AplicacionPruebas?> ObtenerPorTokenAsync(string huella, CancellationToken cancellationToken) =>
        db.Set<AplicacionPruebas>().FirstOrDefaultAsync(a => a.TokenHuella == huella, cancellationToken);

    public Task<AplicacionPruebas?> ObtenerPorPostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken) =>
        db.Set<AplicacionPruebas>().FirstOrDefaultAsync(a => a.EmpresaId == empresaId && a.PostulacionId == postulacionId, cancellationToken);

    public Task<AplicacionPruebas?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<AplicacionPruebas>().FirstOrDefaultAsync(a => a.EmpresaId == empresaId && a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AplicacionPruebas>> DeVacanteAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken) =>
        await db.Set<AplicacionPruebas>().AsNoTracking().Where(a => a.EmpresaId == empresaId && a.VacanteId == vacanteId).ToListAsync(cancellationToken);

    public void Agregar(AplicacionPruebas aplicacion) => db.Add(aplicacion);
}

/// <summary>32 bytes aleatorios en Base64 URL; se guarda solo la huella SHA-256.</summary>
internal sealed class TokensPruebas : ITokensPruebas
{
    public (string Token, string Huella) Generar()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Huella(token));
    }

    public string Huella(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
}

internal sealed class LecturaPruebas(ReclutamientoDbContext db) : ILecturaPruebas
{
    public async Task<IReadOnlyList<ResultadoPostulanteDto>> ResultadosAsync(Guid empresaId, Guid vacanteId, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var aplicaciones = await db.Set<AplicacionPruebas>().AsNoTracking().Where(a => a.EmpresaId == empresaId && a.VacanteId == vacanteId)
            .OrderByDescending(a => a.EnviadaEn).ToListAsync(cancellationToken);
        return [.. aplicaciones.Select(a =>
        {
            var pruebas = a.Pruebas.Select(p =>
            {
                var d = SuitePsicotecnica.Buscar(p.Codigo);
                return new ResultadoPruebaDto(p.Codigo, d?.Nombre ?? p.Codigo, p.Estado.ToString(), p.Puntaje, p.Corte,
                    p.Puntaje is { } x && p.Corte is { } corte ? x >= corte : null, p.Peso, d?.Sensible ?? false, d?.Validada ?? false, p.Alertas);
            }).ToList();
            var provisional = pruebas.Any(p => p.Estado == nameof(EstadoPrueba.Terminada) && !p.Validada);
            return new ResultadoPostulanteDto(a.Id, a.PostulacionId, a.Titular, a.Estado.ToString(), a.EnviadaEn, a.VenceEn, a.Vencida(ahora), a.PuntajeTotal(),
                provisional, pruebas);
        })];
    }

    public async Task<(string Empresa, string Vacante)?> VacanteAsync(Guid vacanteId, CancellationToken cancellationToken)
    {
        var v = await db.Set<Vacante>().AsNoTracking().Where(x => x.Id == vacanteId)
            .Select(x => new { Empresa = x.EmpresaUsuariaNombre ?? x.EmpresaNombre, x.Titulo }).FirstOrDefaultAsync(cancellationToken);
        return v is null ? null : (v.Empresa, v.Titulo);
    }
}
