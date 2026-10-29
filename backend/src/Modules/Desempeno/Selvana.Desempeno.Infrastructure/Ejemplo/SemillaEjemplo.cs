using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Empresas;
using Selvana.Desempeno.Domain.Ciclos;
using Selvana.Desempeno.Domain.Evaluaciones;
using Selvana.Desempeno.Infrastructure.Persistence;
using Selvana.Expediente.Contracts;
using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Infrastructure.Ejemplo;

/// <summary>
/// Desempeño de ejemplo para el ambiente de pruebas, sin IA: un ciclo 2025 cerrado y calibrado (con su 9-box) y un ciclo 2026 abierto con
/// formularios aprobados, objetivos con avance y evidencias, autoevaluaciones, evaluaciones calificadas y en curso, un borrador del copiloto
/// fijado a mano y un evaluador que califica a todos casi igual (para ver la alerta de sesgo). Idempotente: si la empresa ya tiene ciclos, no la toca.
/// </summary>
internal static class SemillaEjemplo
{
    private const string Modelo = "ejemplo (sin IA)";

    private static readonly string[] Evidencias =
    [
        "Cerró el mes con el 98 % de cumplimiento según el informe de operación.",
        "Cero incidentes de seguridad en el trimestre.",
        "Lideró el conteo de inventario de fin de año sin diferencias.",
        "Recibió felicitación escrita de un cliente.",
        "Propuso un cambio en el alistamiento que redujo los tiempos.",
        "Faltó a dos capacitaciones obligatorias.",
    ];

    public static async Task SembrarAsync(DesempenoDbContext db, IPlantaActiva planta, IReadOnlyList<EmpresaResumen> empresas, string porRrhh, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        foreach (var (empresa, n) in empresas.Select((e, n) => (e, n)))
        {
            if (await db.Set<Ciclo>().AnyAsync(c => c.EmpresaId == empresa.Id))
            {
                continue;
            }

            var empleados = await planta.ListarAsync(empresa.Id, CancellationToken.None);
            if (empleados.Count == 0)
            {
                continue;
            }

            var lideres = n == 0 ? new[] { "Valentina Ríos", "Mario Rentería", porRrhh } : n == 1 ? ["Mateo Herrera", "Luz Marina Ortiz"] : ["Juliana Pardo", "Ernesto Salcedo"];
            var cargos = empleados.Select(e => e.Cargo).ToList();

            // Ciclo 2025: cerrado, todo calificado y con ajustes del comité
            var anterior = Ok(Ciclo.Crear(empresa.Id, "Evaluación de desempeño 2025", TipoCiclo.Anual, AlcanceEvaluacion.Grados90, new DateOnly(2025, 1, 1),
                new DateOnly(2025, 12, 31), 40, cargos, porRrhh, ahora.AddMonths(-11))).Value;
            Formularios(anterior, porRrhh, ahora.AddMonths(-11));
            Ok(anterior.Abrir(ahora.AddMonths(-11)));
            db.Add(anterior);
            foreach (var (e, i) in empleados.Select((e, i) => (e, i)))
            {
                var ev = Crear(anterior, e, ahora.AddMonths(-10));
                Avanzar(ev, i, 100, porRrhh, ahora.AddMonths(-2));
                Calificar(ev, i, lideres[i % lideres.Length], ahora.AddMonths(-1));
                if (i % 9 == 4)
                {
                    Ok(ev.Calibrar(Math.Min(9, (ev.Caja ?? 5) + 1), "El comité reconoce que asumió la coordinación del turno sin el cargo.", porRrhh, ahora.AddDays(-25)));
                }

                db.Add(ev);
            }

            Ok(anterior.PasarACalibracion(ahora.AddDays(-28)));
            Ok(anterior.Cerrar(ahora.AddDays(-20)));

            // Ciclo 2026: abierto (180°), con evaluaciones en todas las etapas
            var actual = Ok(Ciclo.Crear(empresa.Id, "Evaluación de desempeño 2026", TipoCiclo.Anual, AlcanceEvaluacion.Grados180, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), 40, cargos, porRrhh, ahora.AddDays(-40))).Value;
            Formularios(actual, porRrhh, ahora.AddDays(-39));
            Ok(actual.Abrir(ahora.AddDays(-35)));
            db.Add(actual);
            foreach (var (e, i) in empleados.Select((e, i) => (e, i)))
            {
                var ev = Crear(actual, e, ahora.AddDays(-35));
                Avanzar(ev, i, 15 + i * 13 % 85, porRrhh, ahora.AddDays(-5));
                if (i % 3 != 2)
                {
                    var auto = ev.Competencias.ToDictionary(c => c.Nombre, c => 3 + (c.Nombre.Length + i) % 3);
                    Ok(ev.Autoevaluar(auto, i % 2 == 0 ? "Siento que este año crecí en el manejo del equipo y en la calidad del trabajo." : "Cumplí mis metas; quiero aprender más del área.",
                        ahora.AddDays(-10)));
                }

                if (i % 2 == 0)
                {
                    // El primer evaluador califica a todos casi igual: el análisis de sesgo lo señala
                    var evaluador = lideres[i % lideres.Length];
                    Calificar(ev, i, evaluador, ahora.AddDays(-3), igual: evaluador == lideres[0]);
                }
                else if (i % 5 == 1)
                {
                    ev.PedirBorrador(ahora.AddDays(-1));
                    ev.RegistrarBorrador(["Cumple de forma constante sus metas del turno", "Sus compañeros lo buscan para resolver dudas"],
                        ["Registrar a tiempo las novedades en el sistema"],
                        "Este año sostuviste un buen ritmo de trabajo y te convertiste en un apoyo para el equipo. Tus resultados en los objetivos lo muestran. " +
                        "Para el próximo periodo, te propongo que registres a tiempo las novedades del turno: así el equipo tiene la información completa.",
                        [new RazonPropuesta { Texto = "Avance alto en los objetivos del periodo", Fuente = "Objetivos" },
                         new RazonPropuesta { Texto = "Evidencia de apoyo al equipo", Fuente = "Evidencias" },
                         new RazonPropuesta { Signo = "−", Texto = "Pocas evidencias sobre el registro de novedades", Fuente = "Evidencias" }],
                        Modelo, 0m, null, ahora.AddDays(-1));
                }

                db.Add(ev);
            }
        }

        await db.SaveChangesAsync();
    }

    private static void Formularios(Ciclo c, string por, DateTimeOffset cuando)
    {
        foreach (var f in c.Formularios)
        {
            var (competencias, objetivos, razones) = PlantillaBase.Para(f.Cargo);
            Ok(c.GuardarFormulario(f.Cargo, competencias, objetivos, "Plantilla base", razones, null, cuando));
            Ok(c.AprobarFormulario(f.Cargo, por, cuando.AddHours(2)));
        }
    }

    private static Evaluacion Crear(Ciclo c, EmpleadoActivo e, DateTimeOffset cuando) =>
        Evaluacion.Crear(c, c.Formulario(e.Cargo)!, e.EmpleadoId, e.Codigo, e.Nombre, e.Documento, e.Correo, e.Area, e.Sede, e.EmpresaUsuariaId, cuando);

    private static void Avanzar(Evaluacion ev, int i, int tope, string por, DateTimeOffset cuando)
    {
        foreach (var (o, k) in ev.Objetivos.Select((o, k) => (o, k)))
        {
            var avance = Math.Clamp(tope - k * 7 + i % 11, 0, 100);
            Ok(ev.RegistrarAvance(o.Id, avance, Evidencias[(i + k) % Evidencias.Length], por, cuando.AddDays(-k)));
        }
    }

    private static void Calificar(Evaluacion ev, int i, string evaluador, DateTimeOffset cuando, bool igual = false)
    {
        var notas = ev.Competencias.Select((c, k) => (c.Nombre, Nota: igual ? 3 : Math.Clamp(1 + (i * 7 + k * 3) % 5, 1, 5))).ToDictionary(x => x.Nombre, x => x.Nota);
        var potencial = (Potencial)(i * 5 % 3);
        Ok(ev.Calificar(notas, notas.Values.Average() >= 3.5
                ? "Tuviste un año muy bueno: cumpliste tus objetivos y eres un apoyo para el equipo. Sigamos trabajando en tu crecimiento."
                : "Cumpliste parte de tus objetivos. Acordamos un plan para fortalecer las competencias con brecha en el próximo semestre.",
            potencial, evaluador, cuando));
    }

    private static T Ok<T>(T resultado) where T : Result =>
        resultado.IsFailure ? throw new InvalidOperationException($"Semilla de ejemplo: {resultado.Error.Code} · {resultado.Error.Description}") : resultado;
}
