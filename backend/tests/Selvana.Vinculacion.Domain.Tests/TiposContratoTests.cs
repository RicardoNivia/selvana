using FluentAssertions;
using Selvana.Vinculacion.Domain.Procesos;
using Selvana.Vinculacion.Domain.TiposContrato;

namespace Selvana.Vinculacion.Domain.Tests;

public class TiposContratoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LaSemillaTraeIndefinidoTerminoFijoYObraLaborConSuDescripcion()
    {
        var tipos = TipoContratoEmpresa.PorDefectoPara(Guid.NewGuid(), esTemporal: false, Ahora);

        tipos.Select(t => t.Modalidad).Should().BeEquivalentTo([TipoContrato.Indefinido, TipoContrato.TerminoFijo, TipoContrato.ObraOLabor, TipoContrato.Aprendizaje]);
        tipos.Should().OnlyContain(t => t.PorDefecto && t.Activo && t.Descripcion.Length > 100);
        tipos.Single(t => t.Modalidad == TipoContrato.TerminoFijo).Descripcion.Should().Contain("fecha de inicio").And.Contain("fecha de terminación");
    }

    [Fact]
    public void LaMisionEnEmpresaUsuariaSoloSeSiembraEnEmpresasTemporales()
    {
        TipoContratoEmpresa.PorDefectoPara(Guid.NewGuid(), esTemporal: true, Ahora).Should().Contain(t => t.Modalidad == TipoContrato.MisionTemporal);
        TipoContratoEmpresa.PorDefectoPara(Guid.NewGuid(), esTemporal: false, Ahora).Should().NotContain(t => t.Modalidad == TipoContrato.MisionTemporal);
    }

    [Fact]
    public void EditarOSubirPlantillaDejaDeSerPorDefecto()
    {
        var tipo = TipoContratoEmpresa.PorDefectoPara(Guid.NewGuid(), esTemporal: false, Ahora)[0];

        tipo.PonerPlantilla(Guid.NewGuid(), "contrato.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Laura Moreno", Ahora);

        tipo.PorDefecto.Should().BeFalse();
        tipo.PlantillaNombre.Should().Be("contrato.docx");
        tipo.QuitarPlantilla("Laura Moreno", Ahora);
        tipo.PlantillaArchivoId.Should().BeNull();
    }

    [Fact]
    public void ElNombreDelContratoEsElDeLaEmpresaSiLoHay()
    {
        ReglasContrato.Nombre(new CondicionesContratacion { Tipo = TipoContrato.TerminoFijo }).Should().Be("Término fijo");
        ReglasContrato.Nombre(new CondicionesContratacion { Tipo = TipoContrato.TerminoFijo, TipoContratoNombre = "Fijo a 6 meses" }).Should().Be("Fijo a 6 meses");
    }
}
