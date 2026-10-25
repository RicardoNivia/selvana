using FluentAssertions;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Recontacto;
using Selvana.Reclutamiento.Domain.Vacantes;

namespace Selvana.Reclutamiento.Domain.Tests;

public sealed class RecontactoBancoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora.UtcDateTime);

    private static AutorizacionDatos Autorizacion(string texto) => new() { Version = "v3", Texto = texto, AceptadaEn = Ahora, Ip = "127.0.0.1", Navegador = "Pruebas" };

    private const string TextoV3 = "Autorizo a Servicios Andinos EST y a Selvana a tratar mis datos personales para este proceso de selección y para ofrecerme " +
        "vacantes similares, según la Ley 1581 de 2012.";

    private static Vacante Vacante(params string[] requisitos)
    {
        var v = Domain.Vacantes.Vacante.Solicitar(Guid.NewGuid(), "Servicios Andinos EST", null, null, "Necesito auxiliares de bodega en Funza", "laura", Ahora).Value;
        v.RegistrarBorradorIa(new ContenidoVacante("Auxiliar de bodega", "AUXILIAR DE BODEGA", "Funza", 5, "Turno noche", "Cargue y descargue.", 1_750_905, 1_900_000,
            requisitos, []), "Mínimo legal", [], [], [], "gemini-3.5-flash");
        return v;
    }

    private static Candidato Candidato(string ciudad = "Funza", string cargo = "Bodeguero", params string[] habilidades) =>
        Domain.Candidatos.Candidato.Registrar(Guid.NewGuid(), new DatosCandidato
        {
            Nombre = "Andrés Felipe Salcedo", Documento = "1020456789", Ciudad = ciudad,
            Experiencia = [new ExperienciaLaboral { Cargo = cargo, Empresa = "Logística del Norte", Desde = "2022-01", Hasta = "2025-06" }],
            Habilidades = [.. habilidades],
        }, Ahora);

    [Fact]
    public void SeRecontactaASoloQuienAutorizoVacantesSimilares()
    {
        var conV3 = Candidato();
        conV3.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], Autorizacion(TextoV3), Guid.NewGuid(), [], Ahora);
        var soloEsteProceso = Candidato();
        soloEsteProceso.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], Autorizacion("Autorizo el tratamiento de mis datos para este proceso."), Guid.NewGuid(), [], Ahora);

        RecontactoBanco.PuedeRecontactarse(conV3, Guid.NewGuid()).Should().BeTrue();
        RecontactoBanco.PuedeRecontactarse(soloEsteProceso, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void NoSeRecontactaAlSuprimidoNiAQuienYaSePostuloALaVacante()
    {
        var vacanteId = Guid.NewGuid();
        var postulado = Candidato();
        postulado.Postularse(vacanteId, CanalPostulacion.Portal, [], Autorizacion(TextoV3), Guid.NewGuid(), [], Ahora);
        var suprimido = Candidato();
        suprimido.Postularse(Guid.NewGuid(), CanalPostulacion.Portal, [], Autorizacion(TextoV3), Guid.NewGuid(), [], Ahora);
        suprimido.Suprimir(Ahora);

        RecontactoBanco.PuedeRecontactarse(postulado, vacanteId).Should().BeFalse();
        RecontactoBanco.PuedeRecontactarse(suprimido, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void ElCargoNormalizadoLaCiudadYLosRequisitosSumanConEvidencia()
    {
        var e = RecontactoBanco.Evaluar(Vacante("Licencia de conducción C1", "2 años de experiencia"), Candidato("Funza", "Bodeguero", "Pase C1").Datos, Hoy);

        e.Encaja.Should().BeTrue();
        e.Puntaje.Should().Be(90);
        e.Coincidencias.Should().Contain(c => c.Tipo == RecontactoBanco.TipoCargo && c.Evidencia == "HV · experiencia 1");
        e.Coincidencias.Should().Contain(c => c.Tipo == RecontactoBanco.TipoCiudad);
        e.Coincidencias.Where(c => c.Tipo == RecontactoBanco.TipoRequisito).Should().HaveCount(2);
    }

    [Fact]
    public void LaCiudadSolaNoBasta()
    {
        var e = RecontactoBanco.Evaluar(Vacante(), Candidato("Funza", "Cajero").Datos, Hoy);

        e.Encaja.Should().BeFalse();
    }

    [Fact]
    public void InvitarYDescartarLosDecideUnaPersonaUnaSolaVez()
    {
        var s = SugerenciaBanco.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EvaluacionBanco(70, []), Ahora);

        s.Invitar(true, "laura", Ahora).IsSuccess.Should().BeTrue();
        s.Estado.Should().Be(EstadoSugerenciaBanco.Invitada);
        s.DecididaPor.Should().Be("laura");
        s.DescartarSugerencia("No tiene turno noche", "laura", Ahora).Error.Should().Be(SugerenciaBancoErrors.YaDecidida);
    }

    [Fact]
    public void NoSeInvitaAQuienYaNoAutoriza()
    {
        var s = SugerenciaBanco.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EvaluacionBanco(70, []), Ahora);

        s.Invitar(false, "laura", Ahora).Error.Should().Be(SugerenciaBancoErrors.NoRecontactable);
        s.Estado.Should().Be(EstadoSugerenciaBanco.Sugerida);
    }

    [Fact]
    public void NoEncajaExigeMotivoSinVariablesProtegidas()
    {
        var s = SugerenciaBanco.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EvaluacionBanco(70, []), Ahora);

        s.DescartarSugerencia(" ", "laura", Ahora).Error.Should().Be(SugerenciaBancoErrors.SinMotivo);
        s.DescartarSugerencia("Es muy mayor para el cargo", "laura", Ahora).Error.Should().Be(RetroalimentacionErrors.MotivoDiscriminatorio);
        s.DescartarSugerencia("No tiene experiencia en cargue", "laura", Ahora).IsSuccess.Should().BeTrue();
        s.Motivo.Should().Be("No tiene experiencia en cargue");
    }
}
