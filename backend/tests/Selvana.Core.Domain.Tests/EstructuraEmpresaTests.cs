using FluentAssertions;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Domain.Tests;

public sealed class EstructuraEmpresaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    private static List<EmpleadoLeido> Nomina()
    {
        var empleados = new List<EmpleadoLeido>();
        void Agregar(int n, string codigo, string cargo, string sede, string cc = "CC-0440")
        {
            for (var i = 0; i < n; i++)
            {
                empleados.Add(new EmpleadoLeido($"{codigo}{empleados.Count:D6}", codigo, cargo, "Bodega", cc, "Bodega Funza", sede));
            }
        }

        Agregar(10, "0412", "AUX BODEGA", "Funza");
        Agregar(3, "0413", "Auxiliar de Bodega", "Funza");
        Agregar(2, "0412", "AUX BODEGA", "");
        Agregar(4, "0231", "JEFE DE BODEGA", "Funza");
        return empleados;
    }

    private static (EstructuraEmpresa Estructura, ImportacionNomina Importacion) Leida()
    {
        var estructura = EstructuraEmpresa.Crear(Guid.NewGuid());
        var importacion = estructura.RegistrarLectura("Carga manual", Nomina(), [], Ahora).Value;
        return (estructura, importacion);
    }

    [Fact]
    public void LaLecturaCuentaCargosYProponeSedeParaLosEmpleadosSinSede()
    {
        var (_, importacion) = Leida();

        importacion.Estado.Should().Be(EstadoImportacion.OrganizandoConIa);
        importacion.TotalEmpleados.Should().Be(19);
        importacion.Cargos.Should().HaveCount(3);
        importacion.Propuestas.Should().ContainSingle(p => p.Tipo == TipoPropuesta.AsignarSede && p.Valor == "Funza" && p.Cantidad == 2 && p.Origen == OrigenPropuesta.Regla);
    }

    [Fact]
    public void UnaCedulaRepetidaSeReportaYNoSeCuenta()
    {
        var empleados = Nomina();
        empleados.Add(empleados[0] with { CargoCodigo = "0231" });

        var importacion = EstructuraEmpresa.Crear(Guid.NewGuid()).RegistrarLectura("Carga manual", empleados, [], Ahora).Value;

        importacion.Errores.Should().ContainSingle(e => e.Contains(empleados[0].Documento, StringComparison.Ordinal));
        importacion.TotalEmpleados.Should().Be(18);
    }

    [Fact]
    public void UnaLecturaVaciaEsUnErrorDeValidacion()
    {
        var resultado = EstructuraEmpresa.Crear(Guid.NewGuid()).RegistrarLectura("Carga manual", [], ["Faltan columnas: sede."], Ahora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Type.Should().Be(ErrorType.Validation);
        resultado.Error.Description.Should().Contain("Faltan columnas");
    }

    [Fact]
    public void SoloSeAceptanUnionesDeLaIaConCodigosValidosYSinRepetir()
    {
        var (estructura, importacion) = Leida();

        estructura.AgregarUnionesIa(importacion.Id,
        [
            ("Auxiliar de bodega", ["0412", "0413"], "Mismo cargo."),
            ("Inventado", ["0412", "9999"], "Repite un código y trae uno que no existe."),
            ("Solo uno", ["0231"], "Una unión necesita dos cargos."),
        ], "modelo-prueba");

        importacion.Estado.Should().Be(EstadoImportacion.ListaParaRevisar);
        importacion.Propuestas.Where(p => p.Tipo == TipoPropuesta.UnirCargos).Should().ContainSingle()
            .Which.Evidencia.Should().Be("Nómina · 12 + 3 empleados");
    }

    [Fact]
    public void NoSePuedeAplicarMientrasLaIaTrabaja()
    {
        var (estructura, importacion) = Leida();

        estructura.AplicarImportacion(importacion.Id, "Laura", Ahora).Error.Should().Be(EstructuraErrors.IaTrabajando);
    }

    [Fact]
    public void AplicarUneLosCargosIncluidosYDevuelveLosQueNoTienenPerfil()
    {
        var (estructura, importacion) = Leida();
        estructura.AgregarUnionesIa(importacion.Id, [("Auxiliar de bodega", ["0412", "0413"], "Mismo cargo.")], "modelo");

        var sinPerfil = estructura.AplicarImportacion(importacion.Id, "Laura Moreno", Ahora).Value;

        estructura.Cargos.Should().HaveCount(2);
        var auxiliar = estructura.Cargos.Single(c => c.Nombre == "Auxiliar de bodega");
        auxiliar.Empleados.Should().Be(15);
        auxiliar.CodigosNomina.Should().BeEquivalentTo(["0412", "0413"]);
        sinPerfil.Should().HaveCount(2);
        estructura.Sedes.Single(s => s.Nombre == "Funza").Empleados.Should().Be(19, "los 2 sin sede se asignan a Funza");
        importacion.Estado.Should().Be(EstadoImportacion.Aplicada);
        importacion.AplicadaPor.Should().Be("Laura Moreno");
    }

    [Fact]
    public void UnaUnionExcluidaNoSeAplica()
    {
        var (estructura, importacion) = Leida();
        estructura.AgregarUnionesIa(importacion.Id, [("Auxiliar de bodega", ["0412", "0413"], "Mismo cargo.")], "modelo");
        var union = importacion.Propuestas.Single(p => p.Tipo == TipoPropuesta.UnirCargos);

        estructura.CambiarInclusion(importacion.Id, union.Id, incluida: false).IsSuccess.Should().BeTrue();
        estructura.AplicarImportacion(importacion.Id, "Laura", Ahora);

        estructura.Cargos.Should().HaveCount(3);
    }

    [Fact]
    public void UnaNuevaLecturaConservaElPerfilAprobadoDelCargo()
    {
        var (estructura, importacion) = Leida();
        estructura.RegistrarFallaIa(importacion.Id, "Sin modelos");
        estructura.AplicarImportacion(importacion.Id, "Laura", Ahora);
        var jefe = estructura.Cargos.Single(c => c.CodigosNomina.Contains("0231"));
        estructura.RegistrarBorradorPerfil(jefe.Id, new PerfilCargo { Mision = "Dirigir la bodega." }, [], "modelo");
        estructura.AprobarPerfil(jefe.Id, "Laura", Ahora);

        var segunda = estructura.RegistrarLectura("Carga manual", Nomina(), [], Ahora.AddDays(1)).Value;
        estructura.RegistrarFallaIa(segunda.Id, "Sin modelos");
        var sinPerfil = estructura.AplicarImportacion(segunda.Id, "Laura", Ahora.AddDays(1)).Value;

        estructura.Cargos.Single(c => c.Id == jefe.Id).EstadoPerfil.Should().Be(EstadoPerfil.Aprobado);
        sinPerfil.Should().NotContain(c => c.Id == jefe.Id);
    }

    [Fact]
    public void UnBorradorDeLaIaNoReemplazaUnPerfilAprobado()
    {
        var (estructura, importacion) = Leida();
        estructura.RegistrarFallaIa(importacion.Id, "Sin modelos");
        estructura.AplicarImportacion(importacion.Id, "Laura", Ahora);
        var cargo = estructura.Cargos[0];
        estructura.RegistrarBorradorPerfil(cargo.Id, new PerfilCargo { Mision = "Primera" }, [], "modelo");
        estructura.AprobarPerfil(cargo.Id, "Laura", Ahora);

        estructura.RegistrarBorradorPerfil(cargo.Id, new PerfilCargo { Mision = "Segunda" }, [], "modelo");

        cargo.Perfil!.Mision.Should().Be("Primera");
        cargo.AprobadoPor.Should().Be("Laura");
    }

    [Fact]
    public void NoSeApruebaUnCargoSinBorrador()
    {
        var (estructura, importacion) = Leida();
        estructura.RegistrarFallaIa(importacion.Id, "Sin modelos");
        estructura.AplicarImportacion(importacion.Id, "Laura", Ahora);

        estructura.AprobarPerfil(estructura.Cargos[0].Id, "Laura", Ahora).Error.Should().Be(EstructuraErrors.PerfilSinBorrador);
    }

    private static EstructuraEmpresa Aplicada()
    {
        var (estructura, importacion) = Leida();
        estructura.RegistrarFallaIa(importacion.Id, "Sin modelos");
        estructura.AplicarImportacion(importacion.Id, "Laura", Ahora);
        return estructura;
    }

    private static void Resincronizar(EstructuraEmpresa estructura)
    {
        var otra = estructura.RegistrarLectura("Carga manual", Nomina(), [], Ahora.AddDays(1)).Value;
        estructura.RegistrarFallaIa(otra.Id, "Sin modelos");
        estructura.AplicarImportacion(otra.Id, "Laura", Ahora.AddDays(1));
    }

    [Fact]
    public void LaSincronizacionNoPisaLoQueEditoUnaPersona()
    {
        var estructura = Aplicada();
        var sede = estructura.Sedes.Single();
        var cargo = estructura.Cargos.Single(c => c.CodigosNomina.Contains("0231"));
        var centro = estructura.CentrosCosto.Single();
        estructura.EditarSede(sede.Id, "Centro logístico Funza", activo: true);
        estructura.EditarCargo(cargo.Id, "Jefe(a) de bodega", "Logística", activo: true, perfil: null, "Laura", Ahora);
        estructura.EditarCentroCosto(centro.Id, "Bodega principal", "Funza", activo: true);

        Resincronizar(estructura);

        estructura.Sedes.Should().ContainSingle().Which.Nombre.Should().Be("Centro logístico Funza");
        estructura.Cargos.Single(c => c.Id == cargo.Id).Should().Match<Cargo>(c => c.Nombre == "Jefe(a) de bodega" && c.Area == "Logística" && c.Empleados == 4);
        estructura.CentrosCosto.Single().Nombre.Should().Be("Bodega principal");
    }

    [Fact]
    public void EditarElPerfilQuePropusoLaIaLoApruebaAQuienLoEdito()
    {
        var estructura = Aplicada();
        var cargo = estructura.Cargos[0];
        estructura.RegistrarBorradorPerfil(cargo.Id, new PerfilCargo { Mision = "Borrador" }, [], "modelo");

        estructura.EditarCargo(cargo.Id, cargo.Nombre, cargo.Area, activo: true,
            new PerfilCargo { Mision = "Corregida", Funciones = ["Recibir mercancía"], Requisitos = ["Bachiller"], RiesgoArl = "Clase III" }, "Laura Moreno", Ahora);

        cargo.EstadoPerfil.Should().Be(EstadoPerfil.Aprobado);
        cargo.AprobadoPor.Should().Be("Laura Moreno");
        cargo.Perfil!.Mision.Should().Be("Corregida");
    }

    [Fact]
    public void NoSeCreaNiSeRenombraUnCargoConUnNombreQueYaExiste()
    {
        var estructura = Aplicada();
        var existente = estructura.Cargos[0];

        estructura.CrearCargo($"  {existente.Nombre.ToUpperInvariant()} ", "Bodega", null, "Laura", Ahora).Error.Should().Be(EstructuraErrors.CargoDuplicado);
        estructura.EditarCargo(estructura.Cargos[1].Id, existente.Nombre, "Bodega", true, null, "Laura", Ahora).Error.Should().Be(EstructuraErrors.CargoDuplicado);
    }

    [Fact]
    public void UnCargoCreadoAManoQuedaPendienteDePerfilYNoLoCambiaLaNomina()
    {
        var estructura = Aplicada();

        var nuevo = estructura.CrearCargo("Analista de datos", "bodega", null, "Laura", Ahora).Value;
        Resincronizar(estructura);

        nuevo.Area.Should().Be("Bodega", "el área se toma del maestro, con su nombre guardado");

        nuevo.EstadoPerfil.Should().Be(EstadoPerfil.SinPerfil);
        nuevo.PerfilSolicitadoEn.Should().Be(Ahora);
        estructura.Cargos.Should().Contain(c => c.Id == nuevo.Id && c.Nombre == "Analista de datos");
    }

    [Fact]
    public void UnCargoCreadoConPerfilEscritoAManoQuedaAprobadoPorQuienLoEscribioSinPedirloALaIa()
    {
        var estructura = Aplicada();
        var perfil = new PerfilCargo { Mision = "Analizar datos", Funciones = ["Construir tableros"], Requisitos = ["Profesional en estadística"], RiesgoArl = "Clase I" };

        var nuevo = estructura.CrearCargo("Analista de datos", "Bodega", perfil, "Laura", Ahora).Value;

        nuevo.EstadoPerfil.Should().Be(EstadoPerfil.Aprobado);
        nuevo.AprobadoPor.Should().Be("Laura");
        nuevo.PerfilSolicitadoEn.Should().BeNull();
        nuevo.Perfil!.Mision.Should().Be("Analizar datos");
    }

    [Fact]
    public void UnCargoNuevoSoloSeAsociaAUnAreaActivaDelMaestro()
    {
        var estructura = Aplicada();

        estructura.CrearCargo("Analista de datos", "Administración", null, "Laura", Ahora).Error.Should().Be(EstructuraErrors.AreaNoValida);

        estructura.CrearArea("Administración").IsSuccess.Should().BeTrue();
        estructura.CrearCargo("Analista de datos", "Administración", null, "Laura", Ahora).IsSuccess.Should().BeTrue();

        var area = estructura.Areas.Single(a => a.Nombre == "Bodega");
        estructura.EditarArea(area.Id, "Bodega", activo: false);
        estructura.CrearCargo("Montacarguista", "Bodega", null, "Laura", Ahora).Error.Should().Be(EstructuraErrors.AreaNoValida);
    }

    [Fact]
    public void LosMaestrosCreadosAManoNoSeDuplicanYQuedanComoEditados()
    {
        var estructura = Aplicada();

        estructura.CrearArea(" bodega ").Error.Should().Be(EstructuraErrors.AreaDuplicada);
        estructura.CrearSede("Bogotá").Value.EditadoManualmente.Should().BeTrue();
        estructura.CrearSede("BOGOTÁ").Error.Should().Be(EstructuraErrors.SedeDuplicada);
        estructura.CrearCentroCosto("CC-90", "Comercial", "Bogotá").Value.Sede.Should().Be("Bogotá");
        estructura.CrearCentroCosto("cc-90", "Otro", "").Error.Should().Be(EstructuraErrors.CentroCostoDuplicado);
    }

    [Fact]
    public void UnAreaCreadaAManoSeReconoceSiDespuesLlegaEnLaNomina()
    {
        var estructura = EstructuraEmpresa.Crear(Guid.NewGuid());
        var area = estructura.CrearArea("Bodega").Value;

        Resincronizar(estructura);

        estructura.Areas.Should().ContainSingle(a => a.Nombre == "Bodega").Which.Id.Should().Be(area.Id);
        area.Empleados.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NoSePideOtroBorradorSiElPerfilYaEstaAprobado()
    {
        var estructura = Aplicada();
        var cargo = estructura.Cargos[0];
        estructura.RegistrarBorradorPerfil(cargo.Id, new PerfilCargo { Mision = "Ok" }, [], "modelo");
        estructura.AprobarPerfil(cargo.Id, "Laura", Ahora);

        estructura.SolicitarPerfil(cargo.Id, Ahora).Error.Should().Be(EstructuraErrors.PerfilYaAprobado);
    }

    [Fact]
    public void UnaLecturaNuevaDescartaLaAnteriorSinAplicar()
    {
        var (estructura, primera) = Leida();

        estructura.RegistrarLectura("Carga manual", Nomina(), [], Ahora.AddMinutes(5));

        primera.Estado.Should().Be(EstadoImportacion.Descartada);
    }
}
