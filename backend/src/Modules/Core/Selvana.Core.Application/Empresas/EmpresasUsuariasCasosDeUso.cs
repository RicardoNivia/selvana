using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Usuarios;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Domain.Empresas;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Empresas;

/// <summary>Empresas usuarias de la EST con cuántas personas de la consola tienen acceso a cada una.</summary>
public sealed record ListarEmpresasUsuariasQuery : IQuery<IReadOnlyList<EmpresaUsuariaDto>>;

internal sealed class ListarEmpresasUsuariasHandler(IContextoUsuario usuario, ILecturaUsuarios lectura)
    : IQueryHandler<ListarEmpresasUsuariasQuery, IReadOnlyList<EmpresaUsuariaDto>>
{
    public async Task<Result<IReadOnlyList<EmpresaUsuariaDto>>> Handle(ListarEmpresasUsuariasQuery query, CancellationToken cancellationToken) =>
        Result.Success(await lectura.EmpresasUsuariasAsync(usuario.EmpresaId, cancellationToken));
}

public sealed record AgregarEmpresaUsuariaCommand(string Nombre, string Nit, string Sector, string Ciudad) : ICommand<Guid>;

internal sealed class AgregarEmpresaUsuariaValidator : AbstractValidator<AgregarEmpresaUsuariaCommand>
{
    public AgregarEmpresaUsuariaValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba la razón social.").MaximumLength(200);
        RuleFor(c => c.Nit).NotEmpty().WithMessage("Escriba el NIT.").MaximumLength(20);
        RuleFor(c => c.Sector).MaximumLength(200);
        RuleFor(c => c.Ciudad).MaximumLength(100);
    }
}

internal sealed class AgregarEmpresaUsuariaHandler(IContextoUsuario usuario, IEmpresas empresas, IBitacora bitacora, IUnitOfWork unidad, TimeProvider reloj)
    : ICommandHandler<AgregarEmpresaUsuariaCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AgregarEmpresaUsuariaCommand c, CancellationToken cancellationToken)
    {
        var empresa = await empresas.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (empresa is null)
        {
            return EmpresaErrors.NoExiste;
        }

        var agregada = empresa.AgregarEmpresaUsuaria(c.Nombre, c.Nit, c.Sector ?? "", c.Ciudad ?? "", reloj.GetUtcNow());
        if (agregada.IsFailure)
        {
            return agregada.Error;
        }

        await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Agregó la empresa usuaria {agregada.Value.Nombre}",
            $"NIT {agregada.Value.Nit}{(string.IsNullOrWhiteSpace(agregada.Value.Ciudad) ? "" : $" · {agregada.Value.Ciudad}")}", null, "EmpresaUsuaria", agregada.Value.Id,
            cancellationToken: cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return agregada.Value.Id;
    }
}

public sealed record EditarEmpresaUsuariaCommand(Guid Id, string Nombre, string Sector, string Ciudad, bool Activa) : ICommand;

internal sealed class EditarEmpresaUsuariaValidator : AbstractValidator<EditarEmpresaUsuariaCommand>
{
    public EditarEmpresaUsuariaValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba la razón social.").MaximumLength(200);
        RuleFor(c => c.Sector).MaximumLength(200);
        RuleFor(c => c.Ciudad).MaximumLength(100);
    }
}

internal sealed class EditarEmpresaUsuariaHandler(IContextoUsuario usuario, IEmpresas empresas, IBitacora bitacora, IUnitOfWork unidad)
    : ICommandHandler<EditarEmpresaUsuariaCommand>
{
    public async Task<Result> Handle(EditarEmpresaUsuariaCommand c, CancellationToken cancellationToken)
    {
        var empresa = await empresas.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (empresa is null)
        {
            return Result.Failure(EmpresaErrors.NoExiste);
        }

        var editada = empresa.EditarEmpresaUsuaria(c.Id, c.Nombre, c.Sector ?? "", c.Ciudad ?? "", c.Activa);
        if (editada.IsSuccess)
        {
            await bitacora.RegistrarAsync(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Editó la empresa usuaria {c.Nombre.Trim()}",
                c.Activa ? "Activa" : "La desactivó: nadie la ve en la consola", null, "EmpresaUsuaria", c.Id, cancellationToken: cancellationToken);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return editada;
    }
}
