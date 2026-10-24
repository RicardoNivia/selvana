using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Contracts.Empresas;
using Selvana.Core.Domain.Empresas;
using Selvana.Core.Domain.Usuarios;
using Selvana.Core.Infrastructure.Persistence;

namespace Selvana.Core.Infrastructure.Empresas;

internal sealed class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("empresas");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.DomainEvents);
        builder.Property(e => e.Nombre).HasMaxLength(200);
        builder.Property(e => e.Nit).HasMaxLength(20);
        builder.HasMany(e => e.EmpresasUsuarias).WithOne().HasForeignKey("EmpresaId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.EmpresasUsuarias).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class EmpresaUsuariaConfiguration : IEntityTypeConfiguration<EmpresaUsuaria>
{
    public void Configure(EntityTypeBuilder<EmpresaUsuaria> builder)
    {
        builder.ToTable("empresas_usuarias");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Nombre).HasMaxLength(200);
        builder.Property(e => e.Nit).HasMaxLength(20);
        builder.Property(e => e.Sector).HasMaxLength(200);
        builder.Property(e => e.Ciudad).HasMaxLength(100);
        builder.HasIndex("EmpresaId", nameof(EmpresaUsuaria.Nit)).IsUnique();
    }
}

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Ignore(u => u.DomainEvents);
        builder.Ignore(u => u.RolCorregido);
        builder.Ignore(u => u.AdministraLaEmpresa);
        builder.Property(u => u.Correo).HasMaxLength(254);
        builder.Property(u => u.Nombre).HasMaxLength(200);
        builder.Property(u => u.CargoNombre).HasMaxLength(200);
        builder.Property(u => u.InvitadoPor).HasMaxLength(200);
        builder.Property(u => u.RazonRolIa).HasMaxLength(500);
        builder.Property(u => u.ContrasenaHash).HasMaxLength(200);
        builder.Property(u => u.InvitacionHash).HasMaxLength(64);
        builder.HasIndex(u => u.InvitacionHash).IsUnique().HasFilter("invitacion_hash IS NOT NULL");
        builder.Property(u => u.Rol).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.RolSugeridoIa).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Estado).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(u => new { u.EmpresaId, u.Correo }).IsUnique();
    }
}

internal sealed class DirectorioEmpresas(CoreDbContext db) : IDirectorioEmpresas
{
    public async Task<EmpresaResumen?> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        var empresa = await db.Set<Empresa>().AsNoTracking().Include(e => e.EmpresasUsuarias)
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);

        return empresa is null ? null : new EmpresaResumen(empresa.Id, empresa.Nombre, empresa.EsTemporal,
            [.. empresa.EmpresasUsuarias.OrderBy(u => u.Nombre).Select(u => new EmpresaUsuariaResumen(u.Id, u.Nombre, u.Ciudad, u.Activa))]);
    }
}

/// <summary>
/// Datos de desarrollo: la empresa de ejemplo (una EST) con sus empresas usuarias y tres usuarios para probar el ingreso y el alcance.
/// La contraseña sale de la configuración (user-secrets: Desarrollo:Contrasena), nunca del código.
/// </summary>
internal static class SemillaDesarrollo
{
    public static async Task SembrarAsync(CoreDbContext db, Guid empresaId, string usuario, string? contrasena, IHasherContrasenas hasher, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var empresa = await db.Set<Empresa>().Include(e => e.EmpresasUsuarias).FirstOrDefaultAsync(e => e.Id == empresaId);
        if (empresa is null)
        {
            empresa = Empresa.Crear(empresaId, "Servicios Andinos EST", "900123456", esTemporal: true).Value;
            empresa.AgregarEmpresaUsuaria("Contact Center Norte", "900412887", "BPO y contact center", "Bogotá", ahora);
            empresa.AgregarEmpresaUsuaria("Logística del Occidente", "830115902", "Logística y bodegaje", "Funza", ahora);
            empresa.AgregarEmpresaUsuaria("Alimentos La Sabana", "860044551", "Industria de alimentos", "Mosquera", ahora);
            empresa.AgregarEmpresaUsuaria("Clínica Santa María", "890903120", "Salud", "Medellín", ahora);
            db.Add(empresa);
        }

        Guid[] Ids(params string[] nombres) => [.. empresa.EmpresasUsuarias.Where(e => nombres.Contains(e.Nombre)).Select(e => e.Id)];

        // El analista solo ve dos empresas usuarias: sirve para probar que los datos de un cliente no aparecen en otro
        (string Correo, string Nombre, Rol Rol, Guid[]? Empresas)[] personas =
        [
            ("laura.moreno@serviciosandinos.co", usuario, Rol.GerenteRrhh, null),
            ("admin@serviciosandinos.co", "Andrés Rojas", Rol.Administrador, null),
            ("camilo.duarte@serviciosandinos.co", "Camilo Duarte", Rol.Analista, Ids("Contact Center Norte", "Logística del Occidente")),
        ];
        foreach (var (correo, nombre, rol, empresasUsuarias) in personas)
        {
            var existente = await db.Set<Usuario>().FirstOrDefaultAsync(u => u.EmpresaId == empresaId && u.Correo == correo);
            if (existente is null)
            {
                existente = Usuario.Invitar(empresaId, correo, nombre, rol, null, null, todasLasEmpresasUsuarias: empresasUsuarias is null,
                    empresasUsuarias ?? [], null, null, "Sistema", ahora).Value;
                db.Add(existente);
            }

            if (existente.ContrasenaHash is null && !string.IsNullOrWhiteSpace(contrasena))
            {
                existente.EstablecerContrasena(hasher.Calcular(contrasena));
            }
        }

        await db.SaveChangesAsync();
    }
}
