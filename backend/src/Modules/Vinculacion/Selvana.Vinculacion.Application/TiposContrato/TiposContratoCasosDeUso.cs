using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Empresas;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Application.Documentos;
using Selvana.Vinculacion.Domain.Procesos;
using Selvana.Vinculacion.Domain.TiposContrato;

namespace Selvana.Vinculacion.Application.TiposContrato;

/// <summary>Maestro de tipos de contrato de la empresa (pocos por empresa: no se pagina, tiene tope).</summary>
public sealed record ListarTiposContratoQuery(bool SoloActivos) : IQuery<IReadOnlyList<TipoContratoDto>>;

public sealed record TipoContratoRequest(string Nombre, string Modalidad, string Descripcion, bool Activo);

public sealed record CrearTipoContratoCommand(TipoContratoRequest Datos) : ICommand<Guid>;

public sealed record EditarTipoContratoCommand(Guid Id, TipoContratoRequest Datos) : ICommand;

/// <summary>Crea los tipos por defecto si la empresa no tiene ninguno.</summary>
public sealed record CargarTiposPorDefectoCommand : ICommand<int>;

/// <summary>Contrato de ejemplo o plantilla del tipo: la IA sigue su estructura al redactar.</summary>
public sealed record SubirPlantillaContratoCommand(Guid Id, string NombreArchivo, string TipoContenido, byte[] Contenido) : ICommand;

public sealed record QuitarPlantillaContratoCommand(Guid Id) : ICommand;

public sealed record ObtenerPlantillaContratoQuery(Guid Id) : IQuery<ArchivoDto>;

public static class TiposContratoErrors
{
    public static readonly Error NoExiste = Error.NotFound("Vinculacion.TipoContratoNoExiste", "Ese tipo de contrato no existe en su empresa.");
    public static readonly Error Inactivo = Error.Validation("Vinculacion.TipoContratoInactivo", "Ese tipo de contrato está inactivo. Elija otro de la lista o actívelo en Tipos de contrato.");
    public static readonly Error NombreRepetido = Error.Conflict("Vinculacion.TipoContratoRepetido", "Ya hay un tipo de contrato con ese nombre.");
    public static readonly Error Tope = Error.Validation("Vinculacion.TiposContratoTope", $"Una empresa puede tener hasta {ReglasTiposContrato.Maximo} tipos de contrato. Desactive o edite uno.");
    public static readonly Error ModalidadInvalida = Error.Validation("Vinculacion.ModalidadInvalida", "Elija la modalidad legal de la lista.");
    public static readonly Error SinPlantilla = Error.NotFound("Vinculacion.SinPlantilla", "Este tipo de contrato no tiene plantilla.");
}

internal static class ReglasTiposContrato
{
    public const int Maximo = 30;
    public const int TamanoMaximoPlantilla = 5 * 1024 * 1024;

    public static readonly string[] TiposPlantilla = ["application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];

    public static Result<TipoContrato> Modalidad(string valor) =>
        Enum.TryParse<TipoContrato>(valor, ignoreCase: true, out var m) && Enum.IsDefined(m) ? m : TiposContratoErrors.ModalidadInvalida;
}

internal sealed class TipoContratoRequestValidator : AbstractValidator<TipoContratoRequest>
{
    public TipoContratoRequestValidator()
    {
        RuleFor(t => t.Nombre).NotEmpty().WithMessage("Escriba el nombre del tipo de contrato.")
            .MaximumLength(TipoContratoEmpresa.NombreMaximo).WithMessage($"El nombre va en menos de {TipoContratoEmpresa.NombreMaximo} caracteres.");
        RuleFor(t => t.Modalidad).NotEmpty().WithMessage("Elija la modalidad legal.");
        RuleFor(t => t.Descripcion).NotEmpty().WithMessage("Describa cómo se trabaja este contrato: la IA lo sigue al redactar.")
            .MaximumLength(TipoContratoEmpresa.DescripcionMaxima).WithMessage($"Resuma la descripción en menos de {TipoContratoEmpresa.DescripcionMaxima} caracteres.");
    }
}

internal sealed class CrearTipoContratoValidator : AbstractValidator<CrearTipoContratoCommand>
{
    public CrearTipoContratoValidator() => RuleFor(c => c.Datos).NotNull().SetValidator(new TipoContratoRequestValidator());
}

internal sealed class EditarTipoContratoValidator : AbstractValidator<EditarTipoContratoCommand>
{
    public EditarTipoContratoValidator() => RuleFor(c => c.Datos).NotNull().SetValidator(new TipoContratoRequestValidator());
}

internal sealed class SubirPlantillaContratoValidator : AbstractValidator<SubirPlantillaContratoCommand>
{
    public SubirPlantillaContratoValidator()
    {
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El archivo está vacío. Vuelva a subirlo.")
            .Must(c => c.Length <= ReglasTiposContrato.TamanoMaximoPlantilla).WithMessage("La plantilla pesa más de 5 MB. Suba un PDF o un Word más liviano.");
        RuleFor(c => c.TipoContenido).Must(t => ReglasTiposContrato.TiposPlantilla.Contains(t)).WithMessage("Suba la plantilla en PDF o en Word (.docx).");
        RuleFor(c => c.NombreArchivo).NotEmpty().MaximumLength(200);
    }
}

internal sealed class ListarTiposContratoHandler(IUsuarioActual usuario, ILecturaTiposContrato lectura)
    : IQueryHandler<ListarTiposContratoQuery, IReadOnlyList<TipoContratoDto>>
{
    public async Task<Result<IReadOnlyList<TipoContratoDto>>> Handle(ListarTiposContratoQuery q, CancellationToken ct) =>
        Result.Success(await lectura.DeLaEmpresaAsync(usuario.EmpresaId, q.SoloActivos, ct));
}

internal sealed class CrearTipoContratoHandler(IUsuarioActual usuario, ITiposContrato tipos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<CrearTipoContratoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearTipoContratoCommand c, CancellationToken cancellationToken)
    {
        var modalidad = ReglasTiposContrato.Modalidad(c.Datos.Modalidad);
        if (modalidad.IsFailure)
        {
            return modalidad.Error;
        }

        if (await tipos.ContarAsync(usuario.EmpresaId, cancellationToken) >= ReglasTiposContrato.Maximo)
        {
            return TiposContratoErrors.Tope;
        }

        if (await tipos.ExisteNombreAsync(usuario.EmpresaId, c.Datos.Nombre.Trim(), null, cancellationToken))
        {
            return TiposContratoErrors.NombreRepetido;
        }

        var tipo = TipoContratoEmpresa.Crear(usuario.EmpresaId, c.Datos.Nombre, modalidad.Value, c.Datos.Descripcion, usuario.Nombre, reloj.GetUtcNow());
        if (!c.Datos.Activo)
        {
            tipo.Editar(tipo.Nombre, tipo.Modalidad, tipo.Descripcion, activo: false, usuario.Nombre, tipo.ActualizadoEn);
        }

        tipos.Agregar(tipo);
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Creó el tipo de contrato «{tipo.Nombre}»", ReglasContrato.Nombre(tipo.Modalidad),
            null, "TipoContrato", tipo.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return tipo.Id;
    }
}

internal sealed class EditarTipoContratoHandler(IUsuarioActual usuario, ITiposContrato tipos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<EditarTipoContratoCommand>
{
    public async Task<Result> Handle(EditarTipoContratoCommand c, CancellationToken cancellationToken)
    {
        var tipo = await tipos.PorIdAsync(usuario.EmpresaId, c.Id, cancellationToken);
        if (tipo is null)
        {
            return Result.Failure(TiposContratoErrors.NoExiste);
        }

        var modalidad = ReglasTiposContrato.Modalidad(c.Datos.Modalidad);
        if (modalidad.IsFailure)
        {
            return Result.Failure(modalidad.Error);
        }

        if (await tipos.ExisteNombreAsync(usuario.EmpresaId, c.Datos.Nombre.Trim(), tipo.Id, cancellationToken))
        {
            return Result.Failure(TiposContratoErrors.NombreRepetido);
        }

        tipo.Editar(c.Datos.Nombre, modalidad.Value, c.Datos.Descripcion, c.Datos.Activo, usuario.Nombre, reloj.GetUtcNow());
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Editó el tipo de contrato «{tipo.Nombre}»",
            $"{ReglasContrato.Nombre(tipo.Modalidad)} · {(tipo.Activo ? "activo" : "inactivo")}", "Los contratos ya abiertos conservan la versión anterior",
            "TipoContrato", tipo.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class CargarTiposPorDefectoHandler(IUsuarioActual usuario, IDirectorioEmpresas empresas, ITiposContrato tipos, IBitacora bitacora,
    IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<CargarTiposPorDefectoCommand, int>
{
    public async Task<Result<int>> Handle(CargarTiposPorDefectoCommand c, CancellationToken cancellationToken)
    {
        if (await tipos.ContarAsync(usuario.EmpresaId, cancellationToken) > 0)
        {
            return 0;
        }

        var empresa = await empresas.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        var nuevos = TipoContratoEmpresa.PorDefectoPara(usuario.EmpresaId, empresa?.EsTemporal ?? false, reloj.GetUtcNow());
        foreach (var t in nuevos)
        {
            tipos.Agregar(t);
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, "Cargó los tipos de contrato por defecto",
            string.Join(", ", nuevos.Select(t => t.Nombre)), null, "TipoContrato", null);
        await unidad.SaveChangesAsync(cancellationToken);
        return nuevos.Count;
    }
}

internal sealed class SubirPlantillaContratoHandler(IUsuarioActual usuario, ITiposContrato tipos, IAlmacenDocumentos almacen, IBitacora bitacora,
    IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<SubirPlantillaContratoCommand>
{
    public async Task<Result> Handle(SubirPlantillaContratoCommand c, CancellationToken cancellationToken)
    {
        var tipo = await tipos.PorIdAsync(usuario.EmpresaId, c.Id, cancellationToken);
        if (tipo is null)
        {
            return Result.Failure(TiposContratoErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var archivo = almacen.Guardar(usuario.EmpresaId, c.NombreArchivo, c.TipoContenido, c.Contenido, ahora);
        tipo.PonerPlantilla(archivo, c.NombreArchivo, c.TipoContenido, usuario.Nombre, ahora);
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Subió la plantilla del tipo de contrato «{tipo.Nombre}»", c.NombreArchivo,
            "La IA seguirá esta plantilla al redactar los contratos nuevos", "TipoContrato", tipo.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class QuitarPlantillaContratoHandler(IUsuarioActual usuario, ITiposContrato tipos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<QuitarPlantillaContratoCommand>
{
    public async Task<Result> Handle(QuitarPlantillaContratoCommand c, CancellationToken cancellationToken)
    {
        var tipo = await tipos.PorIdAsync(usuario.EmpresaId, c.Id, cancellationToken);
        if (tipo is null)
        {
            return Result.Failure(TiposContratoErrors.NoExiste);
        }

        if (tipo.PlantillaArchivoId is null)
        {
            return Result.Success();
        }

        var nombre = tipo.PlantillaNombre;
        tipo.QuitarPlantilla(usuario.Nombre, reloj.GetUtcNow());
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Quitó la plantilla del tipo de contrato «{tipo.Nombre}»", nombre ?? "",
            null, "TipoContrato", tipo.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ObtenerPlantillaContratoHandler(IUsuarioActual usuario, ITiposContrato tipos, IAlmacenDocumentos almacen)
    : IQueryHandler<ObtenerPlantillaContratoQuery, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(ObtenerPlantillaContratoQuery q, CancellationToken ct)
    {
        var tipo = await tipos.PorIdAsync(usuario.EmpresaId, q.Id, ct);
        if (tipo is null)
        {
            return TiposContratoErrors.NoExiste;
        }

        return tipo.PlantillaArchivoId is { } id && await almacen.LeerAsync(id, ct) is { } a
            ? new ArchivoDto(a.Nombre, a.TipoContenido, a.Contenido)
            : TiposContratoErrors.SinPlantilla;
    }
}
