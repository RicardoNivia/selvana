using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Contracts.Eventos;
using Selvana.Expediente.Domain.Empleados;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Contracts.Empleados;

namespace Selvana.Expediente.Application.Empleados;

/* ---------- DTO ---------- */

public sealed record SituacionDto(
    string Cargo, string Area, string CentroCosto, string Sede, Guid? EmpresaUsuariaId, string? EmpresaUsuaria, string TipoContrato, decimal SalarioCop,
    DateOnly? FechaFinContrato);

public sealed record AfiliacionesDto(string Eps, string FondoPension, string Arl, int? ClaseRiesgoArl, string CajaCompensacion);

public sealed record NovedadDto(Guid Id, string Tipo, DateOnly FechaEfectiva, IReadOnlyList<string> Cambios, string Motivo, string Por, DateTimeOffset En);

public sealed record VersionDto(Guid ArchivoId, string NombreArchivo, DateOnly? VenceEn, string? Nota, string SubidoPor, DateTimeOffset SubidoEn);

public sealed record DocumentoDto(Guid Id, string Tipo, string Nombre, DateOnly? VenceEn, string EstadoVigencia, IReadOnlyList<VersionDto> Versiones);

public sealed record AlertaDto(string Nivel, string Texto, string Fuente);

public sealed record EmpleadoDto(
    Guid Id, string Codigo, string Documento, string Nombre, string Correo, string Celular, string Ciudad, string Origen, Guid? ProcesoVinculacionId,
    SituacionDto Situacion, DateOnly FechaIngreso, AfiliacionesDto Afiliaciones, string Estado, DateOnly? FechaRetiro, string? MotivoRetiro,
    IReadOnlyList<AlertaDto> Alertas, IReadOnlyList<NovedadDto> Novedades, IReadOnlyList<DocumentoDto> Documentos, DateTimeOffset CreadoEn, DateTimeOffset ActualizadoEn);

public sealed record ListaEmpleadosDto(Pagina<FilaEmpleadoDto> Empleados, ConteoEmpleadosDto Conteo);

public sealed record ArchivoDto(string Nombre, string TipoContenido, byte[] Contenido);

/* ---------- Consultas y comandos ---------- */

public sealed record ListarEmpleadosQuery(int? Pagina, int? Tamano, string? Buscar, string? Estado, bool SoloConAlertas) : IQuery<ListaEmpleadosDto>;

public sealed record ObtenerEmpleadoQuery(Guid Id) : IQuery<EmpleadoDto>;

public sealed record ObtenerArchivoEmpleadoQuery(Guid EmpleadoId, Guid ArchivoId) : IQuery<ArchivoDto>;

/// <summary>Camino manual: crear el empleado sin pasar por la vinculación (por ejemplo, la planta que ya existía).</summary>
public sealed record CrearEmpleadoManualCommand(
    string Documento, string Nombre, string Correo, string Celular, string Ciudad, SituacionDto Situacion, DateOnly FechaIngreso, AfiliacionesDto Afiliaciones)
    : ICommand<Guid>;

public sealed record ActualizarDatosEmpleadoCommand(Guid Id, string Nombre, string Correo, string Celular, string Ciudad, AfiliacionesDto Afiliaciones) : ICommand;

/// <summary>Cambio de cargo, salario, traslado o contrato con su fecha efectiva y motivo.</summary>
public sealed record RegistrarNovedadCommand(Guid Id, string Tipo, SituacionDto Situacion, DateOnly FechaEfectiva, string Motivo) : ICommand;

public sealed record RetirarEmpleadoCommand(Guid Id, DateOnly Fecha, string Motivo) : ICommand;

/// <summary>Documento nuevo en la carpeta, o versión nueva de uno que ya está (con <see cref="DocumentoId"/>).</summary>
public sealed record GuardarDocumentoEmpleadoCommand(
    Guid Id, Guid? DocumentoId, string Tipo, string Nombre, DateOnly? VenceEn, string? Nota, string NombreArchivo, string TipoContenido, byte[] Contenido)
    : ICommand<Guid>;

internal static class Expedientes
{
    public static readonly string[] TiposDocumento =
        ["Cédula", "Contrato", "Hoja de vida", "Certificado de estudios", "Curso o certificación", "Licencia", "Afiliación", "Certificado bancario", "Otro"];

    public static readonly string[] TiposArchivo = ["application/pdf", "image/jpeg", "image/png", "image/webp",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];

    public const int TamanoMaximo = 10 * 1024 * 1024;

    public static SituacionLaboral Situacion(SituacionDto s) =>
        new(s.Cargo, s.Area ?? "", s.CentroCosto ?? "", s.Sede ?? "", s.EmpresaUsuariaId, s.EmpresaUsuaria, s.TipoContrato, Math.Round(s.SalarioCop), s.FechaFinContrato);

    public static Afiliaciones Afiliaciones(AfiliacionesDto a) => new()
    {
        Eps = a.Eps ?? "", FondoPension = a.FondoPension ?? "", Arl = a.Arl ?? "", ClaseRiesgoArl = a.ClaseRiesgoArl, CajaCompensacion = a.CajaCompensacion ?? "",
    };

    public static async Task<Result<Empleado>> DelUsuarioAsync(IEmpleados empleados, IUsuarioActual usuario, Guid id, CancellationToken ct)
    {
        var e = await empleados.PorIdAsync(usuario.EmpresaId, id, ct);
        return e is null || !usuario.Ve(e.EmpresaUsuariaId) ? EmpleadoErrors.NoExiste : e;
    }

    public static string Vigencia(DocumentoEmpleado d, DateOnly hoy) => d.Vigente?.VenceEn switch
    {
        null => "SinVencimiento",
        var v when v < hoy => "Vencido",
        var v when v <= hoy.AddDays(Empleado.DiasAvisoVencimiento) => "PorVencer",
        _ => "Vigente",
    };

    public static EmpleadoDto Dto(Empleado e, DateOnly hoy)
    {
        var s = e.Situacion;
        var a = e.Afiliaciones;
        return new EmpleadoDto(e.Id, e.Codigo, e.Documento, e.Nombre, e.Correo, e.Celular, e.Ciudad, e.Origen, e.ProcesoVinculacionId,
            new SituacionDto(s.Cargo, s.Area, s.CentroCosto, s.Sede, s.EmpresaUsuariaId, s.EmpresaUsuaria, s.TipoContrato, s.SalarioCop, s.FechaFinContrato),
            e.FechaIngreso, new AfiliacionesDto(a.Eps, a.FondoPension, a.Arl, a.ClaseRiesgoArl, a.CajaCompensacion), e.Estado.ToString(), e.FechaRetiro, e.MotivoRetiro,
            [.. e.Alertas(hoy).Select(x => new AlertaDto(x.Nivel.ToString(), x.Texto, x.Fuente))],
            [.. e.Novedades.OrderByDescending(n => n.FechaEfectiva).ThenByDescending(n => n.En)
                .Select(n => new NovedadDto(n.Id, n.Tipo.ToString(), n.FechaEfectiva, n.Cambios, n.Motivo, n.Por, n.En))],
            [.. e.Documentos.OrderBy(d => d.Tipo).ThenBy(d => d.Nombre).Select(d => new DocumentoDto(d.Id, d.Tipo, d.Nombre, d.Vigente?.VenceEn, Vigencia(d, hoy),
                [.. d.Versiones.AsEnumerable().Reverse().Select(v => new VersionDto(v.ArchivoId, v.NombreArchivo, v.VenceEn, v.Nota, v.SubidoPor, v.SubidoEn))]))],
            e.CreadoEn, e.ActualizadoEn);
    }
}

/* ---------- Validadores ---------- */

internal sealed class SituacionValidator : AbstractValidator<SituacionDto>
{
    public SituacionValidator()
    {
        RuleFor(s => s.Cargo).NotEmpty().WithMessage("Escriba el cargo.").MaximumLength(200);
        RuleFor(s => s.TipoContrato).NotEmpty().WithMessage("Elija el tipo de contrato.").MaximumLength(100);
        RuleFor(s => s.SalarioCop).GreaterThan(0).WithMessage("Escriba el salario.").LessThan(1_000_000_000).WithMessage("Revise el salario: es demasiado alto.");
        RuleFor(s => s.Area).MaximumLength(200);
        RuleFor(s => s.CentroCosto).MaximumLength(200);
        RuleFor(s => s.Sede).MaximumLength(200);
    }
}

internal sealed class CrearEmpleadoManualValidator : AbstractValidator<CrearEmpleadoManualCommand>
{
    public CrearEmpleadoManualValidator()
    {
        RuleFor(c => c.Documento).NotEmpty().WithMessage("Escriba el número de documento.").MaximumLength(20);
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre completo.").MaximumLength(200);
        RuleFor(c => c.Correo).MaximumLength(200);
        RuleFor(c => c.Celular).MaximumLength(30);
        RuleFor(c => c.Situacion).NotNull().SetValidator(new SituacionValidator());
    }
}

internal sealed class ActualizarDatosEmpleadoValidator : AbstractValidator<ActualizarDatosEmpleadoCommand>
{
    public ActualizarDatosEmpleadoValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba el nombre completo.").MaximumLength(200);
        RuleFor(c => c.Afiliaciones.ClaseRiesgoArl).InclusiveBetween(1, 5).When(c => c.Afiliaciones?.ClaseRiesgoArl is not null)
            .WithMessage("La clase de riesgo de la ARL va de 1 a 5.");
    }
}

internal sealed class RegistrarNovedadValidator : AbstractValidator<RegistrarNovedadCommand>
{
    public RegistrarNovedadValidator()
    {
        RuleFor(c => c.Situacion).NotNull().SetValidator(new SituacionValidator());
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Escriba el motivo de la novedad.").MaximumLength(500);
    }
}

internal sealed class RetirarEmpleadoValidator : AbstractValidator<RetirarEmpleadoCommand>
{
    public RetirarEmpleadoValidator() => RuleFor(c => c.Motivo).NotEmpty().WithMessage("Escriba el motivo del retiro.").MaximumLength(500);
}

internal sealed class GuardarDocumentoEmpleadoValidator : AbstractValidator<GuardarDocumentoEmpleadoCommand>
{
    public GuardarDocumentoEmpleadoValidator()
    {
        RuleFor(c => c.Tipo).Must(t => Expedientes.TiposDocumento.Contains(t)).When(c => c.DocumentoId is null).WithMessage("Elija el tipo de documento.");
        RuleFor(c => c.Nombre).NotEmpty().When(c => c.DocumentoId is null).WithMessage("Escriba el nombre del documento.").MaximumLength(200);
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El archivo está vacío.").Must(c => c.Length <= Expedientes.TamanoMaximo).WithMessage("El archivo pesa más de 10 MB.");
        RuleFor(c => c.TipoContenido).Must(t => Expedientes.TiposArchivo.Contains(t)).WithMessage("Suba el documento en PDF, Word o como imagen (JPG o PNG).");
        RuleFor(c => c.Nota).MaximumLength(500);
    }
}

/* ---------- Handlers ---------- */

internal sealed class ListarEmpleadosHandler(IUsuarioActual usuario, ILecturaEmpleados lectura, TimeProvider reloj) : IQueryHandler<ListarEmpleadosQuery, ListaEmpleadosDto>
{
    public async Task<Result<ListaEmpleadosDto>> Handle(ListarEmpleadosQuery q, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var estado = Enum.TryParse<EstadoEmpleado>(q.Estado, ignoreCase: true, out var e) ? e.ToString() : null;
        var pagina = await lectura.EmpleadosAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, new PaginaSolicitada(q.Pagina, q.Tamano), q.Buscar, estado, q.SoloConAlertas,
            hoy, ct);
        return new ListaEmpleadosDto(pagina, await lectura.ConteoAsync(usuario.EmpresaId, usuario.EmpresasUsuarias, hoy, ct));
    }
}

internal sealed class ObtenerEmpleadoHandler(IUsuarioActual usuario, IEmpleados empleados, TimeProvider reloj) : IQueryHandler<ObtenerEmpleadoQuery, EmpleadoDto>
{
    public async Task<Result<EmpleadoDto>> Handle(ObtenerEmpleadoQuery q, CancellationToken ct)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, q.Id, ct);
        return r.IsFailure ? r.Error : Expedientes.Dto(r.Value, DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime));
    }
}

internal sealed class ObtenerArchivoEmpleadoHandler(IUsuarioActual usuario, IEmpleados empleados, IAlmacenArchivos almacen) : IQueryHandler<ObtenerArchivoEmpleadoQuery, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(ObtenerArchivoEmpleadoQuery q, CancellationToken ct)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, q.EmpleadoId, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        // Solo archivos de la carpeta de ese empleado
        if (!r.Value.Documentos.Any(d => d.Versiones.Any(v => v.ArchivoId == q.ArchivoId)))
        {
            return EmpleadoErrors.DocumentoNoExiste;
        }

        return await almacen.LeerAsync(usuario.EmpresaId, q.ArchivoId, ct) is { } a ? new ArchivoDto(a.Nombre, a.TipoContenido, a.Contenido) : EmpleadoErrors.DocumentoNoExiste;
    }
}

internal sealed class CrearEmpleadoManualHandler(IUsuarioActual usuario, IEmpleados empleados, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : ICommandHandler<CrearEmpleadoManualCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearEmpleadoManualCommand c, CancellationToken cancellationToken)
    {
        if (!usuario.Ve(c.Situacion.EmpresaUsuariaId))
        {
            return EmpleadoErrors.NoExiste;
        }

        if (await empleados.ActivoConDocumentoAsync(usuario.EmpresaId, c.Documento.Trim(), cancellationToken))
        {
            return EmpleadoErrors.YaExiste;
        }

        var ahora = reloj.GetUtcNow();
        var e = Empleado.Crear(usuario.EmpresaId, "Manual", c.Documento, c.Nombre, c.Correo ?? "", c.Celular ?? "", c.Ciudad ?? "", Expedientes.Situacion(c.Situacion),
            c.FechaIngreso, Expedientes.Afiliaciones(c.Afiliaciones), usuario.Nombre, ahora);
        empleados.Agregar(e);
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Creó a mano el expediente de {e.Nombre}", $"{e.Codigo} · {e.Cargo}", null, "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return e.Id;
    }
}

internal sealed class ActualizarDatosEmpleadoHandler(IUsuarioActual usuario, IEmpleados empleados, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : ICommandHandler<ActualizarDatosEmpleadoCommand>
{
    public async Task<Result> Handle(ActualizarDatosEmpleadoCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.Id, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var hecho = e.ActualizarDatos(c.Nombre, c.Correo ?? "", c.Celular ?? "", c.Ciudad ?? "", Expedientes.Afiliaciones(c.Afiliaciones), usuario.Nombre, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Actualizó los datos de {e.Nombre}", string.Join(" · ", e.Novedades[^1].Cambios), null,
            "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class RegistrarNovedadHandler(IUsuarioActual usuario, IEmpleados empleados, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : ICommandHandler<RegistrarNovedadCommand>
{
    public async Task<Result> Handle(RegistrarNovedadCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.Id, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        if (!Enum.TryParse<TipoNovedad>(c.Tipo, ignoreCase: true, out var tipo)
            || tipo is not (TipoNovedad.CambioCargo or TipoNovedad.CambioSalario or TipoNovedad.Traslado or TipoNovedad.CambioContrato))
        {
            return Result.Failure(Error.Validation("Expediente.TipoNovedad", "Elija el tipo de novedad: cambio de cargo, de salario, traslado o cambio de contrato."));
        }

        if (!usuario.Ve(c.Situacion.EmpresaUsuariaId))
        {
            return Result.Failure(EmpleadoErrors.NoExiste);
        }

        var e = r.Value;
        var hecho = e.RegistrarNovedad(tipo, Expedientes.Situacion(c.Situacion), c.FechaEfectiva, c.Motivo, usuario.Nombre, reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Registró una novedad de {e.Nombre}",
            $"{e.Codigo} · {string.Join(" · ", e.Novedades[^1].Cambios)}", c.Motivo, "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class RetirarEmpleadoHandler(IUsuarioActual usuario, IEmpleados empleados, IEventosExpediente eventos, IBitacora bitacora, IUnidadExpediente unidad,
    TimeProvider reloj) : ICommandHandler<RetirarEmpleadoCommand>
{
    public async Task<Result> Handle(RetirarEmpleadoCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.Id, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var ahora = reloj.GetUtcNow();
        var hecho = e.Retirar(c.Fecha, c.Motivo, usuario.Nombre, ahora);
        if (hecho.IsFailure)
        {
            return hecho;
        }

        eventos.Publicar(new EmpleadoRetirado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.Id, c.Fecha, e.MotivoRetiro ?? ""));
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Registró el retiro de {e.Nombre}", $"{e.Codigo} · {c.Fecha:dd/MM/yyyy}", c.Motivo,
            "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class GuardarDocumentoEmpleadoHandler(IUsuarioActual usuario, IEmpleados empleados, IAlmacenArchivos almacen, IBitacora bitacora, IUnidadExpediente unidad,
    TimeProvider reloj) : ICommandHandler<GuardarDocumentoEmpleadoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(GuardarDocumentoEmpleadoCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.Id, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var e = r.Value;
        var ahora = reloj.GetUtcNow();
        var archivo = almacen.Guardar(e.EmpresaId, c.NombreArchivo, c.TipoContenido, c.Contenido, ahora);
        var doc = e.GuardarDocumento(c.DocumentoId, c.Tipo, c.Nombre, archivo, c.NombreArchivo, c.VenceEn, c.Nota, usuario.Nombre, ahora);
        if (doc.IsFailure)
        {
            return doc.Error;
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre,
            c.DocumentoId is null ? $"Agregó «{doc.Value.Nombre}» a la carpeta de {e.Nombre}" : $"Subió una versión nueva de «{doc.Value.Nombre}» de {e.Nombre}",
            $"{e.Codigo} · {c.NombreArchivo}{(c.VenceEn is { } v ? $" · vence {v:dd/MM/yyyy}" : "")}", null, "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return doc.Value.Id;
    }
}

/// <summary>Se cerró la vinculación: el empleado nace en el expediente con su contrato y sus afiliaciones (una sola vez por proceso).</summary>
internal sealed class EmpleadoVinculadoConsumer(IEmpleados empleados, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<EmpleadoVinculado>
{
    public async Task Handle(EmpleadoVinculado e, CancellationToken cancellationToken)
    {
        if (await empleados.DeProcesoAsync(e.ProcesoId, cancellationToken))
        {
            return;
        }

        var empleado = Empleado.Crear(e.EmpresaId, "Vinculación", e.Documento, e.Nombre, e.Correo, e.Celular, e.Ciudad,
            new SituacionLaboral(e.Cargo, e.Area, e.CentroCosto, e.Sede, e.EmpresaUsuariaId, e.EmpresaUsuaria, e.TipoContrato, e.SalarioCop, e.FechaFin), e.FechaIngreso,
            new Afiliaciones { Eps = e.Eps, FondoPension = e.FondoPension, Arl = e.Arl, ClaseRiesgoArl = e.ClaseRiesgoArl, CajaCompensacion = e.CajaCompensacion },
            e.CreadoPor, reloj.GetUtcNow(), e.ProcesoId, e.CandidatoId);
        empleados.Agregar(empleado);
        bitacora.Registrar(e.EmpresaId, TipoActor.Sistema, "Expediente", $"Abrió el expediente de {empleado.Nombre}", $"{empleado.Codigo} · {empleado.Cargo}",
            "Viene de la vinculación", "Empleado", empleado.Id);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}
