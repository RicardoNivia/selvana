using Microsoft.EntityFrameworkCore;
using Selvana.Clima.Domain.Encuestas;
using Selvana.Clima.Domain.Pulsos;
using Selvana.Clima.Infrastructure.Persistence;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Contracts.Empresas;
using Selvana.Expediente.Contracts;
using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Infrastructure.Ejemplo;

/// <summary>
/// Clima de ejemplo para el ambiente de pruebas, sin IA: una encuesta abierta con respuestas de casi toda la planta (un área con el clima bajo),
/// comentarios y su análisis por palabras clave, y los pulsos de 30, 60 y 90 días de quienes llegaron al hito, algunos en alerta con su aviso en
/// la bandeja. Idempotente: si la empresa ya tiene encuestas, no la toca.
/// </summary>
internal static class SemillaEjemplo
{
    private static readonly string[] Comentarios =
    [
        "Me gusta el ambiente con los compañeros, somos como una familia.",
        "Los turnos de noche son muy pesados y casi no hay descanso entre semana.",
        "Mi jefe me apoya y me explica cuando tengo dudas.",
        "El salario no alcanza con el costo de vida y no ha habido aumento.",
        "Falta dotación: los uniformes llegan tarde y los guantes se acaban.",
        "Nadie nos avisa a tiempo de los cambios de horario.",
        "Quisiera más capacitación para poder crecer en la empresa.",
        "Excelente empresa, me siento orgulloso de trabajar aquí.",
        "El supervisor a veces grita delante de todos, no es buen trato.",
        "Las reuniones de inicio de turno sirven para enterarse de todo.",
        "Hay mucha carga de trabajo los domingos y festivos.",
        "Buena empresa, pagan cumplido y a tiempo.",
        "No hay oportunidades de ascenso, siempre traen gente de afuera.",
        "El comedor y los baños necesitan mejoras.",
    ];

    public static async Task SembrarAsync(ClimaDbContext db, IPlantaActiva planta, IReadOnlyList<EmpresaResumen> empresas, string porRrhh, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        foreach (var empresa in empresas)
        {
            if (await db.Set<Encuesta>().AnyAsync(e => e.EmpresaId == empresa.Id))
            {
                continue;
            }

            var empleados = await planta.ListarAsync(empresa.Id, CancellationToken.None);
            if (empleados.Count == 0)
            {
                continue;
            }

            var encuesta = Encuesta.Crear(empresa.Id, "Clima laboral 2026", PlantillaClima.Preguntas(), true, PlantillaClima.PreguntaAbierta,
                empleados.Select(e => e.Area), empleados.Select(e => e.Sede), porRrhh, ahora.AddDays(-12));
            Ok(encuesta.Abrir($"ejemplo-{empresa.Id:N}", ahora.AddDays(-12)));
            db.Add(encuesta);

            // Casi toda la planta responde; el área con más gente tiene el clima más bajo
            var areaBaja = empleados.GroupBy(e => e.Area).OrderByDescending(g => g.Count()).First().Key;
            var comentarios = new List<string>();
            foreach (var (e, i) in empleados.Select((e, i) => (e, i)).Where(x => x.i % 7 != 6))
            {
                var baja = e.Area == areaBaja;
                var puntajes = encuesta.Preguntas.Select((p, k) => (p, k)).ToDictionary(x => x.p.Id, x => Math.Clamp((baja ? 2 : 4) + (i + x.k) % 3 - 1
                    - (x.p.Dimension is "Reconocimiento" or "Condiciones de trabajo" ? 1 : 0) + (x.p.Dimension == "Pertenencia" ? 1 : 0), 1, 5));
                var enps = Math.Clamp((baja ? 5 : 8) + i % 4 - 1, 0, 10);
                var comentario = i % 2 == 0 ? Comentarios[(i + (baja ? 1 : 0)) % Comentarios.Length] : "";
                if (comentario.Length > 0)
                {
                    comentarios.Add(comentario);
                }

                db.Add(Ok(encuesta.Responder(e.Area, e.Sede, puntajes, enps, comentario, ahora.AddDays(-10 + i % 9))).Value);
            }

            var (temas, resumen) = TemasPorReglas.Analizar(comentarios);
            encuesta.RegistrarAnalisis("Reglas", temas, resumen, comentarios.Count, null, 0, null, ahora.AddDays(-1));
            encuesta.MarcarAnalisisRevisado(porRrhh);

            // Pulsos de ingreso de quienes están en su hito; algunos responden bajo y quedan en alerta
            foreach (var (e, i) in empleados.Select((e, i) => (e, i)))
            {
                // El pulso del último hito que alcanzó (para que haya historia); los que están en su ventana quedan también pendientes
                var dias = hoy.DayNumber - e.FechaIngreso.DayNumber;
                if (dias is < 30 or > 200)
                {
                    continue;
                }

                var hito = Pulso.HitoVigente(e.FechaIngreso, hoy) ?? Pulso.Hitos.Last(h => h <= dias);

                var p = Pulso.Programar(empresa.Id, e.EmpleadoId, e.Nombre, e.Documento, e.Correo, e.Cargo, e.Area, e.EmpresaUsuariaId, e.FechaIngreso, hito);
                if (i % 3 != 2 || Pulso.HitoVigente(e.FechaIngreso, hoy) is null)
                {
                    var bajo = i % 4 == 0;
                    Ok(p.Responder(bajo ? 2 : 4, bajo ? 3 : 5, bajo ? 2 : 4, bajo ? 5 : 9, bajo ? "Todavía no tengo claro qué se espera de mí y casi no veo a mi líder." : "Me han recibido muy bien.",
                        ahora.AddDays(-2)));
                    if (p.Alerta)
                    {
                        db.AddIntegrationEvent(new PropuestaPublicada(Guid.CreateVersion7(), ahora.AddDays(-2), empresa.Id, e.EmpresaUsuariaId, "Clima", "AlertaRetiroTemprano", p.Id,
                            $"Alerta de retiro temprano · {p.Nombre}",
                            $"Su pulso de {p.Hito} días salió bajo: {p.Promedio:0.0} de 5 y recomendaría la empresa con {p.Respuesta!.Recomendaria} de 10. Converse con la persona.",
                            null, null, [new RazonPropuesta("−", $"Promedio del pulso {p.Promedio:0.0} de 5", $"Pulso de {p.Hito} días")],
                            [new CampoPropuesta("Cargo", p.Cargo), new CampoPropuesta("Área", p.Area), new CampoPropuesta("Comentario", p.Respuesta.Comentario)],
                            null, "/consola/clima?pestana=pulsos", false, hoy.AddDays(5)));
                    }
                }

                db.Add(p);
            }
        }

        await db.SaveChangesAsync();
    }

    private static T Ok<T>(T resultado) where T : Result =>
        resultado.IsFailure ? throw new InvalidOperationException($"Semilla de ejemplo: {resultado.Error.Code} · {resultado.Error.Description}") : resultado;
}
