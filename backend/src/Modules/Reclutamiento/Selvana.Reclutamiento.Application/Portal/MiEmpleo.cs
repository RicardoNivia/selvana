using Selvana.Clima.Contracts;
using Selvana.Desempeno.Contracts;
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

/* Compartir la hoja de vida del portal con su empresa (para su expediente); sin esto, la empresa no la ve */

public sealed record EmpresasConMiHojaDeVidaQuery : IQuery<IReadOnlyList<Guid>>;

public sealed record CompartirMiHojaDeVidaCommand(Guid EmpresaId, bool Compartir) : ICommand;

internal sealed class EmpresasConMiHojaDeVidaHandler(IPersonaActual persona, ICuentasCandidato cuentas) : IQueryHandler<EmpresasConMiHojaDeVidaQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(EmpresasConMiHojaDeVidaQuery query, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        return r.IsFailure ? r.Error : r.Value.HojaDeVidaCompartidaCon;
    }
}

internal sealed class CompartirMiHojaDeVidaHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEmpleoDeLaPersona empleos, IBitacora bitacora,
    IUnidadReclutamiento unidad) : ICommandHandler<CompartirMiHojaDeVidaCommand>
{
    public async Task<Result> Handle(CompartirMiHojaDeVidaCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        // Solo con una empresa donde la persona trabaja (o trabajó)
        var cuenta = r.Value;
        if (!(await empleos.ListarAsync(cuenta.Documento, cuenta.Correo, cancellationToken)).Any(e => e.EmpresaId == c.EmpresaId))
        {
            return Result.Failure(Error.NotFound("Portal.SinEmpleo", "No encontramos tu empleo en esa empresa."));
        }

        cuenta.CompartirHojaDeVida(c.EmpresaId, c.Compartir);
        bitacora.Registrar(c.EmpresaId, Core.Contracts.Auditoria.TipoActor.Titular, cuenta.Datos.Nombre,
            c.Compartir ? "Compartió su hoja de vida del portal con la empresa" : "Dejó de compartir su hoja de vida del portal",
            cuenta.Documento, "Autorización de la persona desde su portal", "CuentaPortal", cuenta.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/* Desempeño y clima desde el portal del empleado: su autoevaluación (180° y 360°) y sus pulsos de ingreso */

public sealed record PendientesEmpleoDto(IReadOnlyList<AutoevaluacionDeLaPersona> Autoevaluaciones, IReadOnlyList<PulsoDeLaPersona> Pulsos);

public sealed record ObtenerPendientesEmpleoQuery : IQuery<PendientesEmpleoDto>;

public sealed record AutoevaluarmeCommand(Guid EvaluacionId, IReadOnlyDictionary<string, int> Calificaciones, string Comentario) : ICommand;

public sealed record ResponderMiPulsoCommand(Guid PulsoId, RespuestaPulsoDeLaPersona Respuesta) : ICommand;

internal sealed class ObtenerPendientesEmpleoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEvaluacionesDeLaPersona evaluaciones, IPulsosDeLaPersona pulsos)
    : IQueryHandler<ObtenerPendientesEmpleoQuery, PendientesEmpleoDto>
{
    public async Task<Result<PendientesEmpleoDto>> Handle(ObtenerPendientesEmpleoQuery query, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        return new PendientesEmpleoDto(await evaluaciones.ListarAsync(r.Value.Documento, r.Value.Correo, cancellationToken),
            await pulsos.ListarAsync(r.Value.Documento, r.Value.Correo, cancellationToken));
    }
}

internal sealed class AutoevaluarmeHandler(IPersonaActual persona, ICuentasCandidato cuentas, IEvaluacionesDeLaPersona evaluaciones) : ICommandHandler<AutoevaluarmeCommand>
{
    public async Task<Result> Handle(AutoevaluarmeCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        return r.IsFailure
            ? Result.Failure(r.Error)
            : await evaluaciones.AutoevaluarAsync(c.EvaluacionId, r.Value.Documento, r.Value.Correo, c.Calificaciones, c.Comentario ?? "", cancellationToken);
    }
}

internal sealed class ResponderMiPulsoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IPulsosDeLaPersona pulsos) : ICommandHandler<ResponderMiPulsoCommand>
{
    public async Task<Result> Handle(ResponderMiPulsoCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        return r.IsFailure ? Result.Failure(r.Error) : await pulsos.ResponderAsync(c.PulsoId, r.Value.Documento, r.Value.Correo, c.Respuesta, cancellationToken);
    }
}
