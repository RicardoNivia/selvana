using FluentAssertions;
using Selvana.Reclutamiento.Domain.Vacantes;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class VacanteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);
    private const string Lider = "Lider";
    private const string Rrhh = "GerenteRrhh";
    private const string Gerencia = "Administrador";

    private static ContenidoVacante Contenido(decimal minimo = 1_750_905, decimal maximo = 1_900_000) => new(
        "Auxiliar de bodega", "AUXILIAR DE BODEGA", "Funza", 25, "Turno noche", "Cargue y descargue de mercancía.",
        minimo, maximo, ["Bachiller"], ["¿Tiene disponibilidad para turno noche?"]);

    private static Vacante Solicitada(string? empresaUsuaria = null) =>
        Vacante.Solicitar(Guid.NewGuid(), "Servicios Andinos EST", empresaUsuaria is null ? null : Guid.NewGuid(), empresaUsuaria,
            "Necesito 25 auxiliares de bodega en Funza, turno noche", "laura", Ahora).Value;

    private static Vacante ConBorrador()
    {
        var vacante = Solicitada();
        vacante.RegistrarBorradorIa(Contenido(), "Mínimo legal más auxilio", ["Orden"], ["Turno de 8 horas"], [], "gemini-3.5-flash");
        return vacante;
    }

    [Fact]
    public void SolicitarCreaUnaVacanteRedactandoConCodigoPublico()
    {
        var vacante = Solicitada();

        vacante.Estado.Should().Be(EstadoVacante.Redactando);
        vacante.Codigo.Should().MatchRegex(@"^VAC-261022-[0-9A-F]{4}$");
        vacante.RecibePostulaciones.Should().BeFalse();
    }

    [Fact]
    public void SolicitarSinFraseFalla()
    {
        var resultado = Vacante.Solicitar(Guid.NewGuid(), "Empresa", null, null, "  ", "laura", Ahora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(VacanteErrors.FraseVacia);
    }

    [Fact]
    public void ElPortalMuestraLaEmpresaUsuariaCuandoLaHay()
    {
        Solicitada("Logística del Occidente").EmpresaVisible.Should().Be("Logística del Occidente");
        Solicitada().EmpresaVisible.Should().Be("Servicios Andinos EST");
    }

    [Fact]
    public void ElBorradorDeLaIaNoSeMarcaComoEditadoPorPersona()
    {
        var vacante = ConBorrador();

        vacante.Estado.Should().Be(EstadoVacante.Borrador);
        vacante.EditadaPorPersona.Should().BeFalse();
        vacante.ModeloIa.Should().Be("gemini-3.5-flash");
    }

    [Fact]
    public void LaVacanteDesdeElCatalogoConservaElNombreDelCargo()
    {
        var cargoId = Guid.NewGuid();
        var vacante = Vacante.Solicitar(Guid.NewGuid(), "Servicios Andinos EST", null, null, "Necesito 5 para bodega", "laura", Ahora, cargoId, "Auxiliar de bodega").Value;

        vacante.RegistrarBorradorIa(Contenido() with { Cargo = "Bodeguero" }, "", [], [], [], "gemini-3.5-flash");

        vacante.CargoId.Should().Be(cargoId);
        vacante.Cargo.Should().Be("Auxiliar de bodega");
    }

    [Fact]
    public void NoSePuedeEditarNiPublicarMientrasLaIaRedacta()
    {
        var vacante = Solicitada();

        vacante.Editar(Contenido()).Error.Should().Be(VacanteErrors.Redactando);
        vacante.SolicitarAprobacion("laura", Lider, Ahora).Error.Should().Be(VacanteErrors.Redactando);
    }

    [Fact]
    public void EditarConSalarioMaximoMenorFalla()
    {
        ConBorrador().Editar(Contenido(2_000_000, 1_800_000)).Error.Should().Be(VacanteErrors.SalarioInvalido);
    }

    [Fact]
    public void EditarMarcaLaVacanteComoConfirmadaPorPersona()
    {
        var vacante = ConBorrador();

        vacante.Editar(Contenido() with { Cupos = 30 }).IsSuccess.Should().BeTrue();

        vacante.EditadaPorPersona.Should().BeTrue();
        vacante.Cupos.Should().Be(30);
    }

    [Fact]
    public void SiLaIaFallaQuedaUnBorradorVacioQueNoSePuedePublicar()
    {
        var vacante = Solicitada();
        vacante.RegistrarFallaIa("Ningún modelo respondió");

        vacante.Estado.Should().Be(EstadoVacante.Borrador);
        vacante.Titulo.Should().Be(vacante.Frase);
        vacante.SolicitarAprobacion("laura", Lider, Ahora).Error.Should().Be(VacanteErrors.Incompleta);
    }

    [Fact]
    public void LaRequisicionDelLiderPasaPorRrhhYGerenciaYLaUltimaAprobacionPublica()
    {
        var vacante = ConBorrador();

        vacante.SolicitarAprobacion("Mario (líder)", Lider, Ahora).IsSuccess.Should().BeTrue();
        vacante.Estado.Should().Be(EstadoVacante.EnAprobacion);
        vacante.EtapaPendiente.Should().Be(EtapaAprobacion.Rrhh);
        vacante.RecibePostulaciones.Should().BeFalse();

        vacante.Aprobar("Andrés", Gerencia, Ahora).IsSuccess.Should().BeTrue("el administrador también aprueba por RRHH");
        vacante.EtapaPendiente.Should().Be(EtapaAprobacion.Gerencia);
        vacante.Aprobar("Andrés", Gerencia, Ahora).Error.Should().Be(VacanteErrors.YaAproboOtraEtapa);

        vacante.Aprobar("Gerente general", Gerencia, Ahora.AddHours(1)).IsSuccess.Should().BeTrue();
        vacante.Estado.Should().Be(EstadoVacante.Publicada);
        vacante.PublicadaEn.Should().Be(Ahora.AddHours(1));
        vacante.Aprobaciones.Select(a => (a.Etapa, a.Automatica)).Should().Equal((EtapaAprobacion.Rrhh, false), (EtapaAprobacion.Gerencia, false));
    }

    [Fact]
    public void LaEtapaQuePuedeAprobarQuienPideSeApruebaSola()
    {
        var vacante = ConBorrador();

        vacante.SolicitarAprobacion("Laura", Rrhh, Ahora);

        vacante.EtapaPendiente.Should().Be(EtapaAprobacion.Gerencia);
        vacante.Aprobaciones.Should().ContainSingle().Which.Automatica.Should().BeTrue();
        vacante.Aprobar("Laura", Rrhh, Ahora).Error.Should().Be(VacanteErrors.NoApruebaEstaEtapa);
    }

    [Fact]
    public void DevolverExigeMotivoYVuelveAlBorradorEditable()
    {
        var vacante = ConBorrador();
        vacante.SolicitarAprobacion("Mario (líder)", Lider, Ahora);

        vacante.Editar(Contenido()).Error.Should().Be(VacanteErrors.EnAprobacion);
        vacante.Devolver("Laura", Rrhh, " ", Ahora).Error.Should().Be(VacanteErrors.SinMotivo);
        vacante.Devolver("Laura", Lider, "No", Ahora).Error.Should().Be(VacanteErrors.NoApruebaEstaEtapa);

        vacante.Devolver("Laura", Rrhh, "El salario está por encima de la banda del cargo", Ahora).IsSuccess.Should().BeTrue();

        vacante.Estado.Should().Be(EstadoVacante.Borrador);
        vacante.MotivoDevolucion.Should().Contain("banda");
        vacante.Editar(Contenido()).IsSuccess.Should().BeTrue();
        vacante.SolicitarAprobacion("Mario (líder)", Lider, Ahora).IsSuccess.Should().BeTrue();
        vacante.MotivoDevolucion.Should().BeNull();
        vacante.Aprobaciones.Should().BeEmpty("cada ronda de aprobación empieza de cero");
    }

    [Fact]
    public void PublicarYCerrarControlanLasPostulaciones()
    {
        var vacante = ConBorrador();

        vacante.SolicitarAprobacion("Andrés", Gerencia, Ahora).IsSuccess.Should().BeTrue();
        vacante.RecibePostulaciones.Should().BeTrue();
        vacante.PublicadaEn.Should().Be(Ahora);

        vacante.Cerrar(Ahora.AddDays(10)).IsSuccess.Should().BeTrue();
        vacante.RecibePostulaciones.Should().BeFalse();
        vacante.SolicitarAprobacion("Andrés", Gerencia, Ahora).Error.Should().Be(VacanteErrors.Cerrada);
        vacante.Editar(Contenido()).Error.Should().Be(VacanteErrors.Cerrada);
    }

    [Fact]
    public void UnaVacanteCreadaAManoNaceComoBorradorConfirmadoConSuMunicipio()
    {
        var vacante = Vacante.CrearManual(Guid.NewGuid(), "Servicios Andinos EST", null, null, Contenido() with { CodigoMunicipio = "25286" }, "laura", Ahora).Value;

        vacante.Estado.Should().Be(EstadoVacante.Borrador);
        vacante.EditadaPorPersona.Should().BeTrue();
        vacante.ModeloIa.Should().BeNull();
        vacante.CodigoMunicipio.Should().Be("25286");
        vacante.Codigo.Should().StartWith("VAC-261022-");
        Vacante.CrearManual(Guid.NewGuid(), "Empresa", null, null, Contenido(2_000_000, 1_800_000), "laura", Ahora).Error.Should().Be(VacanteErrors.SalarioInvalido);
    }

    [Fact]
    public void SiLaIaPreguntaAntesDeRedactarLaVacanteQuedaSinContenidoHastaQueLaPersonaResponda()
    {
        var vacante = Solicitada();

        vacante.RegistrarAclaraciones([new AclaracionVacante { Pregunta = "¿Qué cargo necesita?", Opciones = ["Operario de montacargas", " ", "Auxiliar de bodega"] }]);

        vacante.Estado.Should().Be(EstadoVacante.Borrador);
        vacante.Aclaraciones.Should().ContainSingle().Which.Opciones.Should().Equal("Operario de montacargas", "Auxiliar de bodega");
        vacante.Titulo.Should().BeEmpty();

        vacante.PedirRedaccion("Necesito 3 contadores en Bogotá").IsSuccess.Should().BeTrue();
        vacante.Estado.Should().Be(EstadoVacante.Redactando);
        vacante.Frase.Should().Be("Necesito 3 contadores en Bogotá");
        vacante.Aclaraciones.Should().BeEmpty();
    }

    [Fact]
    public void SoloSePideOtroBorradorALaIaMientrasLaVacanteEsBorrador()
    {
        Solicitada().PedirRedaccion("Otra frase").Error.Should().Be(VacanteErrors.NoSePuedeRedactar);
        ConBorrador().PedirRedaccion(" ").Error.Should().Be(VacanteErrors.FraseVacia);
    }
}
