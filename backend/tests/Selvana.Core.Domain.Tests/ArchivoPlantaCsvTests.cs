using System.Text;
using FluentAssertions;
using Selvana.Integrations.Nomina;

namespace Selvana.Core.Domain.Tests;

public sealed class ArchivoPlantaCsvTests
{
    private static Task<LecturaPlanta> Leer(string csv) =>
        new ArchivoPlantaCsv().LeerAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), CancellationToken.None);

    [Fact]
    public async Task LeeLaPlantaConSalarioEnPesosYFechasColombianas()
    {
        var lectura = await Leer("Cédula;Nombre;Cargo;Salario;Fecha de ingreso;Sede;EPS;Clase riesgo\n1.016.554.209;Diana Gómez;Montacarguista;$1.900.000;02/11/2026;Funza;Sura;3\n");

        lectura.Errores.Should().BeEmpty();
        var e = lectura.Empleados.Should().ContainSingle().Subject;
        e.Documento.Should().Be("1016554209");
        e.SalarioCop.Should().Be(1_900_000m);
        e.FechaIngreso.Should().Be(new DateOnly(2026, 11, 2));
        e.TipoContrato.Should().Be(ArchivoPlantaCsv.ContratoPorDefecto);
        e.ClaseRiesgoArl.Should().Be(3);
    }

    [Fact]
    public async Task ReportaLasFilasConErroresYSigueConLasDemas()
    {
        var lectura = await Leer("documento,nombre,cargo,salario,fecha_ingreso\n1020,Ana,Auxiliar,abc,2026-01-15\n1021,Luis,Auxiliar,1800000,2026-01-15\n");

        lectura.Empleados.Should().ContainSingle().Which.Nombre.Should().Be("Luis");
        lectura.Errores.Should().ContainSingle().Which.Should().Contain("Fila 2").And.Contain("salario");
    }

    [Fact]
    public async Task PideLasColumnasObligatorias()
    {
        var lectura = await Leer("documento,cargo\n1020,Auxiliar\n");

        lectura.Empleados.Should().BeEmpty();
        lectura.Errores.Single().Should().Contain("nombre").And.Contain("salario").And.Contain("fecha_ingreso");
    }
}
