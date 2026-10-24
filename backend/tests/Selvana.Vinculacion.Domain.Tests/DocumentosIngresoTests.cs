using FluentAssertions;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Domain.Tests;

public class DocumentosIngresoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);

    private static ProcesoVinculacion Aceptado(TipoContrato tipo = TipoContrato.Indefinido)
    {
        var p = ProcesoVinculacion.Abrir(Guid.NewGuid(), "Servicios Andinos", null, null,
            new PersonaVinculada { Nombre = "Diana Patricia Gómez Herrera", Documento = "1016554209" },
            new VacanteOrigen { VacanteId = Guid.NewGuid(), PostulacionId = Guid.NewGuid(), Cargo = "Montacarguista" },
            new CondicionesContratacion { Tipo = tipo, SalarioCop = 2_000_000m, FechaIngreso = new DateOnly(2026, 11, 2), PeriodoPruebaDias = 60 }, "Laura", Ahora);
        p.RegistrarBorrador("Hola [TRABAJADOR]", [new Clausula { Titulo = "PRIMERA", Texto = "Partes" }], [], [], "m", 0, Ahora);
        p.AprobarOferta([], "h", "Laura", Ahora);
        p.ResponderOferta(true, "Diana", "1016554209", null, "::1", "Chrome", Ahora).IsSuccess.Should().BeTrue();
        return p;
    }

    private static LecturaDocumento Buena(string documento = "1016554209") => new()
    {
        Legible = true, TipoCorrecto = true,
        Campos = [new CampoLeido { Campo = "número de documento", Valor = documento }],
        CoincideTitular = RequisitosDocumentales.CoincideTitular([new CampoLeido { Campo = "número de documento", Valor = documento }], "Diana Patricia Gómez Herrera", "1016554209"),
    };

    [Fact]
    public void AlAceptarSePidenLosDocumentosDelTipoDeContrato()
    {
        var p = Aceptado();
        p.Documentos.Should().Contain(d => d.Tipo == TipoDocumentoIngreso.Cedula && d.Obligatorio && !d.LoCargaEmpresa);
        p.Documentos.Should().Contain(d => d.Tipo == TipoDocumentoIngreso.ExamenMedico && d.LoCargaEmpresa);
        p.Documentos.Should().NotContain(d => d.Tipo == TipoDocumentoIngreso.CartaInstitucion);
        Aceptado(TipoContrato.Aprendizaje).Documentos.Should().Contain(d => d.Tipo == TipoDocumentoIngreso.CartaInstitucion && d.Obligatorio);
    }

    [Fact]
    public void CoincidenciaDelTitularPorNumeroOPorApellidos()
    {
        RequisitosDocumentales.CoincideTitular([new CampoLeido { Campo = "número", Valor = "1.016.554.209" }], "Diana Gómez", "1016554209").Should().BeTrue();
        RequisitosDocumentales.CoincideTitular([new CampoLeido { Campo = "nombre del titular", Valor = "GOMEZ HERRERA DIANA" }], "Diana Patricia Gómez Herrera", "").Should().BeTrue();
        RequisitosDocumentales.CoincideTitular([new CampoLeido { Campo = "número", Valor = "79123456" }], "Diana Gómez", "1016554209").Should().BeFalse();
        RequisitosDocumentales.CoincideTitular([new CampoLeido { Campo = "banco", Valor = "Bancolombia" }], "Diana Gómez", "1016554209").Should().BeNull();
    }

    [Fact]
    public void ElCandidatoNoPuedeSubirLosDeLaEmpresaYUnaLecturaViejaSeIgnora()
    {
        var p = Aceptado();
        p.SubirDocumento(TipoDocumentoIngreso.ExamenMedico, Guid.NewGuid(), "x.pdf", true, "Diana", Ahora).Error.Should().Be(DocumentosErrors.LoCargaEmpresa);

        var primero = Guid.NewGuid();
        p.SubirDocumento(TipoDocumentoIngreso.Cedula, primero, "cedula.jpg", true, "Diana", Ahora).IsSuccess.Should().BeTrue();
        p.SubirDocumento(TipoDocumentoIngreso.Cedula, Guid.NewGuid(), "cedula2.jpg", true, "Diana", Ahora);
        p.RegistrarLectura(TipoDocumentoIngreso.Cedula, primero, Buena(), Ahora).Should().BeFalse();
        p.Documento(TipoDocumentoIngreso.Cedula)!.Versiones.Should().Be(2);
    }

    [Fact]
    public void RechazarExigeMotivoYPermiteSubirOtroArchivo()
    {
        var p = Aceptado();
        var id = Guid.NewGuid();
        p.SubirDocumento(TipoDocumentoIngreso.CertificadoBancario, id, "banco.pdf", true, "Diana", Ahora);
        p.RegistrarLectura(TipoDocumentoIngreso.CertificadoBancario, id, Buena(), Ahora);
        p.RechazarDocumento(TipoDocumentoIngreso.CertificadoBancario, " ", "Laura", Ahora).Error.Should().Be(DocumentosErrors.SinMotivo);
        p.RechazarDocumento(TipoDocumentoIngreso.CertificadoBancario, "Tiene más de 30 días", "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.SubirDocumento(TipoDocumentoIngreso.CertificadoBancario, Guid.NewGuid(), "banco2.pdf", true, "Diana", Ahora).IsSuccess.Should().BeTrue();
        p.Documento(TipoDocumentoIngreso.CertificadoBancario)!.MotivoRechazo.Should().BeNull();
    }

    [Fact]
    public void ConTodosLosObligatoriosAprobadosPasaAAfiliaciones()
    {
        var p = Aceptado();
        foreach (var d in p.Documentos.Where(x => !x.LoCargaEmpresa && x.Obligatorio).ToList())
        {
            var id = Guid.NewGuid();
            p.SubirDocumento(d.Tipo, id, "a.pdf", true, "Diana", Ahora);
            p.RegistrarLectura(d.Tipo, id, Buena(), Ahora);
        }

        p.CandidatoCompleto.Should().BeTrue();
        p.SinObservaciones.Should().HaveCount(4);
        foreach (var t in p.SinObservaciones.Select(d => d.Tipo).ToList())
        {
            p.AprobarDocumento(t, "Laura", Ahora);
        }

        p.Etapa.Should().Be(EtapaVinculacion.Documentos);
        p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.Antecedentes, Guid.NewGuid(), "ant.pdf", null, null, null, "Laura", Ahora).Error.Should().Be(DocumentosErrors.SinResultado);
        p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.Antecedentes, Guid.NewGuid(), "ant.pdf", ResultadoAntecedentes.SinAnotaciones, null, null, "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.ExamenMedico, Guid.NewGuid(), "examen.pdf", null, "cifrado", false, "Laura", Ahora).IsSuccess.Should().BeTrue();
        p.Etapa.Should().Be(EtapaVinculacion.Afiliaciones);
    }
}
