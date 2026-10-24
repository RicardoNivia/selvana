using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura.EditarMaestros;

/// <summary>Edita una sede: nombre visible y si está activa. La sincronización con la nómina no pisa lo editado.</summary>
public sealed record EditarSedeCommand(Guid SedeId, string Nombre, bool Activo) : ICommand;

/// <summary>Edita un área: nombre visible y si está activa.</summary>
public sealed record EditarAreaCommand(Guid AreaId, string Nombre, bool Activo) : ICommand;

/// <summary>Edita un centro de costo: nombre, sede y si está activo.</summary>
public sealed record EditarCentroCostoCommand(Guid CentroCostoId, string Nombre, string Sede, bool Activo) : ICommand;

internal sealed class EditarSedeValidator : AbstractValidator<EditarSedeCommand>
{
    public EditarSedeValidator() => RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre de la sede.").MaximumLength(200);
}

internal sealed class EditarAreaValidator : AbstractValidator<EditarAreaCommand>
{
    public EditarAreaValidator() => RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre del área.").MaximumLength(200);
}

internal sealed class EditarCentroCostoValidator : AbstractValidator<EditarCentroCostoCommand>
{
    public EditarCentroCostoValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre del centro de costo.").MaximumLength(200);
        RuleFor(c => c.Sede).MaximumLength(200);
    }
}

internal sealed class EditarSedeHandler(IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad) : ICommandHandler<EditarSedeCommand>
{
    public Task<Result> Handle(EditarSedeCommand c, CancellationToken ct) =>
        EdicionMaestro.Aplicar(usuario, estructuras, unidad, EstructuraErrors.SedeNoExiste, e => e.EditarSede(c.SedeId, c.Nombre, c.Activo), ct);
}

internal sealed class EditarAreaHandler(IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad) : ICommandHandler<EditarAreaCommand>
{
    public Task<Result> Handle(EditarAreaCommand c, CancellationToken ct) =>
        EdicionMaestro.Aplicar(usuario, estructuras, unidad, EstructuraErrors.AreaNoExiste, e => e.EditarArea(c.AreaId, c.Nombre, c.Activo), ct);
}

internal sealed class EditarCentroCostoHandler(IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad) : ICommandHandler<EditarCentroCostoCommand>
{
    public Task<Result> Handle(EditarCentroCostoCommand c, CancellationToken ct) =>
        EdicionMaestro.Aplicar(usuario, estructuras, unidad, EstructuraErrors.CentroCostoNoExiste,
            e => e.EditarCentroCosto(c.CentroCostoId, c.Nombre, c.Sede, c.Activo), ct);
}

internal static class EdicionMaestro
{
    public static async Task<Result> Aplicar(
        IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad, Error noExiste, Func<EstructuraEmpresa, Result> editar, CancellationToken ct)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, ct);
        if (estructura is null)
        {
            return Result.Failure(noExiste);
        }

        var resultado = editar(estructura);
        if (resultado.IsSuccess)
        {
            await unidad.SaveChangesAsync(ct);
        }

        return resultado;
    }
}
