using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Contracts.Estructura;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Estructura.ImportarNomina;

/// <summary>Lee la nómina y deja una importación para que la IA la organice y una persona la apruebe.</summary>
public sealed record ImportarNominaCommand(string Fuente, Stream Contenido) : ICommand<Guid>;

internal sealed class ImportarNominaValidator : AbstractValidator<ImportarNominaCommand>
{
    public ImportarNominaValidator()
    {
        RuleFor(c => c.Fuente).NotEmpty().WithMessage("Indique de dónde viene la nómina.");
        RuleFor(c => c.Contenido).NotNull().WithMessage("Adjunte el archivo de la nómina.");
    }
}

internal sealed class ImportarNominaHandler(
    IContextoUsuario usuario, ILectorNomina lector, IEstructuras estructuras, IEventosSalientes eventos,
    IUnitOfWork unidad, TimeProvider reloj) : ICommandHandler<ImportarNominaCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ImportarNominaCommand command, CancellationToken cancellationToken)
    {
        var lectura = await lector.LeerAsync(command.Contenido, cancellationToken);

        var estructura = await estructuras.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            estructura = EstructuraEmpresa.Crear(usuario.EmpresaId);
            estructuras.Agregar(estructura);
        }

        var ahora = reloj.GetUtcNow();
        var resultado = estructura.RegistrarLectura(command.Fuente, lectura.Empleados, lectura.Errores, ahora);
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        var importacion = resultado.Value;
        eventos.Publicar(new ImportacionNominaLeida(Guid.CreateVersion7(), ahora, usuario.EmpresaId, importacion.Id,
            [.. importacion.Cargos.Select(c => new CargoLeido(c.Codigo, c.Nombre, c.Area, c.Empleados))]));

        await unidad.SaveChangesAsync(cancellationToken);
        return importacion.Id;
    }
}
