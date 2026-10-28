using FluentAssertions;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Domain.Tests;

public class AfiliacionesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Ingreso = new(2026, 11, 2);

    /// <summary>Un proceso con todos los documentos aprobados; la IA leyó la EPS y la cuenta bancaria.</summary>
    private static ProcesoVinculacion EnAfiliaciones(bool examenConRestricciones = false)
    {
        var p = ProcesoVinculacion.Abrir(Guid.NewGuid(), "Servicios Andinos", null, null, new PersonaVinculada { Nombre = "Diana Gómez", Documento = "1016554209" },
            new VacanteOrigen { VacanteId = Guid.NewGuid(), PostulacionId = Guid.NewGuid(), Cargo = "Montacarguista" },
            new CondicionesContratacion { Tipo = TipoContrato.Indefinido, SalarioCop = 2_000_000m, FechaIngreso = Ingreso, PeriodoPruebaDias = 60, Ciudad = "Funza" }, "Laura", Ahora);
        p.RegistrarBorrador("Hola", [new Clausula { Titulo = "PRIMERA", Texto = "Partes" }], [], [], "m", 0, Ahora);
        p.AprobarOferta([], "h", "Laura", Ahora);
        p.ResponderOferta(true, "Diana", "1016554209", null, "::1", "Chrome", Ahora);
        var campos = new Dictionary<TipoDocumentoIngreso, List<CampoLeido>>
        {
            [TipoDocumentoIngreso.CertificadoEps] = [new() { Campo = "entidad", Valor = "Sura EPS" }],
            [TipoDocumentoIngreso.CertificadoBancario] = [new() { Campo = "banco", Valor = "Bancolombia" }, new() { Campo = "tipo de cuenta", Valor = "Ahorros" },
                new() { Campo = "número de cuenta", Valor = "123-456789-01" }],
        };
        foreach (var d in p.Documentos.Where(x => !x.LoCargaEmpresa && x.Obligatorio).ToList())
        {
            var id = Guid.NewGuid();
            p.SubirDocumento(d.Tipo, id, "a.pdf", true, "Diana", Ahora);
            p.RegistrarLectura(d.Tipo, id, new LecturaDocumento { Legible = true, TipoCorrecto = true, CoincideTitular = true, Campos = campos.GetValueOrDefault(d.Tipo) ?? [] }, Ahora);
            p.AprobarDocumento(d.Tipo, "Laura", Ahora);
        }

        p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.Antecedentes, Guid.NewGuid(), "a.pdf", ResultadoAntecedentes.SinAnotaciones, null, null, "Laura", Ahora);
        p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.ExamenMedico, Guid.NewGuid(), "e.pdf", null, "x", examenConRestricciones, "Laura", Ahora);
        return p;
    }

    private static void ConfirmarTodo(ProcesoVinculacion p)
    {
        p.ActualizarAfiliacion(TipoAfiliacion.Eps, EstadoAfiliacion.Confirmada, "Sura EPS", null, Ingreso, null, "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.ActualizarAfiliacion(TipoAfiliacion.Pension, EstadoAfiliacion.Confirmada, "Porvenir", null, Ingreso, null, "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.ActualizarAfiliacion(TipoAfiliacion.Arl, EstadoAfiliacion.Confirmada, "Positiva", "R-1", Ingreso.AddDays(-1), 3, "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.ActualizarAfiliacion(TipoAfiliacion.CajaCompensacion, EstadoAfiliacion.Confirmada, "Compensar", null, Ingreso, null, "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.EditarAlta("Operaciones", "CC-201 CEDI Funza", "Funza", "Bancolombia", "Ahorros", "123-456789-01", "Laura", Ahora).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AlPasarAAfiliacionesSePrellenaConLoQueLeyoLaIa()
    {
        var p = EnAfiliaciones();
        p.Etapa.Should().Be(EtapaVinculacion.Afiliaciones);
        p.Afiliaciones.Should().HaveCount(4);
        p.Afiliaciones.Single(a => a.Tipo == TipoAfiliacion.Eps).Entidad.Should().Be("Sura EPS");
        p.Afiliaciones.Single(a => a.Tipo == TipoAfiliacion.Arl).ClaseRiesgo.Should().Be(3);
        p.Alta.NumeroCuenta.Should().Be("123-456789-01");
        p.Alta.SugeridosPorIa.Should().Contain("número de cuenta");
        p.ListoParaCrearEmpleado(DateOnly.FromDateTime(Ahora.UtcDateTime)).Should().BeFalse();
    }

    [Fact]
    public void LaArlDebeCubrirDesdeElDiaAnteriorAlIngreso()
    {
        var p = EnAfiliaciones();
        ConfirmarTodo(p);
        p.ActualizarAfiliacion(TipoAfiliacion.Arl, EstadoAfiliacion.Confirmada, "Positiva", null, Ingreso, 3, "Laura", Ahora);
        ReglasAlta.Revisar(p, DateOnly.FromDateTime(Ahora.UtcDateTime)).Should().Contain(a => a.Nivel == NivelAlerta.Bloquea && a.Fuente == "Decreto 1072 de 2015");
        p.CrearEmpleado(false, "Laura", Ahora).Error.Should().Be(AltaErrors.Incompleto);
    }

    [Fact]
    public void ConTodoConfirmadoSeCreaElEmpleadoYPasaAOnboarding()
    {
        var p = EnAfiliaciones();
        ConfirmarTodo(p);
        p.Alta.SugeridosPorIa.Should().BeEmpty();
        p.ListoParaCrearEmpleado(DateOnly.FromDateTime(Ahora.UtcDateTime)).Should().BeTrue();
        p.CrearEmpleado(false, "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.Etapa.Should().Be(EtapaVinculacion.Onboarding);
        p.EmpleadoCreadoPor.Should().Be("Laura");
    }

    [Fact]
    public void ConExamenConRestriccionesHayQueConfirmarQueSeRevisoYNoAplicaSoloEnAprendizaje()
    {
        var p = EnAfiliaciones(examenConRestricciones: true);
        p.ActualizarAfiliacion(TipoAfiliacion.Pension, EstadoAfiliacion.NoAplica, null, null, null, null, "Laura", Ahora).Error.Should().Be(AltaErrors.NoAplicaSoloPension);
        ConfirmarTodo(p);
        p.CrearEmpleado(false, "Laura", Ahora).Error.Should().Be(AltaErrors.SinConfirmarAlertas);
        p.CrearEmpleado(true, "Laura", Ahora).IsSuccess.Should().BeTrue();
    }

    private static ProcesoVinculacion EnOnboarding()
    {
        var p = EnAfiliaciones();
        ConfirmarTodo(p);
        p.CrearEmpleado(false, "Laura", Ahora).IsSuccess.Should().BeTrue();
        return p;
    }

    private static readonly TareaPropuesta[] TareasIa =
    [
        new("Enviar la bienvenida", "Fecha y lugar", "RRHH", MomentoInduccion.AntesDelIngreso, -3),
        new("Inducción SG-SST", "Riesgos del montacargas", "SST", MomentoInduccion.PrimerDia, 0),
        new("Revisión del primer mes", "Avance", "Líder", MomentoInduccion.PrimerMes, 30),
    ];

    [Fact]
    public void AlCrearElEmpleadoSePideElPlanDeInduccionALaIa()
    {
        EnOnboarding().Induccion.Estado.Should().Be(EstadoPlanInduccion.Generando);
    }

    [Fact]
    public void ElPlanDeLaIaQuedaPorAprobarEnVioletaConSusFechas()
    {
        var p = EnOnboarding();

        p.RegistrarPlanInduccion(TareasIa, [new RazonOferta { Signo = "+", Texto = "SG-SST el primer día", Fuente = "SG-SST" }], [], "haiku", 0.01m, Ahora).Should().BeTrue();

        p.Induccion.Estado.Should().Be(EstadoPlanInduccion.PorAprobar);
        p.Induccion.Tareas.Should().OnlyContain(t => t.DeIa);
        p.Induccion.Tareas[0].Fecha.Should().Be(Ingreso.AddDays(-3));
        p.MarcarTareaInduccion(p.Induccion.Tareas[0].Id, true, "Laura", Ahora).Error.Should().Be(OnboardingErrors.NoEnCurso);
    }

    [Fact]
    public void AprobadoSeHaceSeguimientoYSoloCierraConTodoHecho()
    {
        var p = EnOnboarding();
        p.RegistrarPlanInduccion(TareasIa, [], [], "haiku", 0, Ahora);

        p.AprobarPlanInduccion("Laura", Ahora).IsSuccess.Should().BeTrue();
        p.Induccion.Tareas.Should().OnlyContain(t => !t.DeIa);
        p.MarcarTareaInduccion(p.Induccion.Tareas[0].Id, true, "Camilo", Ahora).IsSuccess.Should().BeTrue();
        p.Induccion.Avance.Should().Be(33);
        p.CerrarOnboarding("Laura", Ahora).Error.Should().Be(OnboardingErrors.Pendientes);

        foreach (var t in p.Induccion.Tareas)
        {
            p.MarcarTareaInduccion(t.Id, true, "Camilo", Ahora);
        }

        p.CerrarOnboarding("Laura", Ahora).IsSuccess.Should().BeTrue();
        p.Etapa.Should().Be(EtapaVinculacion.Vinculado);
        p.Induccion.Estado.Should().Be(EstadoPlanInduccion.Terminado);
    }

    [Fact]
    public void EditarConfirmaLasTareasYConservaLoHecho()
    {
        var p = EnOnboarding();
        p.RegistrarPlanInduccion(TareasIa, [], [], "haiku", 0, Ahora);
        p.AprobarPlanInduccion("Laura", Ahora);
        var primera = p.Induccion.Tareas[0];
        p.MarcarTareaInduccion(primera.Id, true, "Camilo", Ahora);

        p.EditarPlanInduccion([new(primera.Titulo, primera.Detalle, "RRHH", primera.Momento, -5, primera.Id), new("Nueva", "", "Líder", MomentoInduccion.PrimeraSemana, 3)], Ahora)
            .IsSuccess.Should().BeTrue();

        p.Induccion.Tareas.Should().HaveCount(2);
        p.Induccion.Tareas.Single(t => t.Id == primera.Id).Should().Match<TareaInduccion>(t => t.Hecha && t.Fecha == Ingreso.AddDays(-5));
    }

    [Fact]
    public void SiLaIaFallaSePuedeUsarElPlanBase()
    {
        var p = EnOnboarding();
        p.FallaPlanInduccion("Ningún modelo respondió", Ahora).Should().BeTrue();

        p.UsarPlanBase(Ahora).IsSuccess.Should().BeTrue();

        p.Induccion.Origen.Should().Be("Plan base");
        p.Induccion.Tareas.Should().Contain(t => t.Responsable == "SST").And.Contain(t => t.Momento == MomentoInduccion.PeriodoPrueba && t.Dia == 55);
        p.Induccion.Tareas.Should().OnlyContain(t => !t.DeIa);
    }

    [Fact]
    public void ElPlanBaseIncluyeLaEmpresaUsuariaSiLaHay()
    {
        PlanBaseInduccion.Para("Montacarguista", "Logística Funza", 0).Should().Contain(t => t.Responsable == "Empresa usuaria")
            .And.NotContain(t => t.Momento == MomentoInduccion.PeriodoPrueba);
    }
}
