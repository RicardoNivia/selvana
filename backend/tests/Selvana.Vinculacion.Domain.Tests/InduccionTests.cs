using FluentAssertions;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Domain.Tests;

public class InduccionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Ingreso = new(2026, 11, 2);

    /// <summary>Un proceso con el empleado creado (montacarguista: ARL clase III, 60 días de prueba).</summary>
    private static ProcesoVinculacion EnOnboarding(TipoContrato tipo = TipoContrato.Indefinido, string? empresaUsuaria = null)
    {
        var p = ProcesoVinculacion.Abrir(Guid.NewGuid(), "Servicios Andinos", empresaUsuaria is null ? null : Guid.NewGuid(), empresaUsuaria,
            new PersonaVinculada { Nombre = "Diana Gómez", Documento = "1016554209" },
            new VacanteOrigen { VacanteId = Guid.NewGuid(), PostulacionId = Guid.NewGuid(), Cargo = "Montacarguista" },
            new CondicionesContratacion { Tipo = tipo, SalarioCop = 2_000_000m, FechaIngreso = Ingreso, PeriodoPruebaDias = 60, Ciudad = "Funza" }, "Laura", Ahora);
        p.RegistrarBorrador("Hola", [new Clausula { Titulo = "PRIMERA", Texto = "Partes" }], [], [], "m", 0, Ahora);
        p.AprobarOferta([], "h", "Laura", Ahora);
        p.ResponderOferta(true, "Diana", "1016554209", null, "::1", "Chrome", Ahora);
        foreach (var d in p.Documentos.Where(x => !x.LoCargaEmpresa && x.Obligatorio).ToList())
        {
            var id = Guid.NewGuid();
            p.SubirDocumento(d.Tipo, id, "a.pdf", true, "Diana", Ahora);
            p.RegistrarLectura(d.Tipo, id, new LecturaDocumento { Legible = true, TipoCorrecto = true, CoincideTitular = true }, Ahora);
            p.AprobarDocumento(d.Tipo, "Laura", Ahora);
        }

        p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.Antecedentes, Guid.NewGuid(), "a.pdf", ResultadoAntecedentes.SinAnotaciones, null, null, "Laura", Ahora);
        p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.ExamenMedico, Guid.NewGuid(), "e.pdf", null, "x", false, "Laura", Ahora);
        foreach (var a in p.Afiliaciones.ToList())
        {
            p.ActualizarAfiliacion(a.Tipo, EstadoAfiliacion.Confirmada, "Entidad", null, a.Tipo == TipoAfiliacion.Arl ? Ingreso.AddDays(-1) : Ingreso, 3, "Laura", Ahora);
        }

        p.EditarAlta("Operaciones", "CC-201", "Funza", "Bancolombia", "Ahorros", "123", "Laura", Ahora);
        p.CrearEmpleado(false, "Laura", Ahora).IsSuccess.Should().BeTrue();
        return p;
    }

    private static readonly TareaPropuesta[] DeLaIa =
    [
        new("Recorrer la bodega y las zonas de cargue", "Con el líder", "Líder inmediato", 0, "Descripción del cargo"),
        new("Inducción en SST", "Repetida", "SST", 0, "Decreto"),
        new("Seguimiento a los 30 días", "Conversación con el líder", "jefe del área", 30, ""),
        new("Practicar con el montacargas acompañado", "Tres turnos", "Líder inmediato", 400, "Requisito"),
    ];

    private static void ConPlanDeLaIa(ProcesoVinculacion p) =>
        p.RegistrarPlanInduccion(DeLaIa, [new RazonOferta { Texto = "Cargo operativo" }], [], "haiku", 0.01m, Ahora).Should().BeTrue();

    [Fact]
    public void AlCrearElEmpleadoNaceElPlanConLasTareasLegales()
    {
        var p = EnOnboarding();
        p.Induccion!.Estado.Should().Be(EstadoPlanInduccion.Redactando);
        p.Induccion.Tareas.Should().OnlyContain(t => t.Obligatoria && !t.DeIa);
        p.Induccion.Tareas.Select(t => t.Titulo).Should().Contain("Inducción en seguridad y salud en el trabajo")
            .And.Contain("Entregar los elementos de protección personal")
            .And.Contain("Evaluar el periodo de prueba");
        p.Induccion.Tareas.Single(t => t.Titulo == "Evaluar el periodo de prueba").Dia.Should().Be(53);
    }

    [Fact]
    public void EnMisionAgregaLaInduccionEnElSitioDeLaEmpresaUsuaria()
    {
        var p = EnOnboarding(TipoContrato.MisionTemporal, "Logística Funza");
        p.Induccion!.Tareas.Should().Contain(t => t.Titulo == "Inducción en el sitio de Logística Funza" && t.Obligatoria);
    }

    [Fact]
    public void LasTareasDeLaIaSeSumanSinDuplicarYConResponsableYDiaValidos()
    {
        var p = EnOnboarding();
        var legales = p.Induccion!.Tareas.Count;
        ConPlanDeLaIa(p);

        var plan = p.Induccion;
        plan.Estado.Should().Be(EstadoPlanInduccion.PorAprobar);
        plan.Tareas.Should().HaveCount(legales + 3, "la de SST repite una legal");
        plan.Tareas.Single(t => t.Titulo == "Seguimiento a los 30 días").Responsable.Should().Be(ResponsablesInduccion.Lider);
        plan.Tareas.Single(t => t.Titulo.StartsWith("Practicar", StringComparison.Ordinal)).Dia.Should().Be(ReglasInduccion.DiaMaximo);
        plan.Tareas.Where(t => t.DeIa).Should().HaveCount(3);
        plan.Tareas.Select(t => t.Dia).Should().BeInAscendingOrder();
    }

    [Fact]
    public void LaFechaQueCaeEnFinDeSemanaPasaAlLunes()
    {
        ReglasInduccion.Fecha(new DateOnly(2026, 11, 2), 5).Should().Be(new DateOnly(2026, 11, 9));
        ReglasInduccion.Fecha(new DateOnly(2026, 11, 2), 1).Should().Be(new DateOnly(2026, 11, 3));
    }

    [Fact]
    public void SiLaIaFallaQuedanLasLegalesYSePuedeAprobarAMano()
    {
        var p = EnOnboarding();
        p.FallaPlanInduccion("Sin modelos", Ahora).Should().BeTrue();
        p.Induccion!.Estado.Should().Be(EstadoPlanInduccion.FallaIa);
        p.AprobarPlanInduccion("Laura", Ahora).IsSuccess.Should().BeTrue();
        p.Induccion.Estado.Should().Be(EstadoPlanInduccion.Aprobado);
    }

    [Fact]
    public void AjustarUnaTareaDeLaIaLeQuitaElVioletaYLasLegalesNoSeQuitan()
    {
        var p = EnOnboarding();
        ConPlanDeLaIa(p);
        var plan = p.Induccion!;
        var deIa = plan.Tareas.First(t => t.DeIa);
        var editadas = plan.Tareas.Select(t => new TareaEditada(t.Id, t.Titulo, t.Descripcion, t.Responsable, t.Asignado, t.Id == deIa.Id ? 1 : t.Dia)).ToList();

        p.EditarPlanInduccion(editadas, "Laura", Ahora).IsSuccess.Should().BeTrue();
        plan.Tareas.Single(t => t.Id == deIa.Id).DeIa.Should().BeFalse();
        plan.Tareas.Count(t => t.DeIa).Should().Be(2);

        var sinLegal = editadas.Where(t => !plan.Tareas.Single(x => x.Id == t.Id).Obligatoria).ToList();
        p.EditarPlanInduccion(sinLegal, "Laura", Ahora).Error.Should().Be(InduccionErrors.ObligatoriaNoSeQuita);

        var nueva = editadas.Append(new TareaEditada(null, "Entregar el carné", "", "Recursos Humanos", "Camilo", 0)).ToList();
        p.EditarPlanInduccion(nueva, "Laura", Ahora).IsSuccess.Should().BeTrue();
        plan.Tareas.Single(t => t.Titulo == "Entregar el carné").Fuente.Should().Be("Agregada por Laura");
    }

    [Fact]
    public void ElSeguimientoExigeElPlanAprobadoYMotivoParaNoAplica()
    {
        var p = EnOnboarding();
        ConPlanDeLaIa(p);
        var legal = p.Induccion!.Tareas.First(t => t.Obligatoria);
        var opcional = p.Induccion.Tareas.First(t => !t.Obligatoria);

        p.ActualizarTarea(legal.Id, EstadoTarea.Hecha, null, "Laura", Ahora).Error.Should().Be(InduccionErrors.NoAprobado);
        p.AprobarPlanInduccion("Laura", Ahora).IsSuccess.Should().BeTrue();

        p.ActualizarTarea(legal.Id, EstadoTarea.NoAplica, "No hace falta", "Laura", Ahora).Error.Should().Be(InduccionErrors.ObligatoriaNoAplica);
        p.ActualizarTarea(opcional.Id, EstadoTarea.NoAplica, " ", "Laura", Ahora).Error.Should().Be(InduccionErrors.SinNota);
        p.ActualizarTarea(opcional.Id, EstadoTarea.NoAplica, "Ya conoce la bodega", "Laura", Ahora).IsSuccess.Should().BeTrue();
        opcional.CerradaPor.Should().Be("Laura");
    }

    [Fact]
    public void LasPendientesConFechaPasadaQuedanVencidas()
    {
        var p = EnOnboarding();
        ConPlanDeLaIa(p);
        p.AprobarPlanInduccion("Laura", Ahora);
        p.TareasVencidas(Ingreso.AddDays(3)).Should().OnlyContain(t => t.Fecha < Ingreso.AddDays(3)).And.NotBeEmpty();
        p.TareasVencidas(Ingreso.AddDays(-10)).Should().BeEmpty();
    }

    [Fact]
    public void SoloSeCierraConTodasLasTareasCerradasYQuedaVinculado()
    {
        var p = EnOnboarding();
        ConPlanDeLaIa(p);
        p.AprobarPlanInduccion("Laura", Ahora);
        p.CerrarOnboarding("Laura", Ahora).Error.Should().Be(InduccionErrors.Pendientes);

        foreach (var t in p.Induccion!.Tareas.ToList())
        {
            p.ActualizarTarea(t.Id, EstadoTarea.Hecha, null, "Laura", Ahora).IsSuccess.Should().BeTrue();
        }

        p.CerrarOnboarding("Laura", Ahora).IsSuccess.Should().BeTrue();
        p.Etapa.Should().Be(EtapaVinculacion.Vinculado);
        p.OnboardingCerradoPor.Should().Be("Laura");
    }

    [Fact]
    public void PedirOtraVersionConservaLoQueAgregoUnaPersona()
    {
        var p = EnOnboarding();
        ConPlanDeLaIa(p);
        var plan = p.Induccion!;
        var tareas = plan.Tareas.Select(t => new TareaEditada(t.Id, t.Titulo, t.Descripcion, t.Responsable, t.Asignado, t.Dia))
            .Append(new TareaEditada(null, "Entregar el carné", "", "Recursos Humanos", null, 0)).ToList();
        p.EditarPlanInduccion(tareas, "Laura", Ahora);

        p.PedirPlanInduccion(Ahora).IsSuccess.Should().BeTrue();
        p.Induccion!.Estado.Should().Be(EstadoPlanInduccion.Redactando);
        p.Induccion.Tareas.Should().Contain(t => t.Titulo == "Entregar el carné").And.NotContain(t => t.DeIa);
        p.PedirPlanInduccion(Ahora).Error.Should().Be(InduccionErrors.Redactando);
    }
}
