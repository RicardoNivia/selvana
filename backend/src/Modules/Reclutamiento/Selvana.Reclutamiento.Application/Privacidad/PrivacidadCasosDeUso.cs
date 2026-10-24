using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Privacidad;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Privacidad;

public sealed record ArchivoDto(string Nombre, string TipoContenido, byte[] Contenido);

public sealed record AutorizacionDto(
    Guid PostulacionId, string Titular, string Documento, string Canal, string Version, DateTimeOffset AceptadaEn, string Estado, string Huella, string Vacante);

public sealed record ResumenAutorizacionesDto(int Vigentes, int RevocadasEsteAnio, string VersionVigente);

public static class PrivacidadErrors
{
    public static readonly Error SinHojaDeVida = Error.NotFound("HojaDeVida.NoExiste", "Esta postulación no tiene hoja de vida guardada (puede que el titular haya pedido suprimirla).");
}

/// <summary>
/// Abre la hoja de vida de una postulación. Es un dato personal: cada apertura queda en la bitácora con quién, cuándo y
/// para qué. Solo la ve quien ve la empresa usuaria de la vacante.
/// </summary>
public sealed record AbrirHojaDeVidaCommand(Guid PostulacionId, string Motivo) : ICommand<ArchivoDto>;

internal sealed class AbrirHojaDeVidaValidator : AbstractValidator<AbrirHojaDeVidaCommand>
{
    public AbrirHojaDeVidaValidator() =>
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Diga para qué abre la hoja de vida.").MaximumLength(200);
}

internal sealed class AbrirHojaDeVidaHandler(
    IUsuarioActual usuario, ICandidatos candidatos, IVacantes vacantes, IAlmacenHojasDeVida almacen, IBitacora bitacora, IUnidadReclutamiento unidad)
    : ICommandHandler<AbrirHojaDeVidaCommand, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(AbrirHojaDeVidaCommand c, CancellationToken cancellationToken)
    {
        var candidato = await candidatos.ObtenerPorPostulacionAsync(usuario.EmpresaId, c.PostulacionId, cancellationToken);
        var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == c.PostulacionId);
        if (candidato is null || postulacion is null)
        {
            return PrivacidadErrors.SinHojaDeVida;
        }

        var vacante = await vacantes.ObtenerAsync(postulacion.VacanteId, cancellationToken);
        if (vacante is null || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        if (postulacion.ArchivoHojaDeVidaId == Guid.Empty || await almacen.LeerAsync(postulacion.ArchivoHojaDeVidaId, cancellationToken) is not { } archivo)
        {
            return PrivacidadErrors.SinHojaDeVida;
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Abrió la hoja de vida de {candidato.Datos.Nombre}",
            $"{postulacion.Numero} · vacante {vacante.Codigo} · motivo: {c.Motivo.Trim()}", "Dato personal · acceso permitido por su rol",
            "HojaDeVida", postulacion.ArchivoHojaDeVidaId, datoSensible: true);
        await unidad.SaveChangesAsync(cancellationToken);
        return new ArchivoDto(archivo.Nombre, archivo.TipoContenido, archivo.Contenido);
    }
}

/// <summary>Registro de autorizaciones de la Ley 1581: una por postulación, con su versión y su huella.</summary>
public sealed record ListarAutorizacionesQuery(int? Pagina, int? Tamano, string? Buscar, string? Estado) : IQuery<Pagina<AutorizacionDto>>;

internal sealed class ListarAutorizacionesHandler(IUsuarioActual usuario, ILecturaReclutamiento lectura) : IQueryHandler<ListarAutorizacionesQuery, Pagina<AutorizacionDto>>
{
    public async Task<Result<Pagina<AutorizacionDto>>> Handle(ListarAutorizacionesQuery q, CancellationToken ct) =>
        await lectura.AutorizacionesAsync(usuario.EmpresaId, new PaginaSolicitada(q.Pagina, q.Tamano), q.Buscar, q.Estado, ct);
}

public sealed record ObtenerResumenAutorizacionesQuery : IQuery<ResumenAutorizacionesDto>;

internal sealed class ObtenerResumenAutorizacionesHandler(IUsuarioActual usuario, ILecturaReclutamiento lectura, TimeProvider reloj)
    : IQueryHandler<ObtenerResumenAutorizacionesQuery, ResumenAutorizacionesDto>
{
    public async Task<Result<ResumenAutorizacionesDto>> Handle(ObtenerResumenAutorizacionesQuery q, CancellationToken ct)
    {
        var (vigentes, revocadas) = await lectura.ConteoAutorizacionesAsync(usuario.EmpresaId, reloj.GetUtcNow().Year, ct);
        return new ResumenAutorizacionesDto(vigentes, revocadas, PoliticaDatos.Version);
    }
}

/// <summary>La empresa aprobó suprimir los datos de un titular: se borran sus hojas de vida y sus datos, queda la constancia.</summary>
internal sealed class SupresionDeTitularAprobadaConsumer(ICandidatos candidatos, IAlmacenHojasDeVida almacen, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<SupresionDeTitularAprobada>
{
    public async Task Handle(SupresionDeTitularAprobada e, CancellationToken cancellationToken)
    {
        var candidato = await candidatos.ObtenerPorDocumentoAsync(e.EmpresaId, Domain.Candidatos.Candidato.NormalizarDocumento(e.Documento), cancellationToken);
        if (candidato is null || candidato.Suprimido)
        {
            return;
        }

        var nombre = candidato.Datos.Nombre;
        var postulaciones = candidato.Postulaciones.Count;
        var archivos = candidato.Suprimir(reloj.GetUtcNow());
        await almacen.EliminarAsync(archivos, cancellationToken);
        bitacora.Registrar(e.EmpresaId, TipoActor.Sistema, "Selvana", $"Suprimió los datos de {nombre} en Reclutamiento",
            $"{archivos.Count} hojas de vida borradas · {postulaciones} postulaciones sin datos personales · queda la constancia de la revocatoria",
            $"Aprobado por {e.AprobadaPor}", "Candidato", candidato.Id, datoSensible: true);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}
