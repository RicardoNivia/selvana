using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Bandeja;
using Selvana.Core.Contracts.Estructura;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura.Cargos;

public sealed record PerfilEditado(string Mision, IReadOnlyList<string> Funciones, IReadOnlyList<string> Requisitos, string RiesgoArl);

/// <summary>
/// Crea un cargo que aún no está en la nómina. Con <paramref name="Perfil"/> lo escribió una persona y queda aprobado;
/// sin él, la IA redacta el borrador guiada por la <paramref name="Descripcion"/> (nada se llena en blanco).
/// </summary>
public sealed record CrearCargoCommand(string Nombre, string Area, string? Descripcion = null, PerfilEditado? Perfil = null) : ICommand<Guid>;

/// <summary>Edita un cargo como dato maestro. Si trae perfil, queda aprobado por quien lo editó.</summary>
public sealed record EditarCargoCommand(Guid CargoId, string Nombre, string Area, bool Activo, PerfilEditado? Perfil) : ICommand;

/// <summary>Pide a la IA un borrador nuevo del perfil de un cargo que no lo tiene aprobado.</summary>
public sealed record SolicitarPerfilCargoCommand(Guid CargoId) : ICommand;

internal sealed class PerfilEditadoValidator : AbstractValidator<PerfilEditado>
{
    private static readonly string[] Clases = ["Clase I", "Clase II", "Clase III", "Clase IV", "Clase V", "Por definir"];

    public PerfilEditadoValidator()
    {
        RuleFor(p => p.Mision).NotEmpty().WithMessage("Escriba la misión del cargo.").MaximumLength(1000);
        RuleFor(p => p.Funciones).Must(f => f.Count is >= 1 and <= 12).WithMessage("Escriba entre 1 y 12 funciones.");
        RuleFor(p => p.Requisitos).Must(r => r.Count is >= 1 and <= 10).WithMessage("Escriba entre 1 y 10 requisitos.");
        RuleFor(p => p.RiesgoArl).Must(Clases.Contains).WithMessage("Elija una clase de riesgo ARL válida.");
    }
}

internal sealed class CrearCargoValidator : AbstractValidator<CrearCargoCommand>
{
    public CrearCargoValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre del cargo.").MaximumLength(200);
        RuleFor(c => c.Area).NotEmpty().WithMessage("Elija el área del cargo.").MaximumLength(200);
        RuleFor(c => c.Descripcion).MaximumLength(2000).WithMessage("Cuéntele a la IA el cargo en menos de 2.000 caracteres.");
        RuleFor(c => c.Perfil!).SetValidator(new PerfilEditadoValidator()).When(c => c.Perfil is not null);
    }
}

internal sealed class EditarCargoValidator : AbstractValidator<EditarCargoCommand>
{
    public EditarCargoValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre del cargo.").MaximumLength(200);
        RuleFor(c => c.Area).NotEmpty().WithMessage("Elija el área del cargo.").MaximumLength(200);
        RuleFor(c => c.Perfil!).SetValidator(new PerfilEditadoValidator()).When(c => c.Perfil is not null);
    }
}

internal static class PerfilesEditados
{
    public static PerfilCargo? APerfil(PerfilEditado? perfil) => perfil is null ? null : new PerfilCargo
    {
        Mision = perfil.Mision.Trim(),
        Funciones = [.. perfil.Funciones.Select(f => f.Trim()).Where(f => f.Length > 0)],
        Requisitos = [.. perfil.Requisitos.Select(r => r.Trim()).Where(r => r.Length > 0)],
        RiesgoArl = perfil.RiesgoArl,
    };
}

internal sealed class CrearCargoHandler(
    IContextoUsuario usuario, IEstructuras estructuras, IEventosSalientes eventos, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<CrearCargoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearCargoCommand command, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            estructura = EstructuraEmpresa.Crear(usuario.EmpresaId);
            estructuras.Agregar(estructura);
        }

        var ahora = reloj.GetUtcNow();
        var resultado = estructura.CrearCargo(command.Nombre, command.Area, PerfilesEditados.APerfil(command.Perfil), usuario.Nombre, ahora);
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        var cargo = resultado.Value;
        if (command.Perfil is null)
        {
            var descripcion = string.IsNullOrWhiteSpace(command.Descripcion) ? null : command.Descripcion.Trim();
            eventos.Publicar(new PerfilesCargoSolicitados(Guid.CreateVersion7(), ahora, usuario.EmpresaId,
                [new CargoParaPerfil(cargo.Id, cargo.Nombre, cargo.Area, 0, [cargo.Nombre], descripcion)]));
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return cargo.Id;
    }
}

internal sealed class EditarCargoHandler(IContextoUsuario usuario, IEstructuras estructuras, IBandeja bandeja, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<EditarCargoCommand>
{
    public async Task<Result> Handle(EditarCargoCommand command, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            return Result.Failure(EstructuraErrors.CargoNoExiste);
        }

        var resultado = estructura.EditarCargo(command.CargoId, command.Nombre, command.Area, command.Activo,
            PerfilesEditados.APerfil(command.Perfil), usuario.Nombre, reloj.GetUtcNow());
        if (resultado.IsSuccess)
        {
            // Guardar el perfil lo aprueba: la propuesta de la IA en la bandeja queda resuelta, como si se aprobara desde allá
            if (command.Perfil is not null)
            {
                await bandeja.ResolverAsync(usuario.EmpresaId, "PerfilCargo", command.CargoId, "Perfil aprobado al guardarlo", usuario.Nombre, cancellationToken);
            }

            await unidad.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }
}

internal sealed class SolicitarPerfilCargoHandler(
    IContextoUsuario usuario, IEstructuras estructuras, IEventosSalientes eventos, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<SolicitarPerfilCargoCommand>
{
    public async Task<Result> Handle(SolicitarPerfilCargoCommand command, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            return Result.Failure(EstructuraErrors.CargoNoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var resultado = estructura.SolicitarPerfil(command.CargoId, ahora);
        if (resultado.IsFailure)
        {
            return Result.Failure(resultado.Error);
        }

        var c = resultado.Value;
        eventos.Publicar(new PerfilesCargoSolicitados(Guid.CreateVersion7(), ahora, usuario.EmpresaId,
            [new CargoParaPerfil(c.Id, c.Nombre, c.Area, c.Empleados, c.NombresNomina.Count > 0 ? c.NombresNomina : [c.Nombre])]));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
