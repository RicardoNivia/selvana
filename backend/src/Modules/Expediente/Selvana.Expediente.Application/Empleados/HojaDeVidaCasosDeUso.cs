using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Application.Empleados;

/// <summary>La hoja de vida del empleado en su ficha; <see cref="Hoja"/> es null si la empresa no tiene ninguna de esa persona.</summary>
public sealed record HojaDeVidaEmpleadoDto(HojaDeVidaParaEmpresa? Hoja);

/// <summary>Su hoja de vida: la de su postulación en la empresa o la del portal si la compartió. No abre el archivo (eso queda en la bitácora aparte).</summary>
public sealed record ObtenerHojaDeVidaEmpleadoQuery(Guid Id) : IQuery<HojaDeVidaEmpleadoDto>;

/// <summary>Abre el archivo de la hoja de vida que el empleado compartió desde su portal. Pide el motivo: queda en la bitácora.</summary>
public sealed record AbrirHojaDeVidaCompartidaCommand(Guid Id, string Motivo) : ICommand<ArchivoDto>;

internal sealed class AbrirHojaDeVidaCompartidaValidator : AbstractValidator<AbrirHojaDeVidaCompartidaCommand>
{
    public AbrirHojaDeVidaCompartidaValidator() => RuleFor(c => c.Motivo).NotEmpty().WithMessage("Diga para qué abre la hoja de vida.").MaximumLength(200);
}

internal sealed class ObtenerHojaDeVidaEmpleadoHandler(IUsuarioActual usuario, IEmpleados empleados, IHojasDeVidaParaEmpresa hojas)
    : IQueryHandler<ObtenerHojaDeVidaEmpleadoQuery, HojaDeVidaEmpleadoDto>
{
    public async Task<Result<HojaDeVidaEmpleadoDto>> Handle(ObtenerHojaDeVidaEmpleadoQuery q, CancellationToken ct)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, q.Id, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        return new HojaDeVidaEmpleadoDto(await hojas.ObtenerAsync(r.Value.EmpresaId, r.Value.Documento, r.Value.Correo, ct));
    }
}

internal sealed class AbrirHojaDeVidaCompartidaHandler(IUsuarioActual usuario, IEmpleados empleados, IHojasDeVidaParaEmpresa hojas, IBitacora bitacora,
    IUnidadExpediente unidad) : ICommandHandler<AbrirHojaDeVidaCompartidaCommand, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(AbrirHojaDeVidaCompartidaCommand c, CancellationToken ct)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.Id, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var e = r.Value;
        if (await hojas.AbrirCompartidaAsync(e.EmpresaId, e.Documento, e.Correo, ct) is not { } a)
        {
            return Error.NotFound("Expediente.SinHojaDeVida", "La persona no ha compartido una hoja de vida en su portal.");
        }

        bitacora.Registrar(e.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Abrió la hoja de vida de {e.Nombre}", $"{e.Codigo} · compartida desde su portal",
            c.Motivo.Trim(), "Empleado", e.Id, datoSensible: true);
        await unidad.SaveChangesAsync(ct);
        return new ArchivoDto(a.Nombre, a.TipoContenido, a.Contenido);
    }
}
