using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Contracts.Empresas;
using Selvana.Infrastructure.Common.Ejemplo;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Infrastructure.Ejemplo;

/// <summary>
/// Datos de ejemplo del flujo de selección para el ambiente de pruebas en línea. Usa los métodos de dominio y fija a mano lo que
/// normalmente produce la IA (borrador de la vacante, lectura de la HV, ranking, preselección): no encola nada hacia el AI.Worker,
/// así que no gasta IA. Lo que va a la bandeja viaja por el outbox a la propia API.
/// </summary>
internal static class SemillaEjemplo
{
    private const string Modelo = "ejemplo (sin IA)";
    private static readonly CultureInfo Pesos = new("es-CO");

    private enum Avance
    {
        Recibida,
        Ajustada,
        Descartada,
        PreseleccionInvitada,
        PreseleccionCompleta,
        PruebasEnviadas,
        PruebasCompletas,
        Terna,
        TernaOferta,
        TernaDocumentos,
        TernaOnboarding,
    }

    private enum EstadoObjetivo
    {
        Publicada,
        EnAprobacion,
        Borrador,
    }

    private sealed record Razon(string Signo, string Texto, string Evidencia);

    private sealed record Persona(
        string Nombre, string Documento, string Celular, string Correo, string Ciudad, ExperienciaLaboral[] Experiencia, EstudioRealizado[] Formacion,
        string[] Habilidades, int Puntaje, Razon[] Razones, Avance Avance, bool[]? Respuestas = null, decimal? Aspiracion = null);

    private sealed record VacanteEjemplo(
        string? Usuaria, string Frase, ContenidoVacante Contenido, string[] Competencias, string[] Pruebas, EstadoObjetivo Estado, int DiasAtras,
        Persona[] Candidatos);

    private sealed record Equipo(string Solicita, string RolSolicita, string Aprueba, string RolAprueba);

    public static async Task<IReadOnlyList<(Guid EmpresaId, PostulanteContratable Postulante, string Avance)>> SembrarAsync(
        ReclutamientoDbContext db, IAlmacenHojasDeVida almacen, ITokensPruebas tokens, IReadOnlyList<EmpresaResumen> empresas, string nombreDueno, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var contratables = new List<(Guid, PostulanteContratable, string)>();
        var equipos = new[]
        {
            new Equipo("Valentina Ríos", "GerenteRrhh", nombreDueno, "Administrador"),
            new Equipo("Mateo Herrera", "Administrador", "Mateo Herrera", "Administrador"),
            new Equipo("Juliana Pardo", "Administrador", "Juliana Pardo", "Administrador"),
        };
        var datos = new[] { TalentoAndino(), LogisticaCaribe(), ClinicaDelValle() };

        for (var i = 0; i < empresas.Count && i < datos.Length; i++)
        {
            var empresa = empresas[i];
            if (await db.Set<Vacante>().AnyAsync(v => v.EmpresaId == empresa.Id))
            {
                continue;
            }

            foreach (var v in datos[i])
            {
                contratables.AddRange(Sembrar(db, almacen, tokens, empresa, equipos[i], v, ahora));
            }
        }

        await db.SaveChangesAsync();
        return contratables;
    }

    private static List<(Guid, PostulanteContratable, string)> Sembrar(
        ReclutamientoDbContext db, IAlmacenHojasDeVida almacen, ITokensPruebas tokens, EmpresaResumen empresa, Equipo equipo, VacanteEjemplo e, DateTimeOffset ahora)
    {
        var creada = ahora.AddDays(-e.DiasAtras);
        var usuaria = e.Usuaria is null ? null : empresa.EmpresasUsuarias.First(u => u.Nombre == e.Usuaria);
        var vacante = Vacante.Solicitar(empresa.Id, empresa.Nombre, usuaria?.Id, usuaria?.Nombre, e.Frase, equipo.Solicita, creada).Value;
        Ok(vacante.ConfigurarPruebas(e.Pruebas.Select(c => new PruebaEnBateria { Codigo = c, Peso = c == "PERSONALIDAD" ? 1 : 3, Corte = c == "PERSONALIDAD" ? null : 60 })));
        vacante.RegistrarBorradorIa(e.Contenido, "Rango alineado con el salario mínimo 2026 y el mercado de la ciudad para el cargo.", e.Competencias,
            ["Contratación directa por la empresa indicada"],
            [new RazonVacante { Signo = "+", Texto = $"Cargo y ciudad tomados de la frase: {e.Contenido.Cargo}, {e.Contenido.Ciudad}", Fuente = "Frase" },
             new RazonVacante { Signo = "+", Texto = "Requisitos usuales del cargo en el sector", Fuente = "Perfil del cargo" },
             new RazonVacante { Signo = "+", Texto = "Salario dentro del rango de mercado", Fuente = "Referencia salarial" }],
            Modelo);
        db.Add(vacante);

        if (e.Estado == EstadoObjetivo.Borrador)
        {
            return [];
        }

        Ok(vacante.SolicitarAprobacion(equipo.Solicita, equipo.RolSolicita, creada.AddHours(1)));
        if (e.Estado == EstadoObjetivo.EnAprobacion)
        {
            AvisarRequisicion(db, vacante, creada.AddHours(1));
            return [];
        }

        if (vacante.Estado != EstadoVacante.Publicada)
        {
            Ok(vacante.Aprobar(equipo.Aprueba, equipo.RolAprueba, creada.AddHours(3)));
        }

        Ok(vacante.ConfigurarFiltros(e.Contenido.Requisitos.Take(2), e.Contenido.PreguntasFiltro.Take(1), [e.Contenido.Ciudad], 6, equipo.Solicita, creada.AddHours(4)));

        var contratables = new List<(Guid, PostulanteContratable, string)>();
        var orden = 0;
        foreach (var p in e.Candidatos)
        {
            var llego = creada.AddDays(1).AddHours(orden++ * 7);
            contratables.AddRange(Postular(db, almacen, tokens, empresa, vacante, equipo, p, llego, ahora));
        }

        return contratables;
    }

    private static List<(Guid, PostulanteContratable, string)> Postular(
        ReclutamientoDbContext db, IAlmacenHojasDeVida almacen, ITokensPruebas tokens, EmpresaResumen empresa, Vacante vacante, Equipo equipo, Persona p,
        DateTimeOffset llego, DateTimeOffset ahora)
    {
        var datos = new DatosCandidato
        {
            Nombre = p.Nombre, Documento = p.Documento, Celular = p.Celular, Correo = p.Correo, Ciudad = p.Ciudad,
            Experiencia = [.. p.Experiencia], Formacion = [.. p.Formacion], Habilidades = [.. p.Habilidades], Cursos = [], LinkedIn = "",
        };
        var candidato = Candidato.Registrar(empresa.Id, datos, llego);
        db.Add(candidato);

        var archivo = almacen.Guardar(empresa.Id, $"HV {p.Nombre}.pdf", "application/pdf", HojaDeVidaPdf(p), llego);
        var texto = PoliticaDatos.Texto(vacante.EmpresaVisible);
        const string ip = "181.50.0.10";
        const string navegador = "Mozilla/5.0 (datos de ejemplo)";
        var autorizacion = new AutorizacionDatos
        {
            Version = PoliticaDatos.Version, Texto = texto, AceptadaEn = llego, Ip = ip, Navegador = navegador,
            Huella = AutorizacionDatos.CalcularHuella(PoliticaDatos.Version, texto, llego, ip, navegador, p.Documento),
        };
        var respuestas = vacante.PreguntasFiltro.Select((pregunta, i) => new RespuestaFiltro { Pregunta = pregunta, Respuesta = p.Respuestas?.ElementAtOrDefault(i) ?? true });
        var postulacion = candidato.Postularse(vacante.Id, CanalPostulacion.Portal, respuestas, autorizacion, archivo, [], llego).Value;

        var cumplidos = vacante.Requisitos.Take(p.Puntaje >= 70 ? 3 : p.Puntaje >= 55 ? 2 : 1).ToList();
        var faltantes = vacante.Requisitos.Except(cumplidos).ToList();
        Ok(candidato.RegistrarAfinidad(postulacion.Id, p.Puntaje, p.Razones.Select(r => new RazonPuntaje { Signo = r.Signo, Texto = r.Texto, Evidencia = r.Evidencia }),
            cumplidos, faltantes, Modelo, llego.AddMinutes(3)));

        var paso = llego.AddDays(1);
        switch (p.Avance)
        {
            case Avance.Ajustada:
                Ok(candidato.AjustarAfinidad(postulacion.Id, Math.Min(100, p.Puntaje + 8), "En la llamada de verificación demostró más experiencia de la que dice la HV.",
                    equipo.Solicita, paso));
                break;
            case Avance.Descartada:
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Descartada,
                    $"No cumple un excluyente: respondió «no» a «{vacante.PreguntasFiltro.FirstOrDefault()}».", equipo.Solicita, paso));
                break;
            case Avance.PreseleccionInvitada:
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Preseleccion, null, equipo.Solicita, paso));
                db.Add(ConversacionPreseleccion.Invitar(empresa.Id, vacante.EmpresaUsuariaId, vacante.Id, postulacion.Id, candidato.Id, tokens.Generar().Huella,
                    Saludo(p, vacante), equipo.Solicita, paso));
                break;
            case Avance.PreseleccionCompleta:
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Preseleccion, null, equipo.Solicita, paso));
                db.Add(Conversacion(empresa, vacante, candidato, postulacion, tokens, equipo, p, paso));
                break;
            case Avance.PruebasEnviadas:
            case Avance.PruebasCompletas:
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Preseleccion, null, equipo.Solicita, paso));
                db.Add(Conversacion(empresa, vacante, candidato, postulacion, tokens, equipo, p, paso));
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Pruebas, null, equipo.Solicita, paso.AddDays(1)));
                db.Add(Pruebas(empresa, vacante, candidato, postulacion, tokens, equipo, p, paso.AddDays(1), completas: p.Avance == Avance.PruebasCompletas));
                break;
            case Avance.Terna:
            case Avance.TernaOferta:
            case Avance.TernaDocumentos:
            case Avance.TernaOnboarding:
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Preseleccion, null, equipo.Solicita, paso));
                db.Add(Conversacion(empresa, vacante, candidato, postulacion, tokens, equipo, p, paso));
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Pruebas, null, equipo.Solicita, paso.AddDays(1)));
                db.Add(Pruebas(empresa, vacante, candidato, postulacion, tokens, equipo, p, paso.AddDays(1), completas: true));
                Ok(candidato.MoverPostulacion(postulacion.Id, EtapaPostulacion.Terna, null, equipo.Aprueba, paso.AddDays(3)));
                break;
            case Avance.Recibida:
            default:
                break;
        }

        if (p.Avance is not (Avance.TernaOferta or Avance.TernaDocumentos or Avance.TernaOnboarding))
        {
            return [];
        }

        var contratable = new PostulanteContratable(postulacion.Id, postulacion.Numero, candidato.Id, vacante.Id, vacante.Codigo, nameof(EtapaPostulacion.Terna), null,
            p.Nombre, candidato.Documento, p.Correo, p.Celular, p.Ciudad, vacante.Titulo, vacante.Cargo, vacante.Ciudad, vacante.Jornada, vacante.Descripcion,
            vacante.Requisitos, vacante.Competencias, vacante.SalarioMinimo, vacante.SalarioMaximo, p.Aspiracion, vacante.EmpresaVisible, vacante.EmpresaUsuariaId,
            vacante.EmpresaUsuariaNombre);
        var avance = p.Avance switch
        {
            Avance.TernaOferta => "Oferta",
            Avance.TernaDocumentos => "Documentos",
            _ => "Onboarding",
        };
        return [(empresa.Id, contratable, avance)];
    }

    /// <summary>Si una regla del dominio rechaza un paso, la semilla falla con el mensaje en vez de dejar datos a medias.</summary>
    private static T Ok<T>(T resultado) where T : Result =>
        resultado.IsFailure ? throw new InvalidOperationException($"Semilla de ejemplo: {resultado.Error.Code} · {resultado.Error.Description}") : resultado;

    private static string Saludo(Persona p, Vacante v) =>
        $"Hola, {p.Nombre.Split(' ')[0]}. Soy el asistente de selección de {v.EmpresaVisible}. Vi tu postulación a «{v.Titulo}» y quiero hacerte unas preguntas cortas. ¿Tienes cinco minutos?";

    private static ConversacionPreseleccion Conversacion(
        EmpresaResumen empresa, Vacante vacante, Candidato candidato, Postulacion postulacion, ITokensPruebas tokens, Equipo equipo, Persona p, DateTimeOffset cuando)
    {
        var aspiracion = p.Aspiracion ?? vacante.SalarioMinimo;
        var c = ConversacionPreseleccion.Invitar(empresa.Id, vacante.EmpresaUsuariaId, vacante.Id, postulacion.Id, candidato.Id, tokens.Generar().Huella,
            Saludo(p, vacante), equipo.Solicita, cuando);
        var resumen = new ResumenPreseleccion
        {
            Interesado = true,
            AspiracionSalarialCop = aspiracion,
            Disponibilidad = "Inmediata",
            Ubicacion = $"Vive en {p.Ciudad}",
            Respuestas = [.. vacante.PreguntasFiltro.Select(q => new RespuestaTecnica { Pregunta = q, Respuesta = "Sí" })],
            Dudas = ["Preguntó por el horario de los fines de semana"],
            Texto = $"{p.Nombre} confirma interés, disponibilidad inmediata y aspiración de {aspiracion.ToString("C0", Pesos)}. Respondió sí a las preguntas técnicas.",
        };
        Ok(c.EscribeCandidato("Hola, sí, claro. Me interesa mucho la vacante.", cuando.AddMinutes(20)));
        c.RespondeAgente("¡Qué bien! ¿Cuál es tu aspiración salarial y desde cuándo podrías empezar?", resumen, completa: false, null, Modelo, cuando.AddMinutes(21));
        Ok(c.EscribeCandidato($"Aspiro a {aspiracion.ToString("C0", Pesos)} y puedo empezar de inmediato. ¿Cómo son los horarios del fin de semana?", cuando.AddMinutes(25)));
        c.RespondeAgente("Los turnos de fin de semana se rotan y se avisan con una semana de anticipación. Gracias por tus respuestas: el equipo de selección te contactará pronto.",
            resumen, completa: true, null, Modelo, cuando.AddMinutes(26));
        return c;
    }

    private static AplicacionPruebas Pruebas(
        EmpresaResumen empresa, Vacante vacante, Candidato candidato, Postulacion postulacion, ITokensPruebas tokens, Equipo equipo, Persona p, DateTimeOffset cuando,
        bool completas)
    {
        var a = AplicacionPruebas.Enviar(empresa.Id, vacante.Id, postulacion.Id, candidato.Id, p.Nombre, vacante.Pruebas, tokens.Generar().Huella, equipo.Solicita, cuando);
        if (!completas)
        {
            return a;
        }

        var momento = cuando.AddHours(2);
        Ok(a.ResponderConsentimiento(true, momento));
        var aciertos = p.Puntaje / 100.0;
        foreach (var codigo in vacante.Pruebas.Select(x => x.Codigo))
        {
            var prueba = SuitePsicotecnica.Buscar(codigo);
            if (prueba is null)
            {
                continue;
            }

            Ok(a.Iniciar(codigo, momento));
            var respuestas = prueba.Preguntas.Select((q, i) =>
            {
                var mejor = q.Opciones.MaxBy(o => o.Puntaje)!;
                var otra = q.Opciones.FirstOrDefault(o => o.Id != mejor.Id) ?? mejor;
                // Acierta en proporción a su puntaje, repartido a lo largo de la prueba
                var acierta = (i * 37 % 100) < aciertos * 100;
                return new RespuestaPrueba { PreguntaId = q.Id, OpcionId = acierta ? mejor.Id : otra.Id, Milisegundos = 4000 + (i * 911 % 3000) };
            }).ToList();
            momento = momento.AddMinutes(Math.Max(1, prueba.Minutos - 1));
            Ok(a.Terminar(codigo, respuestas, 0, momento));
            momento = momento.AddMinutes(2);
        }

        return a;
    }

    private static void AvisarRequisicion(ReclutamientoDbContext db, Vacante v, DateTimeOffset ahora)
    {
        if (v.EtapaPendiente is not { } etapa)
        {
            return;
        }

        var salario = $"{v.SalarioMinimo.ToString("C0", Pesos)} – {v.SalarioMaximo.ToString("C0", Pesos)}";
        var anteriores = v.Aprobaciones.Select(a => new CampoPropuesta($"Aprobó {FlujoRequisicion.Nombre(a.Etapa)}", a.Automatica ? $"{a.Por} (pidió la vacante)" : a.Por));
        db.AddIntegrationEvent(new PropuestaPublicada(Guid.CreateVersion7(), ahora, v.EmpresaId, v.EmpresaUsuariaId, "Reclutamiento", "Requisicion", v.Id,
            $"Requisición por aprobar · {FlujoRequisicion.Nombre(etapa)} · {v.Titulo}",
            $"{v.Ciudad} · {v.Cupos} {(v.Cupos == 1 ? "cupo" : "cupos")} · la pidió {v.AprobacionPedidaPor}",
            null, null, [],
            [new CampoPropuesta("Cargo", v.Cargo), new CampoPropuesta("Rango salarial", salario), new CampoPropuesta("Jornada", v.Jornada), .. anteriores],
            new AccionPropuesta(etapa == FlujoRequisicion.Etapas[^1] ? "Aprobar y publicar" : "Aprobar requisición", "POST", $"/api/v1/reclutamiento/vacantes/{v.Id}/aprobar"),
            $"/consola/vacantes/{v.Id}", false, null, FlujoRequisicion.QuienesAprueban(etapa)));
    }

    /// <summary>Una hoja de vida de una página en PDF, solo con texto: lo justo para que la consola la abra.</summary>
    private static byte[] HojaDeVidaPdf(Persona p)
    {
        var lineas = new List<string> { p.Nombre, $"CC {p.Documento} · {p.Celular} · {p.Correo}", p.Ciudad, "", "EXPERIENCIA" };
        lineas.AddRange(p.Experiencia.Select(x => $"{x.Cargo} — {x.Empresa} ({x.Desde} a {x.Hasta})"));
        lineas.Add("");
        lineas.Add("FORMACIÓN");
        lineas.AddRange(p.Formacion.Select(x => $"{x.Titulo} — {x.Institucion} ({x.Anio})"));
        lineas.Add("");
        lineas.Add("HABILIDADES");
        lineas.Add(string.Join(", ", p.Habilidades));
        lineas.Add("");
        lineas.Add("Hoja de vida ficticia generada para el ambiente de pruebas de Selvana.");

        return PdfTexto.Crear(lineas);
    }

    private static ExperienciaLaboral Exp(string cargo, string empresa, string desde, string hasta) => new() { Cargo = cargo, Empresa = empresa, Desde = desde, Hasta = hasta };

    private static EstudioRealizado Est(string titulo, string institucion, string anio) => new() { Titulo = titulo, Institucion = institucion, Anio = anio };

    private static Razon Mas(string texto, string evidencia) => new("+", texto, evidencia);

    private static Razon Menos(string texto, string evidencia) => new("−", texto, evidencia);

    private static VacanteEjemplo[] TalentoAndino() =>
    [
        new("Retail Express Colombia", "Necesito 3 asesores comerciales para tiendas de Bogotá, con experiencia en ventas y fines de semana",
            new ContenidoVacante("Asesor comercial de tienda", "Asesor comercial", "Bogotá", 3, "Tiempo completo, turnos rotativos",
                "Atenderás a los clientes en tienda, asesorarás la compra, manejarás caja y mantendrás la exhibición. Trabajarás en un equipo joven con metas mensuales y comisiones.",
                1_750_905m, 2_100_000m,
                ["Bachiller graduado", "6 meses de experiencia en ventas o servicio al cliente", "Manejo básico de caja y datáfono"],
                ["¿Tienes disponibilidad para trabajar fines de semana?", "¿Tienes experiencia en ventas?"]),
            ["Orientación al cliente", "Trabajo en equipo", "Orientación a metas"], ["ATENCION", "SJT-SERVICIO", "PERSONALIDAD"], EstadoObjetivo.Publicada, 18,
            [
                new("Natalia Gómez Ruiz", "1023456781", "3104567821", "natalia.gomez@ejemplo.co", "Bogotá",
                    [Exp("Asesora comercial", "Almacenes Moda Total", "03/2022", "actual"), Exp("Cajera", "Supermercado El Ahorro", "01/2020", "02/2022")],
                    [Est("Técnico en ventas", "SENA", "2021"), Est("Bachiller académico", "Colegio Distrital Kennedy", "2019")], ["Ventas", "Caja", "Servicio al cliente"],
                    88, [Mas("3 años en ventas de moda en tienda", "HV p.1"), Mas("Maneja caja y datáfono", "HV p.1"), Mas("Técnico en ventas del SENA", "HV p.1"),
                        Mas("Disponible fines de semana", "Pregunta de filtro")], Avance.TernaOferta, Aspiracion: 2_000_000m),
                new("Andrés Felipe Castro", "1019876543", "3157894562", "andres.castro@ejemplo.co", "Bogotá",
                    [Exp("Vendedor", "Tecnología Hogar S.A.S.", "06/2021", "actual")],
                    [Est("Bachiller académico", "Colegio San José", "2020")], ["Ventas", "Tecnología", "Negociación"],
                    81, [Mas("Más de 3 años vendiendo tecnología", "HV p.1"), Mas("Supera metas de ventas", "HV p.1"), Menos("No menciona manejo de caja", "HV p.1")],
                    Avance.Terna, Aspiracion: 1_950_000m),
                new("Laura Camila Pérez", "1032145678", "3001239876", "laura.perez@ejemplo.co", "Bogotá",
                    [Exp("Auxiliar de servicio al cliente", "Call Center Andino", "02/2023", "actual")],
                    [Est("Tecnóloga en gestión comercial", "Uniminuto", "2022")], ["Servicio al cliente", "CRM", "Excel"],
                    76, [Mas("Tecnóloga en gestión comercial", "HV p.1"), Mas("2 años de servicio al cliente", "HV p.1"), Menos("Su experiencia es telefónica, no en tienda", "HV p.1")],
                    Avance.PruebasCompletas),
                new("Jhon Sebastián Rojas", "1014567890", "3112345670", "jhon.rojas@ejemplo.co", "Soacha",
                    [Exp("Mercaderista", "Distribuidora La 14", "08/2022", "actual")],
                    [Est("Bachiller técnico", "IED Soacha", "2021")], ["Exhibición", "Inventarios"],
                    72, [Mas("Conoce exhibición y reposición", "HV p.1"), Mas("Experiencia en retail", "HV p.1"), Menos("Vive en Soacha: desplazamiento largo", "HV p.1")],
                    Avance.PreseleccionCompleta),
                new("María José Herrera", "1026789012", "3209876543", "maria.herrera@ejemplo.co", "Bogotá",
                    [Exp("Impulsadora", "Lácteos del Campo", "05/2023", "12/2024")],
                    [Est("Bachiller académico", "Colegio Nueva Esperanza", "2022")], ["Impulso", "Atención al público"],
                    64, [Mas("Experiencia en impulso de marca", "HV p.1"), Menos("Menos de 2 años de experiencia", "HV p.1")],
                    Avance.PreseleccionInvitada),
                new("Carlos Mario Díaz", "1012345098", "3145678901", "carlos.diaz@ejemplo.co", "Bogotá",
                    [Exp("Mesero", "Restaurante La Plaza", "01/2022", "actual")],
                    [Est("Bachiller académico", "Colegio Distrital Suba", "2020")], ["Atención al público", "Trabajo bajo presión"],
                    58, [Mas("Trato con clientes a diario", "HV p.1"), Menos("Sin experiencia en ventas", "HV p.1")], Avance.Ajustada),
                new("Diana Marcela López", "1030987654", "3018765432", "diana.lopez@ejemplo.co", "Bogotá",
                    [Exp("Auxiliar administrativa", "Papelería Central", "03/2023", "actual")],
                    [Est("Técnico en asistencia administrativa", "SENA", "2022")], ["Excel", "Archivo"],
                    45, [Mas("Manejo de caja menor", "HV p.1"), Menos("Perfil administrativo, no comercial", "HV p.1")], Avance.Recibida),
                new("Kevin Alejandro Suárez", "1007654321", "3226543210", "kevin.suarez@ejemplo.co", "Bogotá",
                    [Exp("Vendedor", "Calzado Urbano", "02/2024", "actual")],
                    [Est("Bachiller académico", "Colegio Rural Usme", "2023")], ["Ventas"],
                    39, [Mas("Experiencia en ventas", "HV p.1"), Menos("No tiene disponibilidad los fines de semana", "Pregunta de filtro")],
                    Avance.Descartada, Respuestas: [false, true]),
            ]),
        new("Agroindustrial del Tolima", "Dos auxiliares de bodega para Ibagué con manejo de inventarios",
            new ContenidoVacante("Auxiliar de bodega", "Auxiliar de bodega", "Ibagué", 2, "Tiempo completo, lunes a sábado",
                "Recibirás y despacharás mercancía, harás conteos de inventario y mantendrás la bodega ordenada y segura.",
                1_750_905m, 1_950_000m,
                ["Bachiller graduado", "1 año de experiencia en bodega o logística", "Manejo de inventarios"],
                ["¿Puedes levantar cargas de hasta 25 kg?", "¿Has manejado inventarios?"]),
            ["Orden", "Seguridad en el trabajo", "Responsabilidad"], ["ATENCION", "NUMERICO", "PERSONALIDAD"], EstadoObjetivo.Publicada, 30,
            [
                new("Óscar Javier Méndez", "1110456789", "3164567890", "oscar.mendez@ejemplo.co", "Ibagué",
                    [Exp("Auxiliar de bodega", "Arrocera del Tolima", "01/2021", "actual"), Exp("Operario", "Frutas del Valle", "02/2019", "12/2020")],
                    [Est("Técnico en logística", "SENA", "2020")], ["Inventarios", "Montacargas", "WMS"],
                    85, [Mas("4 años en bodega de alimentos", "HV p.1"), Mas("Técnico en logística del SENA", "HV p.1"), Mas("Certificado en montacargas", "HV p.2")],
                    Avance.TernaOnboarding, Aspiracion: 1_900_000m),
                new("Yeison Andrés Bermúdez", "1110987123", "3176543219", "yeison.bermudez@ejemplo.co", "Ibagué",
                    [Exp("Auxiliar logístico", "Transportes del Sur", "05/2022", "actual")],
                    [Est("Bachiller técnico", "Institución Técnica Ibagué", "2021")], ["Despachos", "Inventarios"],
                    79, [Mas("Experiencia en despachos y cargue", "HV p.1"), Mas("Maneja inventarios cíclicos", "HV p.1")],
                    Avance.TernaDocumentos, Aspiracion: 1_850_000m),
                new("Luisa Fernanda Ortiz", "1110234567", "3189012345", "luisa.ortiz@ejemplo.co", "Ibagué",
                    [Exp("Auxiliar de inventarios", "Droguería Salud Total", "03/2023", "actual")],
                    [Est("Tecnóloga en gestión logística", "Universidad del Tolima", "2022")], ["Inventarios", "Excel"],
                    70, [Mas("Tecnóloga en gestión logística", "HV p.1"), Menos("Experiencia en droguería, no en bodega", "HV p.1")], Avance.PruebasEnviadas),
                new("Duván Esteban Lozano", "1110345678", "3190123456", "duvan.lozano@ejemplo.co", "Espinal",
                    [Exp("Ayudante de construcción", "Obras Civiles Tolima", "01/2023", "actual")],
                    [Est("Bachiller académico", "Colegio Espinal", "2022")], ["Trabajo físico"],
                    61, [Mas("Acostumbrado a trabajo físico", "HV p.1"), Menos("Sin experiencia en inventarios", "HV p.1")], Avance.Recibida),
                new("Paola Andrea Vargas", "1110456123", "3201234567", "paola.vargas@ejemplo.co", "Ibagué",
                    [Exp("Cajera", "Tiendas D1", "06/2023", "actual")],
                    [Est("Bachiller académico", "Colegio Ibagué", "2022")], ["Caja", "Conteo de mercancía"],
                    52, [Mas("Conteo y recibo de mercancía en tienda", "HV p.1"), Menos("No tiene experiencia en bodega", "HV p.1")], Avance.Recibida),
            ]),
        new(null, "Un analista de nómina para la oficina de Bogotá",
            new ContenidoVacante("Analista de nómina", "Analista de nómina", "Bogotá", 1, "Tiempo completo, lunes a viernes",
                "Liquidarás la nómina quincenal de los trabajadores en misión, novedades, incapacidades y la seguridad social (PILA).",
                3_200_000m, 3_800_000m,
                ["Profesional o tecnólogo en contaduría o administración", "2 años liquidando nómina", "Manejo de PILA"],
                ["¿Has liquidado nómina de más de 200 personas?"]),
            ["Precisión", "Confidencialidad"], ["NUMERICO", "PERSONALIDAD"], EstadoObjetivo.EnAprobacion, 2, []),
        new("Retail Express Colombia", "Coordinador de punto de venta para el centro comercial Andino",
            new ContenidoVacante("Coordinador de punto de venta", "Coordinador de tienda", "Bogotá", 1, "Tiempo completo, turnos rotativos",
                "Liderarás el equipo de la tienda, sus metas, inventario y la experiencia del cliente.",
                2_800_000m, 3_300_000m,
                ["Tecnólogo en áreas comerciales", "2 años coordinando tiendas", "Manejo de indicadores de venta"],
                ["¿Has liderado equipos de más de 5 personas?"]),
            ["Liderazgo", "Orientación a resultados"], ["SJT-SERVICIO", "PERSONALIDAD"], EstadoObjetivo.Borrador, 1, []),
    ];

    private static VacanteEjemplo[] LogisticaCaribe() =>
    [
        new(null, "Conductores de camión C2 para rutas Barranquilla – Cartagena",
            new ContenidoVacante("Conductor de camión C2", "Conductor", "Barranquilla", 4, "Tiempo completo, rutas con pernocta",
                "Conducirás camiones de carga seca en rutas regionales, harás el cargue y entregarás con soporte firmado.",
                2_200_000m, 2_600_000m,
                ["Licencia de conducción C2 vigente", "2 años de experiencia en carga pesada", "Sin comparendos pendientes"],
                ["¿Tienes licencia C2 vigente?", "¿Puedes hacer rutas con pernocta?"]),
            ["Seguridad vial", "Responsabilidad"], ["ATENCION", "PERSONALIDAD"], EstadoObjetivo.Publicada, 12,
            [
                new("Rafael Antonio Barrios", "72145678", "3005678123", "rafael.barrios@ejemplo.co", "Barranquilla",
                    [Exp("Conductor C2", "Transcaribe Carga", "01/2019", "actual")], [Est("Bachiller académico", "Colegio Barranquilla", "2005")],
                    ["Conducción de carga pesada", "Mecánica básica"], 90,
                    [Mas("6 años como conductor C2", "HV p.1"), Mas("Licencia C2 vigente", "Pregunta de filtro"), Mas("Mecánica básica", "HV p.1")], Avance.PreseleccionCompleta,
                    Aspiracion: 2_500_000m),
                new("Wilmer José Pacheco", "72987654", "3018901234", "wilmer.pacheco@ejemplo.co", "Soledad",
                    [Exp("Conductor", "Distribuidora Costa", "03/2021", "actual")], [Est("Bachiller académico", "Colegio Soledad", "2010")],
                    ["Conducción", "Entregas"], 77, [Mas("4 años de conducción de reparto", "HV p.1"), Menos("Su licencia es C1, no C2", "HV p.1")], Avance.Recibida),
                new("Jorge Luis Ariza", "8765432", "3123456789", "jorge.ariza@ejemplo.co", "Barranquilla",
                    [Exp("Conductor de bus", "Sobusa", "06/2018", "actual")], [Est("Bachiller académico", "Colegio Malambo", "2003")],
                    ["Conducción de pasajeros"], 68, [Mas("Licencia C2 vigente", "Pregunta de filtro"), Menos("Sin experiencia en carga", "HV p.1")], Avance.Recibida),
                new("Luis Fernando Charris", "1043987654", "3134567890", "luis.charris@ejemplo.co", "Barranquilla",
                    [Exp("Ayudante de camión", "Mudanzas Express", "01/2024", "actual")], [Est("Bachiller académico", "Colegio Puerto Colombia", "2020")],
                    ["Cargue y descargue"], 41, [Mas("Conoce el cargue", "HV p.1"), Menos("No tiene licencia C2", "Pregunta de filtro")], Avance.Descartada,
                    Respuestas: [false, true]),
            ]),
    ];

    private static VacanteEjemplo[] ClinicaDelValle() =>
    [
        new(null, "Auxiliares de enfermería para hospitalización, turnos de 12 horas",
            new ContenidoVacante("Auxiliar de enfermería", "Auxiliar de enfermería", "Cali", 3, "Turnos de 12 horas, rotativos",
                "Cuidarás a los pacientes hospitalizados: signos vitales, administración de medicamentos bajo supervisión y registro en la historia clínica.",
                2_000_000m, 2_400_000m,
                ["Técnico laboral en auxiliar de enfermería", "Registro en el ReTHUS", "Curso de soporte vital básico vigente"],
                ["¿Tienes registro en el ReTHUS?", "¿Puedes trabajar turnos nocturnos?"]),
            ["Vocación de servicio", "Trabajo en equipo"], ["SJT-SERVICIO", "PERSONALIDAD"], EstadoObjetivo.Publicada, 9,
            [
                new("Sandra Milena Valencia", "1144567890", "3157890123", "sandra.valencia@ejemplo.co", "Cali",
                    [Exp("Auxiliar de enfermería", "Hospital San Juan", "02/2020", "actual")], [Est("Técnico en auxiliar de enfermería", "Instituto Salud Valle", "2019")],
                    ["Signos vitales", "Historia clínica", "Soporte vital básico"], 92,
                    [Mas("5 años en hospitalización", "HV p.1"), Mas("Registro ReTHUS", "Pregunta de filtro"), Mas("Curso de soporte vital vigente", "HV p.2")],
                    Avance.PruebasCompletas, Aspiracion: 2_300_000m),
                new("Leidy Johana Mosquera", "1143987654", "3168901234", "leidy.mosquera@ejemplo.co", "Jamundí",
                    [Exp("Auxiliar de enfermería", "IPS Salud en Casa", "04/2022", "actual")], [Est("Técnico en auxiliar de enfermería", "SENA", "2021")],
                    ["Atención domiciliaria", "Curaciones"], 74, [Mas("3 años de atención domiciliaria", "HV p.1"), Menos("Poca experiencia en hospitalización", "HV p.1")],
                    Avance.PreseleccionInvitada),
                new("Jhonatan Riascos", "1130123456", "3179012345", "jhonatan.riascos@ejemplo.co", "Cali",
                    [Exp("Camillero", "Clínica Los Andes", "01/2023", "actual")], [Est("Técnico en auxiliar de enfermería", "Cenaltec", "2024")],
                    ["Traslado de pacientes"], 60, [Mas("Experiencia en entorno clínico", "HV p.1"), Menos("Recién graduado como auxiliar", "HV p.1")], Avance.Recibida),
            ]),
        new(null, "Recepcionista para el servicio de urgencias",
            new ContenidoVacante("Recepcionista de urgencias", "Recepcionista", "Cali", 1, "Turnos rotativos",
                "Recibirás a los pacientes, verificarás derechos con la EPS y orientarás a las familias en la sala de espera.",
                1_750_905m, 1_950_000m,
                ["Bachiller graduado", "1 año en atención al usuario en salud", "Manejo de software de admisiones"],
                ["¿Has verificado derechos con EPS?"]),
            ["Empatía", "Manejo de situaciones difíciles"], ["SJT-SERVICIO", "PERSONALIDAD"], EstadoObjetivo.Borrador, 1, []),
    ];
}
