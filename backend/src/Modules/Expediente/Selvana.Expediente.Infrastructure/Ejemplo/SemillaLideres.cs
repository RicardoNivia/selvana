using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Empresas;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Expediente.Infrastructure.Persistence;

namespace Selvana.Expediente.Infrastructure.Ejemplo;

/// <summary>
/// E4 · Equipos de ejemplo: cada área de la planta de ejemplo con su líder (los usuarios con rol Líder de la semilla de Core, y el dueño del ambiente
/// en Gestión humana para que vea «Mi equipo» con su propia cuenta). Idempotente: solo asigna a quien aún no tiene líder.
/// </summary>
internal static class SemillaLideres
{
    private const string Dueno = "{dueno}";

    // En el orden de las empresas de ejemplo: Talento Andino, Logística Caribe, Clínica del Valle
    private static readonly (string Area, string Correo)[][] Equipos =
    [
        [("Comercial", "andres.gomez@talentoandino.co"), ("Tiendas", "andres.gomez@talentoandino.co"), ("Producción", "carolina.mejia@talentoandino.co"),
         ("Logística", "carolina.mejia@talentoandino.co"), ("Gestión humana", Dueno)],
        [("Bodega", "jorge.castro@logisticacaribe.co"), ("Transporte", "mateo.herrera@logisticacaribe.co")],
        [("Urgencias", "diana.ruiz@clinicadelvalle.co"), ("Hospitalización", "diana.ruiz@clinicadelvalle.co"), ("Admisiones", "juliana.pardo@clinicadelvalle.co"),
         ("Farmacia", "juliana.pardo@clinicadelvalle.co")],
    ];

    public static async Task<int> AsignarAsync(ExpedienteDbContext db, IDirectorioUsuarios directorio, IReadOnlyList<EmpresaResumen> empresas, string correoDueno,
        TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var asignados = 0;
        for (var n = 0; n < empresas.Count && n < Equipos.Length; n++)
        {
            var usuarios = (await directorio.ConsolaAsync(empresas[n].Id, CancellationToken.None))
                .ToDictionary(u => u.Correo, StringComparer.OrdinalIgnoreCase);
            var sinLider = await db.Set<Empleado>().Where(e => e.EmpresaId == empresas[n].Id && e.Estado == EstadoEmpleado.Activo && e.LiderId == null).ToListAsync();
            foreach (var (area, correo) in Equipos[n])
            {
                if (!usuarios.TryGetValue(correo == Dueno ? correoDueno : correo, out var lider))
                {
                    continue;
                }

                foreach (var e in sinLider.Where(e => e.Area == area))
                {
                    e.AsignarLider(lider.Id, lider.Nombre, ahora);
                    asignados++;
                }
            }
        }

        await db.SaveChangesAsync();
        return asignados;
    }
}
