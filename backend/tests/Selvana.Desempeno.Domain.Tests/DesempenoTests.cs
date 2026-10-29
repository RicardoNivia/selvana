using FluentAssertions;
using Selvana.Desempeno.Domain.Ciclos;
using Selvana.Desempeno.Domain.Evaluaciones;

namespace Selvana.Desempeno.Domain.Tests;

public class DesempenoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

    private static Ciclo Ciclo(AlcanceEvaluacion alcance = AlcanceEvaluacion.Grados180, int pesoObjetivos = 40) =>
        Ciclos.Ciclo.Crear(Guid.NewGuid(), "Evaluación 2026", TipoCiclo.Anual, alcance, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), pesoObjetivos,
            ["Auxiliar de bodega", "Cajero", "auxiliar de bodega"], "Laura", Ahora).Value;

    private static Evaluacion ConFormulario(AlcanceEvaluacion alcance = AlcanceEvaluacion.Grados180, int pesoObjetivos = 40)
    {
        var c = Ciclo(alcance, pesoObjetivos);
        var (competencias, objetivos, razones) = PlantillaBase.Para("Auxiliar de bodega");
        c.GuardarFormulario("Auxiliar de bodega", competencias, objetivos, "Plantilla base", razones, null, Ahora).IsSuccess.Should().BeTrue();
        return Evaluacion.Crear(c, c.Formulario("Auxiliar de bodega")!, Guid.NewGuid(), "EMP-1", "Diana Gómez", "1016554209", "diana@correo.co", "Logística", "Funza",
            null, Ahora);
    }

    private static Dictionary<string, int> Todas(Evaluacion e, int valor) => e.Competencias.ToDictionary(c => c.Nombre, _ => valor);

    [Fact]
    public void ElCicloTieneUnFormularioPorCargoSinRepetir() => Ciclo().Formularios.Should().HaveCount(2);

    [Theory]
    [InlineData("Auxiliar de bodega")]
    [InlineData("Enfermera jefe")]
    [InlineData("Asesor comercial")]
    [InlineData("Cargo desconocido")]
    public void LaPlantillaBaseSiempreSuma100(string cargo)
    {
        var (competencias, objetivos, _) = PlantillaBase.Para(cargo);

        competencias.Sum(c => c.Peso).Should().Be(100);
        objetivos.Sum(o => o.Peso).Should().Be(100);
        competencias.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void NoSeAbreConFormulariosSinAprobar()
    {
        var c = Ciclo();
        var (competencias, objetivos, razones) = PlantillaBase.Para("Cajero");
        c.GuardarFormulario("Cajero", competencias, objetivos, "IA", razones, "haiku", Ahora);
        c.AprobarFormulario("Cajero", "Laura", Ahora).IsSuccess.Should().BeTrue();

        c.Abrir(Ahora).Error.Should().Be(CicloErrors.SinAprobar, "falta el formulario de auxiliar de bodega");
    }

    [Fact]
    public void LosPesosDelFormularioDebenSumar100()
    {
        var c = Ciclo();
        c.GuardarFormulario("Cajero", [new() { Nombre = "A", Peso = 50 }, new() { Nombre = "B", Peso = 30 }, new() { Nombre = "C", Peso = 10 }], [], "Manual", [], null, Ahora)
            .Error.Should().Be(CicloErrors.Pesos);
    }

    [Fact]
    public void EditarUnFormularioAprobadoLoVuelveABorrador()
    {
        var c = Ciclo();
        var (competencias, objetivos, razones) = PlantillaBase.Para("Cajero");
        c.GuardarFormulario("Cajero", competencias, objetivos, "IA", razones, "haiku", Ahora);
        c.AprobarFormulario("Cajero", "Laura", Ahora);

        c.GuardarFormulario("Cajero", competencias, objetivos, "Manual", [], null, Ahora);

        c.Formulario("Cajero")!.AprobadoEn.Should().BeNull();
    }

    [Fact]
    public void ElPuntajeCombinaObjetivosYCompetencias()
    {
        var e = ConFormulario(pesoObjetivos: 40);
        foreach (var o in e.Objetivos)
        {
            e.RegistrarAvance(o.Id, 100, "Cumplido", "Líder", Ahora);
        }

        e.Calificar(Todas(e, 3), "Buen año.", Potencial.Medio, "Líder", Ahora).IsSuccess.Should().BeTrue();

        // Objetivos al 100 % = 5; competencias en 3 → 5×0,4 + 3×0,6 = 3,8
        e.Puntaje.Should().Be(3.8m);
        e.Caja.Should().Be(6, "potencial medio y alto desempeño");
    }

    [Fact]
    public void CalificarExigeTodasLasCompetencias()
    {
        var e = ConFormulario();

        e.Calificar(new Dictionary<string, int> { [e.Competencias[0].Nombre] = 4 }, "x", Potencial.Alto, "Líder", Ahora).Error.Should().Be(EvaluacionErrors.SinCalificar);
    }

    [Fact]
    public void LasBrechasProponenUnPlanDeDesarrollo()
    {
        var e = ConFormulario();
        var notas = Todas(e, 4);
        notas[e.Competencias[^1].Nombre] = 2;

        e.Calificar(notas, "Bien, con un tema por mejorar.", Potencial.Medio, "Líder", Ahora);

        e.Plan.Should().ContainSingle().Which.Competencia.Should().Be(e.Competencias[^1].Nombre);
        e.Plan[0].Origen.Should().Be("Reglas");
    }

    [Fact]
    public void LaAutoevaluacionNoMueveElPuntajeYNoAplicaEn90()
    {
        var e = ConFormulario();
        e.Autoevaluar(Todas(e, 5), "Me fue muy bien.", Ahora).IsSuccess.Should().BeTrue();
        e.Calificar(Todas(e, 2), "Hay que mejorar.", Potencial.Bajo, "Líder", Ahora);
        e.Competencias.Should().OnlyContain(c => c.Auto == 5 && c.Lider == 2);

        ConFormulario(AlcanceEvaluacion.Grados90).Autoevaluar(new Dictionary<string, int>(), "", Ahora).Error.Should().Be(EvaluacionErrors.SinAutoevaluacion);
    }

    [Fact]
    public void ElComiteMueveConMotivoYManda()
    {
        var e = ConFormulario();
        e.Calificar(Todas(e, 3), "Ok.", Potencial.Medio, "Líder", Ahora);

        e.Calibrar(8, "", "Comité", Ahora).Error.Should().Be(EvaluacionErrors.SinMotivo);
        e.Calibrar(8, "Lideró la apertura de la sede", "Comité", Ahora).IsSuccess.Should().BeTrue();
        e.Caja.Should().Be(8);
    }

    [Fact]
    public void UsarElBorradorDejaElComentarioParaEditar()
    {
        var e = ConFormulario();
        e.PedirBorrador(Ahora);
        e.RegistrarBorrador(["Cumple sus metas"], ["Documentar los despachos"], "Tuviste un buen año…", [], "haiku", 0.01m, null, Ahora);

        e.UsarBorrador(Ahora).IsSuccess.Should().BeTrue();

        e.ComentarioLider.Should().Be("Tuviste un buen año…");
        e.Borrador!.Estado.Should().Be("Usado");
        e.Puntaje.Should().BeNull("el copiloto nunca califica");
    }

    [Fact]
    public void DetectaAlEvaluadorQueCalificaATodosIgual()
    {
        var alertas = SesgosCalificacion.Revisar([("Mario", "Bodega", 3.0m), ("Mario", "Bodega", 3.0m), ("Mario", "Bodega", 3.1m), ("Ana", "Ventas", 2.0m),
            ("Ana", "Ventas", 4.5m), ("Ana", "Ventas", 3.5m)]);

        alertas.Should().Contain(a => a.Tipo == "Todo igual" && a.Texto.StartsWith("Mario", StringComparison.Ordinal));
        alertas.Should().NotContain(a => a.Texto.StartsWith("Ana", StringComparison.Ordinal));
    }
}
