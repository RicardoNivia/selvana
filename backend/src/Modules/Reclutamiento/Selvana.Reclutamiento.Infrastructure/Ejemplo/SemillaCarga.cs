using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Empresas;
using Selvana.Infrastructure.Common.Ejemplo;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.Reclutamiento.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Infrastructure.Ejemplo;

/// <summary>
/// Sprint 10 · Selección masiva para las pruebas de carga: una vacante publicada con cientos de postulaciones en todas las etapas, sin IA
/// (puntajes fijados con el modelo «ejemplo (sin IA)»). Solo se crea si se pide con Carga:Postulaciones; idempotente por título.
/// </summary>
internal static class SemillaCarga
{
    public const string Titulo = "Operario de producción · selección masiva";
    private const string Modelo = "ejemplo (sin IA)";
    private const string Rrhh = "Valentina Ríos";

    private static readonly (CanalPostulacion Canal, int Peso)[] Canales =
        [(CanalPostulacion.Portal, 55), (CanalPostulacion.LinkedIn, 20), (CanalPostulacion.Computrabajo, 15), (CanalPostulacion.Referido, 10)];

    public static async Task<int> SembrarAsync(ReclutamientoDbContext db, IAlmacenHojasDeVida almacen, ITokensPruebas tokens, EmpresaResumen empresa, int cantidad,
        TimeProvider reloj)
    {
        if (await db.Set<Vacante>().AnyAsync(v => v.EmpresaId == empresa.Id && v.Titulo == Titulo))
        {
            return 0;
        }

        var ahora = reloj.GetUtcNow();
        var creada = ahora.AddDays(-20);
        var vacante = Ok(Vacante.CrearManual(empresa.Id, empresa.Nombre, null, null, new ContenidoVacante(Titulo, "Operario de producción", "Bogotá", 40,
            "Turnos rotativos de 8 horas", "Selección masiva para la temporada: alistamiento, empaque y control de calidad en línea de producción.",
            1_423_500m, 1_700_000m,
            ["Bachiller", "Seis meses de experiencia en producción o bodega", "Disponibilidad para turnos rotativos"],
            ["¿Tienes disponibilidad para turnos rotativos?"]), Rrhh, creada)).Value;
        Ok(vacante.SolicitarAprobacion(Rrhh, "GerenteRrhh", creada.AddHours(1)));
        Ok(vacante.Aprobar("Sistema", "Administrador", creada.AddHours(2)));
        db.Add(vacante);

        var archivo = almacen.Guardar(empresa.Id, "HV selección masiva.pdf", "application/pdf",
            PdfTexto.Crear(["Hoja de vida de ejemplo para la prueba de carga", "Operario de producción"]), creada);
        var texto = PoliticaDatos.Texto(vacante.EmpresaVisible);
        for (var i = 0; i < cantidad; i++)
        {
            var llego = creada.AddDays(1).AddMinutes(i * 27);
            var nombre = NombresEjemplo.Nombre(i + 9000);
            var documento = $"{1_200_000_000 + i * 17}";
            var candidato = Candidato.Registrar(empresa.Id, new DatosCandidato
            {
                Nombre = nombre, Documento = documento, Celular = NombresEjemplo.Celular(i + 9000), Correo = NombresEjemplo.Correo(nombre, i + 90000),
                Ciudad = i % 4 == 0 ? "Soacha" : "Bogotá", LinkedIn = "", Cursos = [], Formacion = [],
                Experiencia = [new ExperienciaLaboral { Cargo = i % 3 == 0 ? "Auxiliar de bodega" : "Operario de producción", Empresa = "Manufacturas del Centro", Desde = "02/2024", Hasta = "Actual" }],
                Habilidades = ["Trabajo en equipo", "Empaque"],
            }, llego);
            db.Add(candidato);

            var autorizacion = new AutorizacionDatos
            {
                Version = PoliticaDatos.Version, Texto = texto, AceptadaEn = llego, Ip = "181.50.0.10", Navegador = "Prueba de carga",
                Huella = AutorizacionDatos.CalcularHuella(PoliticaDatos.Version, texto, llego, "181.50.0.10", "Prueba de carga", documento),
            };
            var p = Ok(candidato.Postularse(vacante.Id, Canal(i), [new RespuestaFiltro { Pregunta = vacante.PreguntasFiltro[0], Respuesta = i % 9 != 0 }],
                autorizacion, archivo, [], llego)).Value;
            var puntaje = 30 + (i * 37 % 66);
            Ok(candidato.RegistrarAfinidad(p.Id, puntaje,
                [new RazonPuntaje { Signo = "+", Texto = "Experiencia en producción o bodega", Evidencia = "HV · experiencia 1" },
                 new RazonPuntaje { Signo = puntaje >= 60 ? "+" : "−", Texto = puntaje >= 60 ? "Cumple los requisitos de la vacante" : "Le falta parte de la experiencia", Evidencia = "HV" }],
                vacante.Requisitos.Take(puntaje >= 60 ? 3 : 1), vacante.Requisitos.Skip(puntaje >= 60 ? 3 : 1), Modelo, llego.AddMinutes(2)));

            // Reparto por etapas: 55 % recibidas, 15 % preselección, 10 % pruebas, 5 % terna y 15 % descartadas
            var paso = llego.AddDays(1);
            var tramo = i % 20;
            if (tramo >= 11)
            {
                if (tramo >= 17)
                {
                    Ok(candidato.MoverPostulacion(p.Id, EtapaPostulacion.Descartada, "No cumple la disponibilidad de turnos rotativos", Rrhh, paso));
                    continue;
                }

                Ok(candidato.MoverPostulacion(p.Id, EtapaPostulacion.Preseleccion, null, Rrhh, paso));
                db.Add(ConversacionPreseleccion.Invitar(empresa.Id, null, vacante.Id, p.Id, candidato.Id, tokens.Generar().Huella,
                    $"Hola, {nombre.Split(' ')[0]}. ¿Te puedo hacer unas preguntas sobre la vacante?", Rrhh, paso));
                if (tramo >= 14)
                {
                    Ok(candidato.MoverPostulacion(p.Id, EtapaPostulacion.Pruebas, null, Rrhh, paso.AddDays(2)));
                }

                if (tramo == 16)
                {
                    Ok(candidato.MoverPostulacion(p.Id, EtapaPostulacion.Terna, null, Rrhh, paso.AddDays(5)));
                }
            }

            if (i % 200 == 199)
            {
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync();
        return cantidad;
    }

    private static CanalPostulacion Canal(int i)
    {
        var x = i * 37 % 100;
        foreach (var (canal, peso) in Canales)
        {
            if (x < peso)
            {
                return canal;
            }

            x -= peso;
        }

        return CanalPostulacion.Portal;
    }

    private static T Ok<T>(T resultado) where T : Result =>
        resultado.IsFailure ? throw new InvalidOperationException($"Semilla de carga: {resultado.Error.Code} · {resultado.Error.Description}") : resultado;
}
