using FluentValidation;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Preseleccion;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.Reclutamiento.Domain.Preseleccion;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Portal;

/// <summary>Un mensaje del chat de preselección como lo ve la persona: el asistente, ella («Tú») o la persona de la empresa.</summary>
public sealed record MensajeChatDto(string Autor, bool EsMio, string Texto, DateTimeOffset En);

public sealed record ChatDePostulacionDto(bool Abierto, IReadOnlyList<MensajeChatDto> Mensajes);

/// <summary>El historial del chat de preselección de una de sus postulaciones (solo si es suya).</summary>
public sealed record ObtenerChatDePostulacionQuery(Guid PostulacionId) : IQuery<ChatDePostulacionDto>;

internal sealed class ObtenerChatDePostulacionHandler(IPersonaActual persona, ICuentasCandidato cuentas, ILecturaPortalPersona lectura, IConversaciones conversaciones)
    : IQueryHandler<ObtenerChatDePostulacionQuery, ChatDePostulacionDto>
{
    public async Task<Result<ChatDePostulacionDto>> Handle(ObtenerChatDePostulacionQuery q, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return CuentaErrors.NoExiste;
        }

        var postulacion = (await lectura.PostulacionesAsync(cuenta.Documento, cuenta.Correo, cancellationToken)).FirstOrDefault(p => p.PostulacionId == q.PostulacionId);
        var conversacion = postulacion is null ? null : await conversaciones.PorPostulacionAsync(postulacion.EmpresaId, postulacion.PostulacionId, cancellationToken);
        if (conversacion is null)
        {
            return new ChatDePostulacionDto(false, []);
        }

        return new ChatDePostulacionDto(conversacion.Activa, [.. conversacion.Mensajes.Select(m => new MensajeChatDto(
            m.Autor switch
            {
                AutorMensaje.Candidato => "Tú",
                AutorMensaje.Persona => m.Por ?? postulacion!.Empresa,
                _ => "Asistente de selección",
            },
            m.Autor == AutorMensaje.Candidato, m.Texto, m.En))]);
    }
}

/// <summary>Su hoja de vida escrita o corregida a mano: datos, experiencia, estudios, cursos y habilidades (reemplaza lo que había).</summary>
public sealed record PerfilEditable(
    DatosEditables Datos, IReadOnlyList<ExperienciaDto> Experiencia, IReadOnlyList<FormacionDto> Formacion, IReadOnlyList<string> Cursos, IReadOnlyList<string> Habilidades);

public sealed record ActualizarMiPerfilCommand(PerfilEditable Perfil) : ICommand;

internal sealed class ActualizarMiPerfilValidator : AbstractValidator<ActualizarMiPerfilCommand>
{
    public ActualizarMiPerfilValidator()
    {
        RuleFor(c => c.Perfil.Datos).SetValidator(new DatosEditablesValidator());
        RuleFor(c => c.Perfil.Experiencia).Must(e => e.Count <= 30).WithMessage("Máximo 30 experiencias.");
        RuleForEach(c => c.Perfil.Experiencia).Must(e => !string.IsNullOrWhiteSpace(e.Cargo)).WithMessage("Cada experiencia necesita el cargo.");
        RuleFor(c => c.Perfil.Formacion).Must(f => f.Count <= 20).WithMessage("Máximo 20 estudios.");
        RuleForEach(c => c.Perfil.Formacion).Must(f => !string.IsNullOrWhiteSpace(f.Titulo)).WithMessage("Cada estudio necesita el título.");
        RuleFor(c => c.Perfil.Cursos).Must(x => x.Count <= 40).WithMessage("Máximo 40 cursos.");
        RuleFor(c => c.Perfil.Habilidades).Must(x => x.Count <= 40).WithMessage("Máximo 40 habilidades.");
    }
}

internal sealed class ActualizarMiPerfilHandler(IPersonaActual persona, ICuentasCandidato cuentas, IUnidadReclutamiento unidad)
    : ICommandHandler<ActualizarMiPerfilCommand>
{
    public async Task<Result> Handle(ActualizarMiPerfilCommand c, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return Result.Failure(CuentaErrors.NoExiste);
        }

        var p = c.Perfil;
        cuenta.ActualizarDatos(new Domain.Candidatos.DatosCandidato
        {
            Nombre = p.Datos.Nombre,
            Celular = p.Datos.Celular,
            Ciudad = p.Datos.Ciudad,
            LinkedIn = p.Datos.LinkedIn ?? "",
            Experiencia = [.. p.Experiencia.Select(e => new Domain.Candidatos.ExperienciaLaboral { Cargo = L(e.Cargo), Empresa = L(e.Empresa), Desde = L(e.Desde), Hasta = L(e.Hasta) })],
            Formacion = [.. p.Formacion.Select(f => new Domain.Candidatos.EstudioRealizado { Titulo = L(f.Titulo), Institucion = L(f.Institucion), Anio = L(f.Anio) })],
            Cursos = [.. p.Cursos.Select(L).Where(x => x.Length > 0).Distinct()],
            Habilidades = [.. p.Habilidades.Select(L).Where(x => x.Length > 0).Distinct()],
        });
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static string L(string? s) => (s ?? "").Trim();
}
