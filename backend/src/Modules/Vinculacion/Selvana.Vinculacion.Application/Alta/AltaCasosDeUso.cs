using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Application.Documentos;
using Selvana.Vinculacion.Application.Procesos;
using Selvana.Vinculacion.Contracts.Empleados;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Alta;

/// <summary>Genera el archivo de alta en el formato canónico para cualquier ERP.</summary>
public interface IArchivoAlta
{
    ArchivoDto Generar(ProcesoVinculacion proceso, bool auxilioTransporte);
}

public sealed record ActualizarAfiliacionCommand(Guid ProcesoId, string Tipo, string Estado, string? Entidad, string? Radicado, DateOnly? FechaEfectiva, int? ClaseRiesgo)
    : ICommand;

public sealed record EditarAltaCommand(Guid ProcesoId, string Area, string CentroCosto, string Sede, string Banco, string TipoCuenta, string NumeroCuenta) : ICommand;

/// <summary>Decisión humana: crear el empleado (afecta a una persona). Con alertas de antecedentes o examen, hay que confirmar que se revisaron.</summary>
public sealed record CrearEmpleadoCommand(Guid ProcesoId, bool ConfirmaAlertas) : ICommand;

/// <summary>Descarga el archivo de alta del empleado ya creado (queda en la bitácora).</summary>
public sealed record DescargarAltaCommand(Guid ProcesoId) : ICommand<ArchivoDto>;

internal static class Altas
{
    public const string TipoBandeja = "CrearEmpleado";

    /// <summary>Si todo quedó listo, la decisión de crear el empleado llega a la bandeja (reemplaza la anterior).</summary>
    public static void AvisarSiListo(ProcesoVinculacion p, IEventosVinculacion eventos, DateTimeOffset ahora)
    {
        if (!p.ListoParaCrearEmpleado(DateOnly.FromDateTime(ahora.UtcDateTime)))
        {
            return;
        }

        var confirmar = ReglasAlta.RequiereConfirmarAlertas(p);
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Vinculacion", TipoBandeja, p.Id,
            $"Listo para crear el empleado · {p.Persona.Nombre}",
            $"Documentos aprobados y afiliaciones confirmadas. Al crear el empleado se genera el archivo de alta para la nómina y empieza el onboarding.",
            null, null,
            [.. p.Afiliaciones.Select(a => new RazonPropuesta("+", $"{ReglasAlta.Nombre(a.Tipo)}: {(a.Estado == EstadoAfiliacion.NoAplica ? "no aplica" : a.Entidad)}", "Afiliaciones"))
                .Concat(confirmar ? [new RazonPropuesta("−", "Hay alertas de antecedentes o del examen médico: revíselas", "Documentos")] : []).Take(5)],
            [new CampoPropuesta("Ingreso", p.Condiciones.FechaIngreso.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)),
             new CampoPropuesta("Cargo", p.Vacante.Cargo), new CampoPropuesta("Centro de costo", p.Alta.CentroCosto)],
            confirmar ? null : new AccionPropuesta("Crear el empleado", "POST", $"/api/v1/vinculacion/procesos/{p.Id}/crear-empleado"),
            $"/consola/vinculacion/{p.Id}?paso=Afiliaciones", true, p.Condiciones.FechaIngreso));
    }
}

internal sealed class EditarAltaValidator : AbstractValidator<EditarAltaCommand>
{
    public EditarAltaValidator()
    {
        RuleFor(c => c.Area).MaximumLength(120);
        RuleFor(c => c.CentroCosto).MaximumLength(120);
        RuleFor(c => c.Sede).MaximumLength(120);
        RuleFor(c => c.Banco).MaximumLength(120);
        RuleFor(c => c.TipoCuenta).MaximumLength(40);
        RuleFor(c => c.NumeroCuenta).MaximumLength(40).Matches(@"^[0-9\- ]*$").WithMessage("El número de cuenta lleva solo dígitos y guiones.");
    }
}

internal sealed class ActualizarAfiliacionValidator : AbstractValidator<ActualizarAfiliacionCommand>
{
    public ActualizarAfiliacionValidator()
    {
        RuleFor(c => c.Entidad).MaximumLength(150);
        RuleFor(c => c.Radicado).MaximumLength(60);
    }
}

internal sealed class ActualizarAfiliacionHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<ActualizarAfiliacionCommand>
{
    public async Task<Result> Handle(ActualizarAfiliacionCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        if (!Enum.TryParse<TipoAfiliacion>(c.Tipo, true, out var tipo) || !Enum.TryParse<EstadoAfiliacion>(c.Estado, true, out var estado))
        {
            return Result.Failure(AltaErrors.NoEnAfiliaciones);
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        p.AsegurarAfiliaciones();
        var a = p.ActualizarAfiliacion(tipo, estado, c.Entidad, c.Radicado, c.FechaEfectiva, c.ClaseRiesgo, usuario.Nombre, ahora);
        if (a.IsFailure)
        {
            return a;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Registró la afiliación a {ReglasAlta.Nombre(tipo)}",
            $"{p.Codigo} · {estado} · {c.Entidad}{(c.Radicado is { Length: > 0 } x ? $" · radicado {x}" : "")}", null, "ProcesoVinculacion", p.Id);
        Altas.AvisarSiListo(p, eventos, ahora);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class EditarAltaHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<EditarAltaCommand>
{
    public async Task<Result> Handle(EditarAltaCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        p.AsegurarAfiliaciones();
        var e = p.EditarAlta(c.Area, c.CentroCosto, c.Sede, c.Banco, c.TipoCuenta, c.NumeroCuenta, usuario.Nombre, ahora);
        if (e.IsFailure)
        {
            return e;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Confirmó los datos del alta", $"{p.Codigo} · centro de costo {c.CentroCosto}",
            "Lo que sugirió la IA queda confirmado", "ProcesoVinculacion", p.Id);
        Altas.AvisarSiListo(p, eventos, ahora);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class CrearEmpleadoHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<CrearEmpleadoCommand>
{
    public async Task<Result> Handle(CrearEmpleadoCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        var creado = p.CrearEmpleado(c.ConfirmaAlertas, usuario.Nombre, ahora);
        if (creado.IsFailure)
        {
            return creado;
        }

        string Entidad(TipoAfiliacion t) => p.Afiliaciones.FirstOrDefault(a => a.Tipo == t) is { Estado: EstadoAfiliacion.Confirmada } a ? a.Entidad : "";
        eventos.Publicar(new EmpleadoVinculado(Guid.CreateVersion7(), ahora, p.EmpresaId, p.Id, p.Vacante.CandidatoId, p.EmpresaUsuariaId, p.Persona.Documento, p.Persona.Nombre,
            p.Persona.Correo, p.Persona.Celular, p.Persona.Ciudad, p.Vacante.Cargo, p.Alta.Area, p.Alta.CentroCosto, p.Alta.Sede, ReglasContrato.Nombre(p.Condiciones),
            p.Condiciones.SalarioCop, p.Condiciones.FechaIngreso, p.Condiciones.FechaFin, Entidad(TipoAfiliacion.Eps), Entidad(TipoAfiliacion.Pension), Entidad(TipoAfiliacion.Arl),
            p.Afiliaciones.FirstOrDefault(a => a.Tipo == TipoAfiliacion.Arl)?.ClaseRiesgo, Entidad(TipoAfiliacion.CajaCompensacion), usuario.Nombre, p.EmpresaUsuaria));
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Altas.TipoBandeja, p.Id, "Empleado creado", usuario.Nombre));
        eventos.Publicar(Onboarding.Onboardings.Solicitud(p, ahora));
        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Creó el empleado {p.Persona.Nombre}",
            $"{p.Codigo} · {p.Vacante.Cargo} · ingreso {p.Condiciones.FechaIngreso:dd/MM/yyyy}",
            c.ConfirmaAlertas ? "Confirmó que revisó las alertas de antecedentes o examen médico" : "Decisión humana; se genera el archivo de alta", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DescargarAltaHandler(IUsuarioActual usuario, IProcesos procesos, IArchivoAlta archivo, IParametrosLegales parametros, IBitacora bitacora,
    IUnidadVinculacion unidad) : ICommandHandler<DescargarAltaCommand, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(DescargarAltaCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var p = r.Value;
        if (p.EmpleadoCreadoEn is null)
        {
            return AltaErrors.Incompleto;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, "Descargó el archivo de alta para la nómina", p.Codigo, "Formato canónico para cualquier ERP",
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return archivo.Generar(p, ReglasContrato.TieneAuxilioTransporte(p.Condiciones, parametros.Actuales));
    }
}
