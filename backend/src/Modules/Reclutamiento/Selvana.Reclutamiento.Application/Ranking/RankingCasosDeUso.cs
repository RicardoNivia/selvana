using FluentValidation;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Ranking;

/// <summary>Lo que viaja al agente de ranking: la vacante y el perfil sin nada que identifique al candidato ni variables protegidas.</summary>
public static class PerfilParaRanking
{
    public static RequisitosVacante Vacante(Vacante v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return new RequisitosVacante(v.Titulo, v.Cargo, v.Ciudad, v.Jornada, v.Descripcion, v.Requisitos, v.Competencias);
    }

    /// <summary>Sin nombre, documento, celular, correo ni LinkedIn: el agente no sabe quién es.</summary>
    public static PerfilAnonimo Anonimo(DatosCandidato d, Postulacion p)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(p);
        return new PerfilAnonimo(d.Ciudad,
            [.. d.Experiencia.Select(e => new ExperienciaAnonima(e.Cargo, e.Empresa, e.Desde, e.Hasta))],
            [.. d.Formacion.Select(f => string.IsNullOrWhiteSpace(f.Institucion) ? f.Titulo : $"{f.Titulo} · {f.Institucion}")],
            d.Cursos, d.Habilidades, [.. p.Respuestas.Select(r => new RespuestaFiltroAnonima(r.Pregunta, r.Respuesta))]);
    }

    public static AfinidadSolicitada Solicitud(Vacante v, Candidato c, Postulacion p, DateTimeOffset ahora, IReadOnlyList<CriterioEmpresa>? calibracion = null) =>
        new(Guid.CreateVersion7(), ahora, v.EmpresaId, p.Id, v.Id, Vacante(v), Anonimo(c.Datos, p), calibracion);
}

/// <summary>Califica con IA las postulaciones de la vacante que no tienen puntaje (o todas, menos las que ajustó una persona).</summary>
public sealed record CalcularAfinidadVacanteCommand(Guid VacanteId, bool Todas) : ICommand<int>;

/// <summary>Una persona corrige el puntaje de la IA con su motivo.</summary>
public sealed record AjustarAfinidadCommand(Guid PostulacionId, int Puntaje, string Motivo) : ICommand;

internal sealed class AjustarAfinidadValidator : AbstractValidator<AjustarAfinidadCommand>
{
    public AjustarAfinidadValidator()
    {
        RuleFor(c => c.Puntaje).InclusiveBetween(0, 100).WithMessage("El puntaje va de 0 a 100.");
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Diga por qué ajusta el puntaje: queda en la auditoría y así la IA aprende.").MaximumLength(500);
    }
}

internal sealed class CalcularAfinidadVacanteHandler(
    IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, ICalibracionRanking calibracion, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<CalcularAfinidadVacanteCommand, int>
{
    /// <summary>Tope por pedido para cuidar el presupuesto de IA.</summary>
    private const int Maximo = 200;

    public async Task<Result<int>> Handle(CalcularAfinidadVacanteCommand c, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        var ahora = reloj.GetUtcNow();
        var pedidas = 0;
        var criterios = await calibracion.CriteriosAsync(vacante.EmpresaId, vacante.Cargo, vacante.Id, cancellationToken);
        foreach (var candidato in await candidatos.ConPostulacionesAAsync(vacante.EmpresaId, vacante.Id, cancellationToken))
        {
            foreach (var p in candidato.Postulaciones.Where(p => p.VacanteId == vacante.Id && Pendiente(p.Afinidad, c.Todas)))
            {
                if (pedidas >= Maximo || candidato.Suprimido)
                {
                    break;
                }

                candidato.PedirAfinidad(p.Id);
                eventos.Publicar(PerfilParaRanking.Solicitud(vacante, candidato, p, ahora, criterios));
                pedidas++;
            }
        }

        if (pedidas > 0)
        {
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Pidió a la IA calificar la afinidad en la vacante {vacante.Codigo}",
                $"{pedidas} postulaciones · {criterios.Count} decisiones de la empresa para calibrar",
                c.Todas ? "Recalcula todas, menos las que ajustó una persona" : "Solo las que no tenían puntaje", "Vacante", vacante.Id);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return pedidas;
    }

    private static bool Pendiente(AfinidadPostulacion? a, bool todas) => a is null || a.Estado switch
    {
        EstadoAfinidad.Calculando => false,
        EstadoAfinidad.SinCalcular or EstadoAfinidad.Fallo => true,
        _ => todas && a.PuntajeAjustado is null,
    };
}

internal sealed class AjustarAfinidadHandler(IUsuarioActual usuario, ICandidatos candidatos, IVacantes vacantes, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<AjustarAfinidadCommand>
{
    public async Task<Result> Handle(AjustarAfinidadCommand c, CancellationToken cancellationToken)
    {
        var candidato = await candidatos.ObtenerPorPostulacionAsync(usuario.EmpresaId, c.PostulacionId, cancellationToken);
        var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == c.PostulacionId);
        var vacante = postulacion is null ? null : await vacantes.ObtenerAsync(postulacion.VacanteId, cancellationToken);
        if (candidato is null || vacante is null || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(AfinidadErrors.NoExiste);
        }

        var antes = postulacion!.Afinidad?.Puntaje;
        var r = candidato.AjustarAfinidad(c.PostulacionId, c.Puntaje, c.Motivo, usuario.Nombre, reloj.GetUtcNow());
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Ajustó la afinidad de la postulación {postulacion.Numero}",
            $"{antes} → {c.Puntaje} · IA {postulacion.Afinidad!.PuntajeIa} ({postulacion.Afinidad.Modelo})", $"Motivo: {c.Motivo.Trim()}", "Postulacion", postulacion.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>El agente de ranking respondió: el puntaje y sus razones quedan en la postulación como sugerencia (violeta) y en la bitácora.</summary>
internal sealed class AfinidadCalculadaConsumer(ICandidatos candidatos, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<AfinidadCalculada>
{
    public async Task Handle(AfinidadCalculada e, CancellationToken cancellationToken)
    {
        var candidato = await candidatos.ObtenerPorPostulacionAsync(e.EmpresaId, e.PostulacionId, cancellationToken);
        if (candidato is null || candidato.Suprimido)
        {
            return;
        }

        if (e.Error is not null)
        {
            var falla = candidato.RegistrarFallaAfinidad(e.PostulacionId, e.Error);
            bitacora.Registrar(e.EmpresaId, TipoActor.Ia, "Agente de ranking", "No pudo calificar una postulación", e.Error, "Se puede recalcular desde la vacante",
                "Postulacion", e.PostulacionId);
            if (falla.IsSuccess)
            {
                await unidad.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var r = candidato.RegistrarAfinidad(e.PostulacionId, e.Puntaje,
            e.Razones.Select(x => new RazonPuntaje { Signo = x.Signo, Texto = x.Texto, Evidencia = x.Evidencia }), e.RequisitosCumplidos, e.RequisitosFaltantes,
            e.Modelo, reloj.GetUtcNow(), e.RazonesRetiradas);
        if (r.IsFailure)
        {
            return;
        }

        bitacora.Registrar(e.EmpresaId, TipoActor.Ia, "Agente de ranking", $"Calificó la afinidad de la postulación {r.Value.Numero}: {e.Puntaje}/100",
            $"{e.Razones.Count} razones con evidencia · {e.RequisitosFaltantes.Count} requisitos sin demostrar · {e.Modelo}",
            "Sugerencia: no usó edad, género, estado civil, foto ni dirección. Una persona decide", "Postulacion", e.PostulacionId);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}
