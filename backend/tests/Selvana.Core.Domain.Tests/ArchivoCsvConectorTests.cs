using System.Text;
using FluentAssertions;
using Selvana.Integrations.Nomina;

namespace Selvana.Core.Domain.Tests;

public sealed class ArchivoCsvConectorTests
{
    private static Task<LecturaNomina> Leer(string csv) =>
        new ArchivoCsvConector().LeerAsync(new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray()), CancellationToken.None);

    [Fact]
    public async Task LeePuntoYComaConEncabezadosConTildesYMayusculas()
    {
        var lectura = await Leer("Cédula;Cargo_Código;CARGO;Área;Centro Costo Código;Centro de Costo;Sede\n1020;0412;AUX BODEGA;Bodega;CC-0440;Bodega Funza;Funza\n");

        lectura.Errores.Should().BeEmpty();
        lectura.Empleados.Should().ContainSingle().Which.Should().Be(
            new EmpleadoNomina("1020", "0412", "AUX BODEGA", "Bodega", "CC-0440", "Bodega Funza", "Funza"));
    }

    [Fact]
    public async Task LeeComasYCamposEntreComillas()
    {
        var lectura = await Leer("documento,cargo_codigo,cargo,area,centro_costo_codigo,centro_costo,sede\n1020,0233,\"Asesor, servicio\",Contact,CC-0210,\"Norte \"\"BPO\"\"\",Bogotá\n");

        lectura.Empleados.Single().CargoNombre.Should().Be("Asesor, servicio");
        lectura.Empleados.Single().CentroCostoNombre.Should().Be("Norte \"BPO\"");
    }

    [Fact]
    public async Task DiceQueColumnasFaltan()
    {
        var lectura = await Leer("documento;cargo\n1020;Auxiliar\n");

        lectura.Empleados.Should().BeEmpty();
        lectura.Errores.Should().ContainSingle().Which.Should().Contain("cargo_codigo").And.Contain("sede");
    }

    [Fact]
    public async Task ReportaLaFilaSinDocumento()
    {
        var lectura = await Leer("documento;cargo_codigo;cargo;area;centro_costo_codigo;centro_costo;sede\n;0412;AUX;B;CC;C;Funza\n1021;0412;AUX;B;CC;C;Funza\n");

        lectura.Empleados.Should().ContainSingle();
        lectura.Errores.Should().ContainSingle().Which.Should().StartWith("Fila 2");
    }
}
