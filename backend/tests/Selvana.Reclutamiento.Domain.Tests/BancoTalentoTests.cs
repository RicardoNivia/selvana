using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class BancoTalentoTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 22);

    private static DatosCandidato Hv(params (string Cargo, string Desde, string Hasta)[] experiencia) => new()
    {
        Nombre = "Kevin Mosquera",
        Ciudad = "Mosquera, Cundinamarca",
        Experiencia = [.. experiencia.Select(e => new ExperienciaLaboral { Cargo = e.Cargo, Empresa = "Logística del Occidente", Desde = e.Desde, Hasta = e.Hasta })],
        Habilidades = ["Montacargas", "Trabajo en equipo"],
        Cursos = ["Licencia de conducción C1", "Curso de alturas"],
    };

    [Fact]
    public void UnCambioDeTrabajoEnElMismoAnoNoEsSuperposicion()
    {
        var alertas = RevisionHojaDeVida.Revisar(Hv(("Auxiliar de bodega", "2021", "2023"), ("Montacarguista", "2023", "actual")), Hoy);

        alertas.Should().BeEmpty();
    }

    [Fact]
    public void DetectaFechasSuperpuestasHuecosYFechasImposibles()
    {
        var alertas = RevisionHojaDeVida.Revisar(Hv(
            ("Auxiliar de bodega", "01/2019", "12/2021"),
            ("Alistador", "06/2020", "03/2021"),
            ("Montacarguista", "2024", "actual"),
            ("Cajero", "2025", "2023"),
            ("Mensajero", "", "")), Hoy);

        alertas.Select(a => a.Tipo).Should().Contain([RevisionHojaDeVida.Superpuestas, RevisionHojaDeVida.Hueco, RevisionHojaDeVida.FechaImposible, RevisionHojaDeVida.SinFechas]);
        alertas.Should().OnlyContain(a => a.Evidencia.StartsWith("HV · experiencia", StringComparison.Ordinal));
    }

    [Fact]
    public void LosMesesDeExperienciaNoCuentanDosVecesLoQueSeCruza()
    {
        var meses = RevisionHojaDeVida.MesesDeExperiencia(Hv(("A", "01/2020", "12/2021"), ("B", "06/2021", "12/2022")), Hoy);

        meses.Should().Be(35);
    }

    [Fact]
    public void NormalizaCargosYHabilidadesContraElCatalogo()
    {
        Normalizacion.Cargo("Bodeguero").Should().Be("Auxiliar de bodega");
        Normalizacion.Cargo("Gerente de innovación").Should().Be("Gerente de innovación");
        Normalizacion.HabilidadesDe(Hv()).Should().Contain(["Operación de montacargas", "Licencia de conducción C1", "Trabajo en alturas"]);
    }

    [Fact]
    public void EntiendeLaBusquedaEnLenguajeNatural()
    {
        var b = BusquedaTalento.Interpretar("Montacarguistas con licencia C1 en Mosquera");

        b.Ciudad.Should().Be("Mosquera");
        b.Terminos.Should().Equal("montacarg", "licencia", "c1");
        b.Entendido.Should().Contain("Ciudad: Mosquera");
    }

    [Fact]
    public void ElTextoDeBusquedaEncuentraLoQueSeBusca()
    {
        var texto = Normalizacion.TextoBusqueda(Hv(("Montacarguista", "2023", "actual")));
        var b = BusquedaTalento.Interpretar("montacarguistas con pase C1 en Mosquera");

        b.Terminos.Append(Normalizacion.Simplificar(b.Ciudad)).Should().OnlyContain(t => texto.Contains(t, StringComparison.Ordinal));
        texto.Should().NotContain("kevin", "el nombre se busca aparte, no en el perfil");
    }

    [Fact]
    public void PluralesYGeneroCoinciden()
    {
        BusquedaTalento.Raiz("auxiliares").Should().Be("auxiliar");
        BusquedaTalento.Raiz("cajeras").Should().Be("cajer");
        BusquedaTalento.Raiz("cajero").Should().Be("cajer");
        BusquedaTalento.Interpretar("1.023.456.789").Documento.Should().Be("1023456789");
    }
}

public sealed class ConjuntosHabilidadesTests
{
    [Fact]
    public void AgrupaLasHabilidadesEnConjuntosConLaEvidencia()
    {
        var datos = new DatosCandidato
        {
            Habilidades = ["Responsable", "puntual", "Trabajo en equipo", "Excel intermedio", "Montacargas", "Inglés A2", "Cargue y descargue"],
            Cursos = ["Licencia de conducción C1", "Curso de alturas, SENA"],
        };

        var g = ConjuntosHabilidades.Agrupar(datos).ToDictionary(x => x.Grupo);

        g[GrupoHabilidad.Blandas].Habilidades.Select(h => h.Nombre).Should().Equal("Responsabilidad", "Puntualidad", "Trabajo en equipo");
        g[GrupoHabilidad.Blandas].Habilidades[1].Menciona.Should().Be("puntual");
        g[GrupoHabilidad.Herramientas].Habilidades.Should().ContainSingle(h => h.Nombre == "Excel");
        g[GrupoHabilidad.Idiomas].Habilidades.Should().ContainSingle(h => h.Nombre == "Inglés A2");
        g[GrupoHabilidad.Certificaciones].Habilidades.Should().HaveCount(2);
        g[GrupoHabilidad.Oficio].Habilidades.Select(h => h.Nombre).Should().Equal("Operación de montacargas", "Cargue y descargue");
        ConjuntosHabilidades.Tecnicas(datos).Should().NotContain("Responsabilidad");
    }

    [Fact]
    public void LasPalabrasSeComparanCompletas() =>
        ConjuntosHabilidades.Clasificar("Posgrado en logística").Grupo.Should().Be(GrupoHabilidad.Oficio, "«pos» de punto de venta no es «posgrado»");

    [Fact]
    public void LaDuracionDeCadaExperienciaYSiSigue()
    {
        var hoy = new DateOnly(2026, 10, 22);

        RevisionHojaDeVida.Duracion(new ExperienciaLaboral { Desde = "03/2024", Hasta = "hoy" }, hoy).Should().Be((31, true));
        RevisionHojaDeVida.Duracion(new ExperienciaLaboral { Desde = "", Hasta = "2020" }, hoy).Should().Be(((int?)null, false));
    }
}
