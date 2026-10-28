using FluentAssertions;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Agentes.Vinculacion;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.Vinculacion.Contracts.Ia;

namespace Selvana.AI.Tests;

public sealed class RedactorOfertaTests
{
    private static readonly CondicionesOferta Condiciones = new("Servicios Andinos", "Logística Funza", "Montacarguista", "Funza", "Tiempo completo",
        "Misión en empresa usuaria", 1_950_000m, true, new DateOnly(2026, 11, 2), new DateOnly(2027, 5, 1), 36, "Operar montacargas en el CEDI", "Opera montacargas",
        ["Licencia de montacargas"]);

    [Fact]
    public void LaPreguntaNoLlevaNombreNiDocumentoYTraeCifrasExactas()
    {
        var texto = RedactorOfertaAgente.Pregunta(Condiciones, new DateOnly(2026, 10, 23));
        texto.Should().Contain("$1.950.000").And.Contain("02/11/2026").And.Contain("Logística Funza").And.Contain("FECHA DE HOY: 23/10/2026");
    }

    [Fact]
    public void LaPreguntaTraeComoSeTrabajaElTipoDeContratoYSuModalidad()
    {
        var c = Condiciones with { TipoContrato = "Fijo a 6 meses", Modalidad = "Término fijo", ComoSeTrabaja = "Se especifican fecha de inicio y fin; preaviso de 30 días." };
        RedactorOfertaAgente.Pregunta(c).Should().Contain("Modalidad legal: Término fijo").And.Contain("preaviso de 30 días");
    }

    [Fact]
    public void LaPlantillaPdfVaComoArchivoYLaDeWordComoTexto()
    {
        RedactorOfertaAgente.ContenidoPlantilla(null).Should().BeEmpty();
        RedactorOfertaAgente.ContenidoPlantilla(new PlantillaContrato([1, 2, 3], "application/pdf"))
            .Should().HaveCount(2).And.ContainItemsAssignableTo<Microsoft.Extensions.AI.DataContent>();

        using var memoria = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(memoria, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        using (var escritor = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
        {
            escritor.Write("""<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>PRIMERA. Objeto</w:t></w:r></w:p></w:body></w:document>""");
        }

        var word = RedactorOfertaAgente.ContenidoPlantilla(new PlantillaContrato(memoria.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document")).ToList();
        word.Should().ContainSingle().Which.Should().BeOfType<Microsoft.Extensions.AI.TextContent>()
            .Which.Text.Should().Contain("PLANTILLA DEL CONTRATO").And.Contain("PRIMERA. Objeto");
    }

    [Fact]
    public void AlertaSiElTextoNoTraeElSalarioNiLaFechaYRetiraRazonesDiscriminatorias()
    {
        var crudo = new BorradorOferta("Hola [TRABAJADOR]: bienvenido.",
            [new ClausulaRedactada("PRIMERA. Partes", "[TRABAJADOR], cédula [DOCUMENTO]"), new ClausulaRedactada("", "conducci\\u00f3n segura")],
            [new RazonRedaccion("+", "Se omite por su edad", "Condiciones"), new RazonRedaccion("-", "Periodo de prueba de la quinta parte", "Art. 78 CST")], []);

        var v = RedactorOfertaAgente.Validar(crudo, Condiciones, new FiltroVariablesProtegidas());

        v.Razones.Should().ContainSingle().Which.Signo.Should().Be("−");
        v.Clausulas[1].Titulo.Should().Be("Cláusula");
        v.Clausulas[1].Texto.Should().Be("conducción segura");
        v.Alertas.Should().Contain(a => a.Contains("$1.950.000", StringComparison.Ordinal)).And.Contain(a => a.Contains("02/11/2026", StringComparison.Ordinal));
    }

    [Fact]
    public void SinAlertasCuandoElTextoTraeLoAprobado()
    {
        var crudo = new BorradorOferta("Hola [TRABAJADOR]: ingresas el 02/11/2026 con salario de $1.950.000.",
            [new ClausulaRedactada("PRIMERA. Partes", "[TRABAJADOR], cédula [DOCUMENTO]")], [], []);
        RedactorOfertaAgente.Validar(crudo, Condiciones, new FiltroVariablesProtegidas()).Alertas.Should().BeEmpty();
    }
}
