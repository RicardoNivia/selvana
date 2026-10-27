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

/// <summary>Crea a mano una sede que no llegó de la nómina.</summary>
public sealed record CrearSedeCommand(string Nombre) : ICommand<Guid>;

/// <summary>Crea a mano un área: es el maestro al que se asocian los cargos nuevos.</summary>
public sealed record CrearAreaCommand(string Nombre) : ICommand<Guid>;

/// <summary>Crea a mano un centro de costo con su código, nombre y sede.</summary>
public sealed record CrearCentroCostoCommand(string Codigo, string Nombre, string Sede) : ICommand<Guid>;

internal sealed class CrearSedeValidator : AbstractValidator<CrearSedeCommand>
{
    public CrearSedeValidator() => RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre de la sede.").MaximumLength(200);
}

internal sealed class CrearAreaValidator : AbstractValidator<CrearAreaCommand>
{
    public CrearAreaValidator() => RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre del área.").MaximumLength(200);
}

internal sealed class CrearCentroCostoValidator : AbstractValidator<CrearCentroCostoCommand>
{
    public CrearCentroCostoValidator()
    {
        RuleFor(c => c.Codigo).NotEmpty().WithMessage("Escriba el código del centro de costo.").MaximumLength(50);
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre del centro de costo.").MaximumLength(200);
        RuleFor(c => c.Sede).MaximumLength(200);
    }
}

internal sealed class CrearSedeHandler(IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad) : ICommandHandler<CrearSedeCommand, Guid>
{
    public Task<Result<Guid>> Handle(CrearSedeCommand c, CancellationToken ct) =>
        EdicionMaestro.Crear(usuario, estructuras, unidad, e => EdicionMaestro.Id(e.CrearSede(c.Nombre)), ct);
}

internal sealed class CrearAreaHandler(IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad) : ICommandHandler<CrearAreaCommand, Guid>
{
    public Task<Result<Guid>> Handle(CrearAreaCommand c, CancellationToken ct) =>
        EdicionMaestro.Crear(usuario, estructuras, unidad, e => EdicionMaestro.Id(e.CrearArea(c.Nombre)), ct);
}

internal sealed class CrearCentroCostoHandler(IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad) : ICommandHandler<CrearCentroCostoCommand, Guid>
{
    public Task<Result<Guid>> Handle(CrearCentroCostoCommand c, CancellationToken ct) =>
        EdicionMaestro.Crear(usuario, estructuras, unidad, e => EdicionMaestro.Id(e.CrearCentroCosto(c.Codigo, c.Nombre, c.Sede)), ct);
}

internal static class EdicionMaestro
{
    public static Result<Guid> Id<T>(Result<T> resultado) where T : Maestro => resultado.IsSuccess ? resultado.Value.Id : resultado.Error;

    /// <summary>Crea un maestro; si la empresa aún no tiene estructura (no ha cargado la nómina), se la crea.</summary>
    public static async Task<Result<Guid>> Crear(
        IContextoUsuario usuario, IEstructuras estructuras, IUnitOfWork unidad, Func<EstructuraEmpresa, Result<Guid>> crear, CancellationToken ct)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, ct);
        if (estructura is null)
        {
            estructura = EstructuraEmpresa.Crear(usuario.EmpresaId);
            estructuras.Agregar(estructura);
        }

        var resultado = crear(estructura);
        if (resultado.IsSuccess)
        {
            await unidad.SaveChangesAsync(ct);
        }

        return resultado;
    }

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
