using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Contracts.Empresas;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Expediente.Infrastructure.Persistence;
using Selvana.Infrastructure.Common.Ejemplo;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Infrastructure.Ejemplo;

/// <summary>
/// Planta de ejemplo de cada empresa para el ambiente de pruebas, como si se hubiera cargado del archivo de la nómina: empleados con
/// novedades, carpeta con vencimientos (vigentes, por vencer y vencidos), afiliaciones incompletas, retiros, campaña de actualización,
/// cambios propuestos desde el portal y una lectura de la IA por decidir. Sin IA: la lectura se fija a mano con el modelo «ejemplo (sin IA)».
/// Idempotente: si la empresa ya tiene empleados del archivo de planta, no la toca.
/// </summary>
internal static class SemillaEjemplo
{
    private const string Origen = "Archivo de planta";
    private const string Modelo = "ejemplo (sin IA)";

    private sealed record Puesto(string Cargo, string Area, string CentroCosto, decimal Salario, string? Curso);

    private sealed record Planta(int Cantidad, string[] Sedes, string[] Ciudades, Puesto[] Puestos, string[] Usuarias);

    private static readonly string[] Eps = ["Sura", "Sanitas", "Nueva EPS", "Compensar", "Salud Total", "Famisanar"];
    private static readonly string[] Pension = ["Porvenir", "Protección", "Colfondos", "Colpensiones", "Skandia"];
    private static readonly string[] Cajas = ["Compensar", "Cafam", "Comfama", "Comfandi", "Combarranquilla", "Comfenalco"];

    public static async Task<IReadOnlyList<PersonaPortalEjemplo>> SembrarAsync(ExpedienteDbContext db, IAlmacenArchivos almacen, IReadOnlyList<EmpresaResumen> empresas,
        string porRrhh, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var plantas = Plantas();
        var personas = new List<PersonaPortalEjemplo>();
        var g = 0;
        for (var n = 0; n < empresas.Count && n < plantas.Length; n++)
        {
            var empresa = empresas[n];
            var planta = plantas[n];
            var yaSembrada = await db.Set<Empleado>().AnyAsync(e => e.EmpresaId == empresa.Id && e.Origen == Origen);
            for (var i = 0; i < planta.Cantidad; i++, g++)
            {
                var nombre = NombresEjemplo.Nombre(g + 500);
                var documento = $"{1_050_000_000 + g * 41}";
                var correo = NombresEjemplo.Correo(nombre, g + 5000);
                var celular = NombresEjemplo.Celular(g + 500);
                var ciudad = planta.Ciudades[g % planta.Ciudades.Length];
                var retirado = g % 13 == 5;
                if (g % 3 == 0 && !retirado)
                {
                    personas.Add(new PersonaPortalEjemplo(nombre, documento, correo, celular, ciudad, ["Cedula"]));
                }

                if (!yaSembrada)
                {
                    db.Add(Empleado(db, almacen, empresa, planta, g, nombre, documento, correo, celular, ciudad, retirado, porRrhh, ahora, hoy));
                }
            }
        }

        await db.SaveChangesAsync();
        return personas;
    }

    private static Empleado Empleado(ExpedienteDbContext db, IAlmacenArchivos almacen, EmpresaResumen empresa, Planta planta, int g, string nombre, string documento,
        string correo, string celular, string ciudad, bool retirado, string porRrhh, DateTimeOffset ahora, DateOnly hoy)
    {
        var puesto = planta.Puestos[g % planta.Puestos.Length];
        var ingreso = hoy.AddDays(-(45 + g * 53 % 1500));
        var usuariaNombre = planta.Usuarias.Length > 0 && g % 4 != 0 ? planta.Usuarias[g % planta.Usuarias.Length] : null;
        var usuaria = usuariaNombre is null ? null : empresa.EmpresasUsuarias.FirstOrDefault(u => u.Nombre == usuariaNombre);
        var (contrato, fin) = usuaria is not null ? ("Obra o labor", (DateOnly?)hoy.AddDays(g % 5 == 0 ? 25 : 150 + g % 90))
            : g % 3 == 1 ? ("Término fijo", hoy.AddDays(g % 6 == 1 ? 30 : 200 + g % 120))
            : ("Término indefinido", (DateOnly?)null);
        var salario = Miles(puesto.Salario * (1 + g % 7 / 100m));
        var completas = g % 9 != 4;
        var afiliaciones = new Afiliaciones
        {
            Eps = Eps[g % Eps.Length], FondoPension = Pension[g % Pension.Length], Arl = completas ? "Positiva" : "", ClaseRiesgoArl = completas ? 1 + g % 3 : null,
            CajaCompensacion = completas ? Cajas[g % Cajas.Length] : "",
        };
        var creado = ahora.AddDays(-30);
        var e = Domain.Empleados.Empleado.Crear(empresa.Id, Origen, documento, nombre, correo, celular, ciudad,
            new SituacionLaboral(puesto.Cargo, puesto.Area, puesto.CentroCosto, planta.Sedes[g % planta.Sedes.Length], usuaria?.Id, usuaria?.Nombre, contrato,
                Math.Max(1_750_905m, salario), fin), ingreso, afiliaciones, porRrhh, creado);

        // Carpeta: cédula y contrato sin vencimiento, y el curso del cargo con vencimientos variados
        Documento(e, almacen, empresa.Id, "Cédula", "Cédula de ciudadanía", null, nombre, documento, porRrhh, creado);
        Documento(e, almacen, empresa.Id, "Contrato", $"Contrato {contrato.ToLowerInvariant()}", null, nombre, documento, porRrhh, creado);
        if (puesto.Curso is { } curso)
        {
            var vence = hoy.AddDays(g % 6 == 0 ? -12 : g % 6 == 1 ? 18 : 220 + g % 200);
            Documento(e, almacen, empresa.Id, "Curso o certificación", curso, vence, nombre, documento, porRrhh, creado);
        }

        // Novedades: aumentos y traslados con antes y después
        if (g % 4 == 0)
        {
            var s = e.Situacion;
            Ok(e.RegistrarNovedad(TipoNovedad.CambioSalario, s with { SalarioCop = Miles(s.SalarioCop * 1.06m) }, hoy.AddDays(-60),
                "Aumento anual", porRrhh, creado.AddDays(1)));
        }

        if (g % 7 == 0 && planta.Sedes.Length > 1)
        {
            var s = e.Situacion;
            Ok(e.RegistrarNovedad(TipoNovedad.Traslado, s with { Sede = planta.Sedes[(g + 1) % planta.Sedes.Length] }, hoy.AddDays(-20),
                "Traslado por apertura de sede", porRrhh, creado.AddDays(2)));
        }

        if (retirado)
        {
            Ok(e.Retirar(hoy.AddDays(-15), g % 2 == 0 ? "Renuncia voluntaria" : "Terminación del contrato", porRrhh, creado.AddDays(3)));
            return e;
        }

        // Campaña de actualización de datos pendiente y cambios que el empleado propuso desde su portal
        if (g % 5 == 2)
        {
            e.PedirActualizacion(creado.AddDays(5));
        }

        if (g % 11 == 3)
        {
            var propuesta = Ok(e.ProponerDatos(NombresEjemplo.Celular(g + 900), ciudad, Eps[(g + 1) % Eps.Length], afiliaciones.FondoPension, afiliaciones.CajaCompensacion,
                ahora.AddDays(-2)));
            db.AddIntegrationEvent(new PropuestaPublicada(Guid.CreateVersion7(), ahora.AddDays(-2), e.EmpresaId, e.EmpresaUsuariaId, "Expediente", "DatosEmpleado", e.Id,
                $"Actualizar datos · {e.Nombre}", $"{e.Nombre} propuso desde su portal {propuesta.Value.Cambios.Count} cambios en sus datos.", null, null,
                [new RazonPropuesta("+", "Lo escribió el propio empleado en su portal", "Portal del empleado")],
                [.. propuesta.Value.Cambios.Select(x => new CampoPropuesta("Cambio", x))], null, $"/consola/empleados/{e.Id}", false, null));
        }

        // Un certificado de EPS leído por la IA que propone cambiar la entidad: espera la decisión de RRHH
        if (g % 17 == 4)
        {
            var d = Documento(e, almacen, empresa.Id, "Afiliación", "Certificado de EPS", null, nombre, documento, nombre, ahora.AddDays(-1));
            var archivo = d.Vigente!.ArchivoId;
            Ok(e.PedirLectura(d.Id, ahora.AddDays(-1)));
            var nueva = Eps[(g + 2) % Eps.Length];
            e.RegistrarLectura(d.Id, archivo, "Certificado de afiliación EPS", true, true,
                [new CampoLeido { Campo = "número de documento", Valor = documento }, new CampoLeido { Campo = "entidad", Valor = nueva }], [],
                [new RazonLectura { Signo = "+", Texto = "El número de documento coincide con el del empleado", Fuente = "Certificado p.1" },
                 new RazonLectura { Signo = "+", Texto = $"La entidad del certificado es {nueva}", Fuente = "Certificado p.1" }],
                Modelo, 0m, ahora.AddDays(-1));
            var lectura = d.Vigente.Lectura!;
            db.AddIntegrationEvent(new PropuestaPublicada(Guid.CreateVersion7(), ahora.AddDays(-1), e.EmpresaId, e.EmpresaUsuariaId, "Expediente", "ActualizacionExpediente",
                d.Id, $"Actualizar el expediente · {e.Nombre}", $"La IA leyó «{d.Nombre}» y propone {lectura.Cambios.Count} cambio. Revíselo y aplíquelo.",
                "Lector de documentos", Modelo, [.. lectura.Razones.Select(x => new RazonPropuesta(x.Signo, x.Texto, x.Fuente))],
                [.. lectura.Cambios.Select(x => new CampoPropuesta(x.Etiqueta, $"{x.Actual} → {x.Propuesto}"))], null, $"/consola/empleados/{e.Id}?pestana=carpeta", true, null));
        }

        return e;
    }

    private static DocumentoEmpleado Documento(Empleado e, IAlmacenArchivos almacen, Guid empresaId, string tipo, string nombre, DateOnly? vence, string persona,
        string documento, string por, DateTimeOffset cuando)
    {
        var archivo = almacen.Guardar(empresaId, $"{nombre}.pdf", "application/pdf",
            PdfTexto.Crear([nombre, $"{persona} · CC {documento}", vence is { } v ? $"Vigente hasta {v:dd/MM/yyyy}" : "", "", "Documento ficticio de ejemplo."]), cuando);
        return Ok(e.GuardarDocumento(null, tipo, nombre, archivo, $"{nombre}.pdf", vence, null, por, cuando)).Value;
    }

    private static decimal Miles(decimal valor) => Math.Round(valor / 1000m) * 1000m;

    private static T Ok<T>(T resultado) where T : Result =>
        resultado.IsFailure ? throw new InvalidOperationException($"Semilla de ejemplo: {resultado.Error.Code} · {resultado.Error.Description}") : resultado;

    private static Planta[] Plantas() =>
    [
        new(36, ["Bogotá · Calle 80", "Ibagué · Parque industrial", "Medellín · Itagüí"], ["Bogotá", "Ibagué", "Medellín", "Soacha"],
        [
            new("Asesor comercial", "Comercial", "CC-101 Ventas", 1_900_000m, null),
            new("Cajero", "Tiendas", "CC-102 Tiendas", 1_800_000m, null),
            new("Auxiliar de bodega", "Logística", "CC-201 Bodega", 1_850_000m, "Curso de trabajo en alturas"),
            new("Operario de producción", "Producción", "CC-301 Planta", 1_900_000m, "Curso de manipulación de alimentos"),
            new("Mercaderista", "Comercial", "CC-101 Ventas", 1_950_000m, null),
            new("Analista de nómina", "Gestión humana", "CC-401 Administración", 3_400_000m, null),
        ], ["Retail Express Colombia", "Agroindustrial del Tolima"]),
        new(18, ["Barranquilla · Centro de distribución", "Cartagena · Mamonal"], ["Barranquilla", "Soledad", "Cartagena"],
        [
            new("Auxiliar de bodega", "Bodega", "CC-10 Operaciones", 1_850_000m, "Curso de trabajo en alturas"),
            new("Montacarguista", "Bodega", "CC-10 Operaciones", 2_300_000m, "Certificado de operación de montacargas"),
            new("Conductor de camión C2", "Transporte", "CC-20 Flota", 2_800_000m, "Curso de manejo defensivo"),
            new("Coordinador de despachos", "Transporte", "CC-20 Flota", 3_600_000m, null),
        ], []),
        new(18, ["Cali · Sede principal", "Palmira · Sede sur"], ["Cali", "Palmira", "Jamundí"],
        [
            new("Auxiliar de enfermería", "Hospitalización", "CC-01 Asistencial", 2_100_000m, "Curso de soporte vital básico"),
            new("Enfermera jefe", "Urgencias", "CC-01 Asistencial", 4_600_000m, "Curso de soporte vital avanzado"),
            new("Auxiliar de farmacia", "Farmacia", "CC-02 Apoyo", 1_950_000m, null),
            new("Camillero", "Urgencias", "CC-02 Apoyo", 1_800_000m, "Curso de primeros auxilios"),
            new("Recepcionista", "Admisiones", "CC-03 Administración", 1_850_000m, null),
        ], []),
    ];
}
