using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Empresas;
using Selvana.Infrastructure.Common.Ejemplo;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Portal;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;

namespace Selvana.Reclutamiento.Infrastructure.Ejemplo;

/// <summary>
/// Más volumen para el ambiente de pruebas: diez vacantes nuevas repartidas en las tres empresas, cada una con catorce postulantes
/// generados en todas las etapas (de recibida a contratada). Igual que la semilla base: métodos de dominio y sin IA.
/// Es idempotente por vacante (si el cargo ya existe en la empresa, no la vuelve a crear).
/// </summary>
internal static partial class SemillaEjemplo
{
    private sealed record Perfil(string[] Cargos, string[] Empresas, string[] Estudios, string[] Habilidades);

    private sealed record VacanteAmpliada(int Empresa, string? Usuaria, string Frase, ContenidoVacante Contenido, string[] Competencias, string[] Pruebas, int DiasAtras,
        bool ConContratacion, Perfil Perfil);

    private static readonly Avance[] Recorrido =
    [
        Avance.TernaOnboarding, Avance.TernaDocumentos, Avance.TernaOferta, Avance.Terna, Avance.PruebasCompletas, Avance.PruebasCompletas, Avance.PruebasEnviadas,
        Avance.PreseleccionCompleta, Avance.PreseleccionCompleta, Avance.PreseleccionInvitada, Avance.Ajustada, Avance.Recibida, Avance.Recibida, Avance.Descartada,
    ];

    public static async Task<(IReadOnlyList<(Guid EmpresaId, PostulanteContratable Postulante, string Avance)> Contratables, IReadOnlyList<PersonaPortalEjemplo> Personas)>
        SembrarAmpliadaAsync(ReclutamientoDbContext db, IAlmacenHojasDeVida almacen, ITokensPruebas tokens, IReadOnlyList<EmpresaResumen> empresas, string nombreDueno,
            TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var equipos = new[]
        {
            new Equipo("Valentina Ríos", "GerenteRrhh", nombreDueno, "Administrador"),
            new Equipo("Mateo Herrera", "Administrador", "Mateo Herrera", "Administrador"),
            new Equipo("Juliana Pardo", "Administrador", "Juliana Pardo", "Administrador"),
        };
        var contratables = new List<(Guid, PostulanteContratable, string)>();
        var personas = new List<PersonaPortalEjemplo>(PersonasBase);
        var indice = 0;
        foreach (var (v, n) in VacantesAmpliadas().Select((v, n) => (v, n)))
        {
            var candidatos = Generar(v, n).ToArray();
            indice += candidatos.Length;
            if (v.Empresa >= empresas.Count)
            {
                continue;
            }

            var empresa = empresas[v.Empresa];
            personas.AddRange(candidatos.Where(p => p.Avance is not (Avance.Recibida or Avance.Descartada or Avance.Ajustada)).Select(p => new PersonaPortalEjemplo(
                p.Nombre, p.Documento, p.Correo, p.Celular, p.Ciudad,
                p.Avance switch
                {
                    Avance.TernaOnboarding or Avance.TernaDocumentos => ["Cedula", "CertificadoBancario", "CertificadoEps", "CertificadoPension"],
                    Avance.TernaOferta => ["Cedula", "CertificadoBancario"],
                    Avance.Terna or Avance.PruebasCompletas => ["Cedula"],
                    _ => [],
                })));
            if (await db.Set<Vacante>().AnyAsync(x => x.EmpresaId == empresa.Id && x.Cargo == v.Contenido.Cargo))
            {
                continue;
            }

            var e = new VacanteEjemplo(v.Usuaria, v.Frase, v.Contenido, v.Competencias, v.Pruebas, EstadoObjetivo.Publicada, v.DiasAtras, candidatos);
            contratables.AddRange(Sembrar(db, almacen, tokens, empresa, equipos[v.Empresa], e, ahora));
        }

        await db.SaveChangesAsync();
        return (contratables, personas);
    }

    /// <summary>Personas de la semilla base que también tienen cuenta en el portal (finalistas y el empleado ya contratado).</summary>
    private static readonly PersonaPortalEjemplo[] PersonasBase =
    [
        new("Natalia Gómez Ruiz", "1023456781", "natalia.gomez@ejemplo.co", "3104567821", "Bogotá", ["Cedula", "CertificadoBancario"]),
        new("Andrés Felipe Castro", "1019876543", "andres.castro@ejemplo.co", "3157894562", "Bogotá", ["Cedula"]),
        new("Laura Camila Pérez", "1032145678", "laura.perez@ejemplo.co", "3001239876", "Bogotá", []),
        new("Óscar Javier Méndez", "1110456789", "oscar.mendez@ejemplo.co", "3164567890", "Ibagué", ["Cedula", "CertificadoBancario", "CertificadoEps"]),
        new("Yeison Andrés Bermúdez", "1110987123", "yeison.bermudez@ejemplo.co", "3176543219", "Ibagué", ["Cedula", "CertificadoBancario"]),
    ];

    /// <summary>Catorce postulantes por vacante, uno por paso del recorrido; sin contratación, los tres primeros se quedan en la terna y en pruebas.</summary>
    private static IEnumerable<Persona> Generar(VacanteAmpliada v, int numeroVacante)
    {
        var c = v.Contenido;
        for (var i = 0; i < Recorrido.Length; i++)
        {
            var semilla = numeroVacante * 100 + i;
            var nombre = NombresEjemplo.Nombre(semilla + 17);
            var avance = v.ConContratacion ? Recorrido[i] : i switch { 0 => Avance.Terna, 1 or 2 => Avance.PruebasCompletas, _ => Recorrido[i] };
            var puntaje = Math.Max(32, 92 - i * 4 - semilla % 3);
            var anios = 1 + semilla % 5;
            var p = v.Perfil;
            var experiencia = new[]
            {
                Exp(p.Cargos[semilla % p.Cargos.Length], p.Empresas[semilla % p.Empresas.Length], $"0{1 + semilla % 9}/{2025 - anios}", "actual"),
                Exp(p.Cargos[(semilla + 1) % p.Cargos.Length], p.Empresas[(semilla + 2) % p.Empresas.Length], $"0{1 + semilla % 8}/{2021 - anios}",
                    $"1{semilla % 3}/{2024 - anios}"),
            }.Take(puntaje >= 60 ? 2 : 1).ToArray();
            var estudio = p.Estudios[semilla % p.Estudios.Length];
            var razones = new List<Razon>
            {
                Mas($"{anios} {(anios == 1 ? "año" : "años")} como {experiencia[0].Cargo.ToLowerInvariant()}", "HV p.1"),
                Mas($"{estudio}", "HV p.1"),
            };
            if (puntaje >= 70)
            {
                razones.Add(Mas($"Cumple «{c.Requisitos[^1]}»", "HV p.1"));
            }

            if (puntaje < 75)
            {
                razones.Add(Menos(puntaje < 55 ? "Su experiencia no es del mismo cargo" : "Le falta uno de los requisitos deseables", "HV p.1"));
            }

            yield return new Persona(nombre, $"{1_040_000_000 + semilla * 37}", NombresEjemplo.Celular(semilla), NombresEjemplo.Correo(nombre, semilla), c.Ciudad,
                experiencia, [Est(estudio, semilla % 2 == 0 ? "SENA" : "Institución técnica regional", $"{2016 + semilla % 8}")],
                [.. p.Habilidades.Skip(semilla % 2).Take(3)], puntaje, [.. razones], avance,
                Respuestas: avance == Avance.Descartada ? [false, true] : null,
                Aspiracion: c.SalarioMinimo + Miles((c.SalarioMaximo - c.SalarioMinimo) * (semilla % 4) / 4m));
        }
    }

    private static decimal Miles(decimal valor) => Math.Round(valor / 1000m) * 1000m;

    private static VacanteAmpliada[] VacantesAmpliadas()
    {
        var bodega = new Perfil(["Auxiliar de bodega", "Auxiliar logístico", "Operario de cargue", "Alistador"],
            ["Almacenes Éxito", "Coordinadora", "Servientrega", "Tiendas Ara", "Postobón"], ["Técnico en logística", "Bachiller técnico", "Tecnólogo en gestión logística"],
            ["Inventarios", "Cargue y descargue", "Montacargas", "WMS", "Alistamiento"]);
        var comercio = new Perfil(["Cajero", "Asesor comercial", "Mercaderista", "Impulsador"],
            ["Supermercados Olímpica", "Falabella", "Tiendas D1", "Jumbo", "Alkosto"], ["Bachiller académico", "Técnico en ventas", "Técnico en mercadeo"],
            ["Caja", "Servicio al cliente", "Ventas", "Exhibición", "Datáfono"]);
        var salud = new Perfil(["Auxiliar de enfermería", "Enfermera", "Auxiliar de farmacia", "Camillero"],
            ["Clínica Imbanaco", "Hospital Universitario del Valle", "Cruz Verde", "Clínica Farallones", "Comfandi"],
            ["Técnico auxiliar de enfermería", "Enfermería profesional", "Técnico en servicios farmacéuticos"],
            ["Atención al paciente", "Signos vitales", "Dispensación", "Bioseguridad", "Historia clínica"]);
        var industria = new Perfil(["Operario de producción", "Operario de empaque", "Auxiliar de calidad", "Operario de máquina"],
            ["Arroz Diana", "Colanta", "Alpina", "Nutresa", "Productos Ramo"], ["Bachiller técnico", "Técnico en procesos industriales", "Técnico en alimentos"],
            ["Buenas prácticas de manufactura", "Empaque", "Control de calidad", "Seguridad industrial", "Trabajo en línea"]);
        var oficina = new Perfil(["Asesor de servicio al cliente", "Auxiliar administrativo", "Agente de contact center", "Recepcionista"],
            ["Teleperformance", "Konecta", "Atento", "Bancolombia", "Claro"], ["Técnico en asistencia administrativa", "Tecnólogo en gestión empresarial", "Bachiller académico"],
            ["Servicio al cliente", "Excel", "CRM", "Redacción", "Atención telefónica"]);

        static ContenidoVacante Contenido(string titulo, string cargo, string ciudad, int cupos, string jornada, string descripcion, decimal min, decimal max,
            string[] requisitos, string[] preguntas) => new(titulo, cargo, ciudad, cupos, jornada, descripcion, min, max, requisitos, preguntas);

        return
        [
            new(0, "Retail Express Colombia", "Cuatro cajeros para supermercados de Bogotá, turnos rotativos",
                Contenido("Cajero de supermercado", "Cajero", "Bogotá", 4, "Tiempo completo, turnos rotativos",
                    "Registrarás las compras, manejarás efectivo y datáfono, harás arqueos y atenderás a los clientes con amabilidad.", 1_750_905m, 1_900_000m,
                    ["Bachiller graduado", "6 meses de experiencia en caja", "Manejo de efectivo y datáfono"],
                    ["¿Tienes disponibilidad para turnos rotativos?", "¿Has manejado caja?"]),
                ["Orientación al cliente", "Responsabilidad", "Precisión"], ["ATENCION", "NUMERICO", "PERSONALIDAD"], 14, true, comercio),
            new(0, "Agroindustrial del Tolima", "Seis operarios de producción para la planta de Ibagué, turno noche",
                Contenido("Operario de producción", "Operario de producción", "Ibagué", 6, "Tiempo completo, turno noche",
                    "Operarás la línea de empaque, cumplirás las buenas prácticas de manufactura y registrarás los controles de calidad.", 1_750_905m, 2_000_000m,
                    ["Bachiller graduado", "6 meses de experiencia en planta", "Curso de manipulación de alimentos"],
                    ["¿Tienes disponibilidad para el turno de noche?", "¿Tienes el curso de manipulación de alimentos?"]),
                ["Seguridad en el trabajo", "Trabajo en equipo", "Orden"], ["ATENCION", "PERSONALIDAD"], 21, true, industria),
            new(0, null, "Cinco asesores de servicio al cliente para la sede de Bogotá",
                Contenido("Asesor de servicio al cliente", "Asesor de servicio al cliente", "Bogotá", 5, "Tiempo completo, lunes a sábado",
                    "Atenderás llamadas y chats de clientes, resolverás solicitudes en el CRM y harás seguimiento hasta cerrar cada caso.", 1_800_000m, 2_200_000m,
                    ["Bachiller graduado", "6 meses en servicio al cliente", "Manejo básico de computador"],
                    ["¿Tienes disponibilidad de lunes a sábado?", "¿Has trabajado en contact center?"]),
                ["Orientación al cliente", "Comunicación", "Tolerancia a la frustración"], ["ATENCION", "SJT-SERVICIO", "PERSONALIDAD"], 9, true, oficina),
            new(0, "Retail Express Colombia", "Tres mercaderistas para Medellín con moto propia",
                Contenido("Mercaderista", "Mercaderista", "Medellín", 3, "Tiempo completo, lunes a sábado",
                    "Visitarás puntos de venta, surtirás y organizarás la exhibición, harás conteos y reportarás agotados.", 1_800_000m, 2_100_000m,
                    ["Bachiller graduado", "Moto propia con papeles al día", "1 año como mercaderista"],
                    ["¿Tienes moto propia?", "¿Has trabajado como mercaderista?"]),
                ["Orden", "Orientación a resultados", "Autonomía"], ["ATENCION", "PERSONALIDAD"], 5, false, comercio),
            new(1, null, "Cinco auxiliares de bodega para el centro de distribución de Barranquilla",
                Contenido("Auxiliar de bodega", "Auxiliar de bodega", "Barranquilla", 5, "Tiempo completo, lunes a sábado",
                    "Recibirás, almacenarás y despacharás mercancía, harás conteos cíclicos y mantendrás la bodega segura y ordenada.", 1_750_905m, 1_950_000m,
                    ["Bachiller graduado", "1 año en bodega", "Manejo de inventarios"],
                    ["¿Puedes levantar cargas de hasta 25 kg?", "¿Has manejado inventarios?"]),
                ["Orden", "Seguridad en el trabajo", "Responsabilidad"], ["ATENCION", "NUMERICO", "PERSONALIDAD"], 16, true, bodega),
            new(1, null, "Dos montacarguistas certificados para Cartagena",
                Contenido("Montacarguista", "Montacarguista", "Cartagena", 2, "Tiempo completo, turnos rotativos",
                    "Operarás montacargas en patio y bodega, cargarás y descargarás contenedores y harás la inspección preoperacional.", 2_000_000m, 2_400_000m,
                    ["Bachiller graduado", "Certificado de operación de montacargas", "1 año operando montacargas"],
                    ["¿Tienes el certificado de montacargas vigente?", "¿Tienes disponibilidad para turnos rotativos?"]),
                ["Seguridad en el trabajo", "Atención al detalle", "Responsabilidad"], ["ATENCION", "PERSONALIDAD"], 11, true, bodega),
            new(1, null, "Un coordinador de despachos para Barranquilla con experiencia en flota",
                Contenido("Coordinador de despachos", "Coordinador de despachos", "Barranquilla", 1, "Tiempo completo, lunes a viernes",
                    "Programarás las rutas de la flota, coordinarás el cargue y harás seguimiento a las entregas y las novedades.", 3_200_000m, 3_800_000m,
                    ["Tecnólogo en logística", "2 años coordinando despachos", "Manejo de Excel intermedio"],
                    ["¿Has coordinado una flota de vehículos?", "¿Manejas Excel intermedio?"]),
                ["Liderazgo", "Planeación", "Comunicación"], ["NUMERICO", "PERSONALIDAD"], 7, false, bodega),
            new(2, null, "Tres auxiliares de farmacia para la Clínica del Valle en Cali",
                Contenido("Auxiliar de farmacia", "Auxiliar de farmacia", "Cali", 3, "Tiempo completo, turnos rotativos",
                    "Dispensarás medicamentos, controlarás inventarios y fechas de vencimiento y apoyarás al químico farmacéutico.", 1_850_000m, 2_100_000m,
                    ["Técnico en servicios farmacéuticos", "Registro ante la secretaría de salud", "6 meses en farmacia hospitalaria"],
                    ["¿Tienes el título de técnico en servicios farmacéuticos?", "¿Has trabajado en farmacia hospitalaria?"]),
                ["Atención al detalle", "Responsabilidad", "Orientación al paciente"], ["ATENCION", "PERSONALIDAD"], 12, true, salud),
            new(2, null, "Dos enfermeras jefe para urgencias en Cali",
                Contenido("Enfermera jefe de urgencias", "Enfermera jefe", "Cali", 2, "Tiempo completo, turnos de 12 horas",
                    "Liderarás el equipo de enfermería de urgencias, harás el triage y velarás por la seguridad del paciente.", 4_200_000m, 5_000_000m,
                    ["Enfermería profesional", "Registro en ReTHUS", "2 años en urgencias", "Curso de soporte vital avanzado vigente"],
                    ["¿Tienes registro en ReTHUS?", "¿Tienes el curso de soporte vital avanzado vigente?"]),
                ["Liderazgo", "Trabajo bajo presión", "Orientación al paciente"], ["SJT-SERVICIO", "PERSONALIDAD"], 19, true, salud),
            new(2, null, "Cuatro camilleros para la sede de Palmira",
                Contenido("Camillero", "Camillero", "Palmira", 4, "Tiempo completo, turnos rotativos",
                    "Trasladarás pacientes entre servicios con seguridad, apoyarás la movilización y mantendrás las camillas listas.", 1_750_905m, 1_900_000m,
                    ["Bachiller graduado", "Curso de primeros auxilios", "6 meses en un servicio de salud"],
                    ["¿Tienes el curso de primeros auxilios?", "¿Tienes disponibilidad para turnos rotativos?"]),
                ["Orientación al paciente", "Trabajo en equipo", "Responsabilidad"], ["ATENCION", "PERSONALIDAD"], 4, false, salud),
        ];
    }

    /// <summary>
    /// Cuentas del portal para las personas de ejemplo (idempotente por correo y por cédula). Su perfil copia la hoja de vida de su postulación
    /// y «Mis documentos» lleva los indicados, con vencimientos variados para que se vean vigentes, por vencer y vencidos.
    /// </summary>
    public static async Task<int> SembrarCuentasPortalAsync(ReclutamientoDbContext db, IAlmacenHojasDeVida almacen, IHasherClavesPortal hasher,
        IReadOnlyList<PersonaPortalEjemplo> personas, string contrasena, TimeProvider reloj)
    {
        if (string.IsNullOrWhiteSpace(contrasena))
        {
            return 0;
        }

        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var hash = hasher.Calcular(contrasena);
        var creadas = 0;
        foreach (var (p, i) in personas.DistinctBy(x => x.Correo).Select((p, i) => (p, i)))
        {
            var correo = CuentaCandidato.NormalizarCorreo(p.Correo);
            if (await db.Set<CuentaCandidato>().AnyAsync(c => c.Correo == correo || (c.TipoDocumento == "CC" && c.Documento == p.Documento)))
            {
                continue;
            }

            var creada = ahora.AddDays(-(3 + i % 20));
            const string ip = "181.50.0.10";
            const string navegador = "Mozilla/5.0 (datos de ejemplo)";
            var autorizacion = new AutorizacionDatos
            {
                Version = PoliticaDatosPortal.Version, Texto = PoliticaDatosPortal.Texto, AceptadaEn = creada, Ip = ip, Navegador = navegador,
                Huella = AutorizacionDatos.CalcularHuella(PoliticaDatosPortal.Version, PoliticaDatosPortal.Texto, creada, ip, navegador, p.Documento),
            };
            var cuenta = Ok(CuentaCandidato.Registrar(correo, hash, p.Nombre, "CC", p.Documento, autorizacion, creada)).Value;
            Ok(cuenta.Ingresar(contrasenaValida: true, creada));

            // Su perfil es la hoja de vida de su postulación, si la hay
            var candidato = await db.Set<Candidato>().AsNoTracking().FirstOrDefaultAsync(c => c.Documento == p.Documento);
            cuenta.ActualizarDatos(candidato?.Datos ?? new DatosCandidato { Nombre = p.Nombre, Celular = p.Celular, Ciudad = p.Ciudad });

            foreach (var (tipo, n) in p.Documentos.Select((t, n) => (t, n)))
            {
                var nombre = TiposDocumentoPersona.Nombre(tipo) ?? tipo;
                var archivo = almacen.Guardar(Guid.Empty, $"{nombre}.pdf", "application/pdf",
                    PdfTexto.Crear([nombre, $"{p.Nombre} · CC {p.Documento}", "", "Documento ficticio de ejemplo."]), creada);
                // Vencimientos variados: vigente, por vencer y alguno vencido
                DateOnly? vence = TiposDocumentoPersona.Todos.First(t => t.Clave == tipo).Vence ? hoy.AddDays((i + n) % 5 == 0 ? -10 : (i + n) % 3 == 0 ? 20 : 120) : null;
                Ok(cuenta.GuardarDocumento(tipo, archivo, $"{nombre}.pdf", "application/pdf", vence, creada.AddHours(1)));
            }

            db.Add(cuenta);
            creadas++;
        }

        await db.SaveChangesAsync();
        return creadas;
    }
}
