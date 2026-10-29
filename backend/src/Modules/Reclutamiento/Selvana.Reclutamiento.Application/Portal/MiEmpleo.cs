using Selvana.Expediente.Contracts;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Privacidad;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Portal;

/* P5 · Portal del empleado: cuando la persona ya trabaja en una empresa que usa Selvana, su portal muestra su empleo. */

public sealed record ObtenerMiEmpleoQuery : IQuery<IReadOnlyList<EmpleoDeLaPersona>>;

public sealed record ProponerMisDatosCommand(Guid EmpleadoId, DatosPropuestosPorElEmpleado Datos) : ICommand;

public sealed record ConfirmarMisDatosCommand(Guid EmpleadoId) : ICommand;

public sealed record SubirSoporteEmpleoCommand(Guid EmpleadoId, string Tipo, string Nombre, DateOnly? VenceEn, string NombreArchivo, string TipoContenido, byte[] Contenido)
    : ICommand;

public sealed record DescargarDocumentoEmpleoQuery(Guid EmpleadoId, Guid DocumentoId) : IQuery<ArchivoDto>;

internal sealed class ObtenerMiEmpleoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEmpleoDeLaPersona empleos)
    : IQueryHandler<ObtenerMiEmpleoQuery, IReadOnlyList<EmpleoDeLaPersona>>
{
    public async Task<Result<IReadOnlyList<EmpleoDeLaPersona>>> Handle(ObtenerMiEmpleoQuery query, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        return r.IsFailure ? r.Error : Result.Success(await empleos.ListarAsync(r.Value.Documento, r.Value.Correo, cancellationToken));
    }
}

internal sealed class ProponerMisDatosHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEmpleoDeLaPersona empleos) : ICommandHandler<ProponerMisDatosCommand>
{
    public async Task<Result> Handle(ProponerMisDatosCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        return r.IsFailure ? Result.Failure(r.Error) : await empleos.ProponerDatosAsync(c.EmpleadoId, r.Value.Documento, r.Value.Correo, c.Datos, cancellationToken);
    }
}

internal sealed class ConfirmarMisDatosHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEmpleoDeLaPersona empleos) : ICommandHandler<ConfirmarMisDatosCommand>
{
    public async Task<Result> Handle(ConfirmarMisDatosCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        return r.IsFailure ? Result.Failure(r.Error) : await empleos.ConfirmarDatosAsync(c.EmpleadoId, r.Value.Documento, r.Value.Correo, cancellationToken);
    }
}

internal sealed class SubirSoporteEmpleoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEmpleoDeLaPersona empleos) : ICommandHandler<SubirSoporteEmpleoCommand>
{
    public async Task<Result> Handle(SubirSoporteEmpleoCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        return r.IsFailure
            ? Result.Failure(r.Error)
            : await empleos.EntregarSoporteAsync(c.EmpleadoId, r.Value.Documento, r.Value.Correo,
                new SoporteDelEmpleado(c.Tipo, c.Nombre, c.VenceEn, Path.GetFileName(c.NombreArchivo), c.TipoContenido, c.Contenido), cancellationToken);
    }
}

internal sealed class DescargarDocumentoEmpleoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEmpleoDeLaPersona empleos)
    : IQueryHandler<DescargarDocumentoEmpleoQuery, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(DescargarDocumentoEmpleoQuery query, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        return await empleos.DescargarAsync(query.EmpleadoId, r.Value.Documento, r.Value.Correo, query.DocumentoId, cancellationToken) is { } a
            ? new ArchivoDto(a.Nombre, a.TipoContenido, a.Contenido)
            : DocumentosPersonaErrors.NoExiste;
    }
}
