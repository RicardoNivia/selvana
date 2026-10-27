using Selvana.AI.Contracts.Reclutamiento;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Reclutamiento.Application.RespuestasIa;

/// <summary>El redactor respondió: la vacante queda como borrador (violeta) para que una persona la revise y publique.</summary>
internal sealed class BorradorVacanteRedactadoConsumer(
    IVacantes vacantes, IBateriasCargo baterias, IBitacora bitacora, IEventosReclutamiento eventos, IUnidadReclutamiento unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<BorradorVacanteRedactado>
{
    public async Task Handle(BorradorVacanteRedactado e, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(e.VacanteId, cancellationToken);
        if (vacante is null)
        {
            return;
        }

        if (e.Error is not null)
        {
            vacante.RegistrarFallaIa(e.Error);
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Ia, "Agente redactor de vacantes", $"No pudo redactar la vacante {vacante.Codigo}", e.Error,
                "Queda un borrador vacío para que una persona lo llene", "Vacante", vacante.Id);
        }
        else if (e.Aclaraciones is { Count: > 0 } aclaraciones)
        {
            vacante.RegistrarAclaraciones(aclaraciones);
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Ia, "Agente redactor de vacantes", $"Pidió aclarar la vacante {vacante.Codigo} antes de redactarla",
                string.Join(" · ", aclaraciones), "Espera que una persona responda", "Vacante", vacante.Id);
        }
        else
        {
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Ia, "Agente redactor de vacantes", $"Redactó la vacante {vacante.Codigo}",
                $"{e.Titulo} · {e.Razones.Count} razones con fuente · {e.Modelo}", "Espera revisión y publicación de una persona", "Vacante", vacante.Id);
            var pesos = new System.Globalization.CultureInfo("es-CO");
            eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), reloj.GetUtcNow(), vacante.EmpresaId, vacante.EmpresaUsuariaId, "Reclutamiento",
                "VacanteRedactada", vacante.Id, $"Vacante redactada · {e.Titulo}",
                $"{e.Ciudad} · {e.Cupos} {(e.Cupos == 1 ? "cupo" : "cupos")} · creada a partir de «{vacante.Frase}»", "Agente redactor de vacantes", e.Modelo,
                [.. e.Razones.Take(5).Select(r => new RazonPropuesta(r.Signo, r.Texto, r.Fuente))],
                [new CampoPropuesta("Título del aviso", e.Titulo),
                 new CampoPropuesta("Rango salarial", e.SalarioMaximo > e.SalarioMinimo ? $"{e.SalarioMinimo.ToString("C0", pesos)} – {e.SalarioMaximo.ToString("C0", pesos)}" : e.SalarioMinimo.ToString("C0", pesos)),
                 new CampoPropuesta("Por qué ese salario", e.JustificacionSalario),
                 new CampoPropuesta("Preguntas filtro", string.Join(" · ", e.PreguntasFiltro.Take(3)))],
                new AccionPropuesta("Enviar a aprobación", "POST", $"/api/v1/reclutamiento/vacantes/{vacante.Id}/solicitar-aprobacion"), $"/consola/vacantes/{vacante.Id}", false, null));
            var bateria = await baterias.ObtenerAsync(vacante.EmpresaId, Baterias.ClaveCargo(e.Cargo), cancellationToken);
            vacante.ConfigurarPruebas(bateria?.Pruebas ?? Baterias.Sugerir(e.Cargo).Pruebas);
            vacante.RegistrarBorradorIa(
                new ContenidoVacante(e.Titulo, e.Cargo, e.Ciudad, e.Cupos, e.Jornada, e.Descripcion, e.SalarioMinimo, e.SalarioMaximo, e.Requisitos, e.PreguntasFiltro),
                e.JustificacionSalario, e.Competencias, e.Supuestos,
                e.Razones.Select(r => new RazonVacante { Signo = r.Signo, Texto = r.Texto, Fuente = r.Fuente }), e.Modelo);
        }

        await unidad.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>El lector respondió: el candidato ve sus datos para confirmar, o un mensaje si no se pudo leer.</summary>
internal sealed class HojaDeVidaLeidaConsumer(IBorradoresPostulacion borradores, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<HojaDeVidaLeida>
{
    public async Task Handle(HojaDeVidaLeida e, CancellationToken cancellationToken)
    {
        var borrador = await borradores.ObtenerAsync(e.BorradorId, cancellationToken);
        if (borrador is null)
        {
            return;
        }

        if (e.Datos is null)
        {
            borrador.RegistrarFalla(e.Error ?? "No pudimos leer tu hoja de vida. Escribe tus datos.", reloj.GetUtcNow());
            bitacora.Registrar(borrador.EmpresaId, TipoActor.Ia, "Lector de hojas de vida", "No pudo leer una hoja de vida", borrador.NombreArchivo,
                "El candidato escribe sus datos", "HojaDeVida", borrador.ArchivoId);
        }
        else
        {
            var d = e.Datos;
            borrador.RegistrarLectura(new DatosCandidato
            {
                Nombre = d.Nombre,
                Documento = d.Documento,
                Celular = d.Celular,
                Correo = d.Correo,
                Ciudad = d.Ciudad,
                Experiencia = [.. d.Experiencia.Select(x => new ExperienciaLaboral { Cargo = x.Cargo, Empresa = x.Empresa, Desde = x.Desde, Hasta = x.Hasta })],
                Formacion = [.. d.Formacion.Select(x => new EstudioRealizado { Titulo = x.Titulo, Institucion = x.Institucion, Anio = x.Anio })],
                Cursos = [.. d.Cursos],
                Habilidades = [.. d.Habilidades],
                LinkedIn = Candidato.NormalizarLinkedIn(d.LinkedIn),
            }, e.Modelo, reloj.GetUtcNow());
            bitacora.Registrar(borrador.EmpresaId, TipoActor.Ia, "Lector de hojas de vida", "Leyó una hoja de vida para una postulación",
                $"{d.Experiencia.Count} experiencias · {d.Formacion.Count} estudios · {e.Modelo} (modelo de pago: hay datos personales)",
                "El candidato confirma o corrige antes de enviar", "HojaDeVida", borrador.ArchivoId);
        }

        await unidad.SaveChangesAsync(cancellationToken);
    }
}
