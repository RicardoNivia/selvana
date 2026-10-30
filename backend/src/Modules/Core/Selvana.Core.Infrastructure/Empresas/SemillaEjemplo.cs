using Microsoft.EntityFrameworkCore;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Contracts.Empresas;
using Selvana.Core.Domain.Empresas;
using Selvana.Core.Domain.Usuarios;
using Selvana.Core.Infrastructure.Persistence;

namespace Selvana.Core.Infrastructure.Empresas;

/// <summary>Quién entra a las empresas de ejemplo: el dueño del ambiente (correo y contraseña de su configuración) y personas ficticias.</summary>
public sealed record AccesoEjemplo(string CorreoDueno, string NombreDueno, string ContrasenaDueno, string ContrasenaFicticios);

/// <summary>
/// Datos de ejemplo para el ambiente de pruebas en línea: tres empresas independientes (una EST con dos empresas usuarias,
/// una de logística y una clínica) con sus usuarios. Los Ids son fijos: si la empresa ya existe no se toca.
/// </summary>
internal static class SemillaEjemplo
{
    public static readonly Guid TalentoAndino = Guid.Parse("0192f0a1-5e1c-7b11-9a8d-5e1ec7a0e101");
    public static readonly Guid LogisticaCaribe = Guid.Parse("0192f0a1-5e1c-7b11-9a8d-5e1ec7a0e102");
    public static readonly Guid ClinicaDelValle = Guid.Parse("0192f0a1-5e1c-7b11-9a8d-5e1ec7a0e103");

    public static async Task<IReadOnlyList<EmpresaResumen>> SembrarAsync(CoreDbContext db, AccesoEjemplo acceso, IHasherContrasenas hasher, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();

        var talento = await EmpresaAsync(db, TalentoAndino, "Talento Andino EST S.A.S.", "901734215-5", esTemporal: true, ahora,
            ("Retail Express Colombia", "900687342-7", "Comercio minorista", "Bogotá"),
            ("Agroindustrial del Tolima", "805214763-0", "Agroindustria", "Ibagué"));
        var logistica = await EmpresaAsync(db, LogisticaCaribe, "Logística Caribe S.A.S.", "901456781-1", esTemporal: false, ahora);
        var clinica = await EmpresaAsync(db, ClinicaDelValle, "Clínica del Valle IPS", "830562194-2", esTemporal: false, ahora);

        await UsuarioAsync(db, hasher, TalentoAndino, acceso.CorreoDueno, acceso.NombreDueno, Rol.Administrador, acceso.ContrasenaDueno, ahora);
        await UsuarioAsync(db, hasher, TalentoAndino, "valentina.rios@talentoandino.co", "Valentina Ríos", Rol.GerenteRrhh, acceso.ContrasenaFicticios, ahora);
        await UsuarioAsync(db, hasher, LogisticaCaribe, "mateo.herrera@logisticacaribe.co", "Mateo Herrera", Rol.Administrador, acceso.ContrasenaFicticios, ahora);
        await UsuarioAsync(db, hasher, ClinicaDelValle, "juliana.pardo@clinicadelvalle.co", "Juliana Pardo", Rol.Administrador, acceso.ContrasenaFicticios, ahora);

        // Líderes de área para «Mi equipo» (E4): sus equipos los arma la semilla de Expediente por área
        await UsuarioAsync(db, hasher, TalentoAndino, "andres.gomez@talentoandino.co", "Andrés Gómez", Rol.Lider, acceso.ContrasenaFicticios, ahora);
        await UsuarioAsync(db, hasher, TalentoAndino, "carolina.mejia@talentoandino.co", "Carolina Mejía", Rol.Lider, acceso.ContrasenaFicticios, ahora);
        await UsuarioAsync(db, hasher, LogisticaCaribe, "jorge.castro@logisticacaribe.co", "Jorge Castro", Rol.Lider, acceso.ContrasenaFicticios, ahora);
        await UsuarioAsync(db, hasher, ClinicaDelValle, "diana.ruiz@clinicadelvalle.co", "Diana Ruiz", Rol.Lider, acceso.ContrasenaFicticios, ahora);

        await db.SaveChangesAsync();
        return [Resumen(talento), Resumen(logistica), Resumen(clinica)];
    }

    private static async Task<Empresa> EmpresaAsync(CoreDbContext db, Guid id, string nombre, string nit, bool esTemporal, DateTimeOffset ahora,
        params (string Nombre, string Nit, string Sector, string Ciudad)[] usuarias)
    {
        var empresa = await db.Set<Empresa>().Include(e => e.EmpresasUsuarias).FirstOrDefaultAsync(e => e.Id == id);
        if (empresa is not null)
        {
            return empresa;
        }

        empresa = Empresa.Crear(id, nombre, nit, esTemporal).Value;
        foreach (var u in usuarias)
        {
            empresa.AgregarEmpresaUsuaria(u.Nombre, u.Nit, u.Sector, u.Ciudad, ahora);
        }

        db.Add(empresa);
        return empresa;
    }

    private static async Task UsuarioAsync(CoreDbContext db, IHasherContrasenas hasher, Guid empresaId, string correo, string nombre, Rol rol, string contrasena,
        DateTimeOffset ahora)
    {
        var normalizado = Usuario.NormalizarCorreo(correo);
        if (await db.Set<Usuario>().AnyAsync(u => u.EmpresaId == empresaId && u.Correo == normalizado))
        {
            return;
        }

        var usuario = Usuario.Invitar(empresaId, correo, nombre, rol, null, null, todasLasEmpresasUsuarias: true, [], null, null, "Sistema", ahora).Value;
        usuario.EstablecerContrasena(hasher.Calcular(contrasena));
        db.Add(usuario);
    }

    private static EmpresaResumen Resumen(Empresa e) =>
        new(e.Id, e.Nombre, e.EsTemporal, [.. e.EmpresasUsuarias.OrderBy(u => u.Nombre).Select(u => new EmpresaUsuariaResumen(u.Id, u.Nombre, u.Ciudad, u.Activa))]);
}
