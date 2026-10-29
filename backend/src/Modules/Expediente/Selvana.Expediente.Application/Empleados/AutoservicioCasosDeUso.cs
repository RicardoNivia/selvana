using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Contracts;
using Selvana.Expediente.Domain.Empleados;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Application.Empleados;

/* P5 · Autoservicio del empleado (desde su portal) y campaña de actualización de datos (desde la consola). */

/// <summary>Campaña: pide a un empleado (o a todos los activos que ve el usuario) que confirme o actualice sus datos. Devuelve a cuántos.</summary>
public sealed record PedirActualizacionDatosCommand(Guid? EmpleadoId) : ICommand<int>;

/// <summary>Decisión de RRHH sobre lo que propuso el empleado.</summary>
public sealed record AplicarCambioPropuestoCommand(Guid EmpleadoId) : ICommand;

public sealed record DescartarCambioPropuestoCommand(Guid EmpleadoId) : ICommand;

/// <summary>Desde el portal: el empleado (cédula y correo) propone cambios a sus datos.</summary>
public sealed record ProponerDatosDesdePortalCommand(Guid EmpleadoId, string Documento, string Correo, DatosPropuestosPorElEmpleado Datos) : ICommand;

public sealed record ConfirmarDatosDesdePortalCommand(Guid EmpleadoId, string Documento, string Correo) : ICommand;

public sealed record EntregarSoporteDesdePortalCommand(Guid EmpleadoId, string Documento, string Correo, SoporteDelEmpleado Soporte) : ICommand;

internal static class Autoservicio
{
    public const string TipoBandeja = "DatosEmpleado";

    /// <summary>Lo que el empleado puede subir desde su portal: soportes que actualizan su expediente.</summary>
    public static readonly string[] TiposSoporte = ["Certificado de estudios", "Curso o certificación", "Licencia", "Afiliación", "Certificado bancario", "Otro"];

    public static async Task<Result<Empleado>> DeLaPersonaAsync(IEmpleados empleados, Guid id, string documento, string correo, CancellationToken ct) =>
        await empleados.DeLaPersonaAsync(id, documento, correo, ct) is { } e ? e : EmpleadoErrors.NoExiste;
}

internal sealed class DatosPropuestosValidator : AbstractValidator<ProponerDatosDesdePortalCommand>
{
    public DatosPropuestosValidator()
    {
        RuleFor(c => c.Datos.Celular).MaximumLength(30);
        RuleFor(c => c.Datos.Ciudad).MaximumLength(120);
        RuleFor(c => c.Datos.Eps).MaximumLength(120);
        RuleFor(c => c.Datos.FondoPension).MaximumLength(120);
        RuleFor(c => c.Datos.CajaCompensacion).MaximumLength(120);
    }
}

internal sealed class EntregarSoporteValidator : AbstractValidator<EntregarSoporteDesdePortalCommand>
{
    public EntregarSoporteValidator()
    {
        RuleFor(c => c.Soporte.Tipo).Must(t => Autoservicio.TiposSoporte.Contains(t)).WithMessage("Elige qué documento es.");
        RuleFor(c => c.Soporte.Nombre).NotEmpty().WithMessage("Dale un nombre al documento, por ejemplo «Curso de alturas».").MaximumLength(200);
        RuleFor(c => c.Soporte.Contenido).NotEmpty().WithMessage("El archivo está vacío. Vuelve a subirlo.")
            .Must(c => c.Length <= Expedientes.TamanoMaximo).WithMessage("El archivo pesa más de 10 MB. Sube un PDF más liviano o una foto.");
        RuleFor(c => c.Soporte.TipoContenido).Must(Lecturas.Legible).WithMessage("Sube el documento en PDF o como foto (JPG o PNG).");
    }
}

internal sealed class PedirActualizacionDatosHandler(IUsuarioActual usuario, IEmpleados empleados, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : ICommandHandler<PedirActualizacionDatosCommand, int>
{
    public async Task<Result<int>> Handle(PedirActualizacionDatosCommand c, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        List<Empleado> lista;
        if (c.EmpleadoId is { } id)
        {
            var r = await Expedientes.DelUsuarioAsync(empleados, usuario, id, cancellationToken);
            if (r.IsFailure)
            {
                return r.Error;
            }

            lista = [r.Value];
        }
        else
        {
            lista = [.. (await empleados.ActivosAsync(usuario.EmpresaId, cancellationToken)).Where(e => usuario.Ve(e.EmpresaUsuariaId))];
        }

        var pedidos = lista.Count(e => e.PedirActualizacion(ahora));
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre,
            c.EmpleadoId is null ? "Lanzó la campaña de actualización de datos" : $"Pidió a {lista[0].Nombre} actualizar sus datos",
            $"{pedidos} {(pedidos == 1 ? "empleado" : "empleados")} la verán en su portal", null, "Empleado", c.EmpleadoId);
        await unidad.SaveChangesAsync(cancellationToken);
        return pedidos;
    }
}

internal sealed class AplicarCambioPropuestoHandler(IUsuarioActual usuario, IEmpleados empleados, IEventosExpediente eventos, IBitacora bitacora, IUnidadExpediente unidad,
    TimeProvider reloj) : ICommandHandler<AplicarCambioPropuestoCommand>
{
    public async Task<Result> Handle(AplicarCambioPropuestoCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.EmpleadoId, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var cambios = e.CambioPropuesto?.Cambios ?? [];
        var ahora = reloj.GetUtcNow();
        var aplicado = e.AplicarCambioPropuesto(usuario.Nombre, ahora);
        if (aplicado.IsFailure)
        {
            return aplicado;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, e.EmpresaId, Autoservicio.TipoBandeja, e.Id, "Cambios aplicados", usuario.Nombre));
        bitacora.Registrar(e.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aplicó los datos que propuso {e.Nombre}", string.Join(" · ", cambios),
            "Decisión humana sobre la propuesta del empleado", "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DescartarCambioPropuestoHandler(IUsuarioActual usuario, IEmpleados empleados, IEventosExpediente eventos, IBitacora bitacora,
    IUnidadExpediente unidad, TimeProvider reloj) : ICommandHandler<DescartarCambioPropuestoCommand>
{
    public async Task<Result> Handle(DescartarCambioPropuestoCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.EmpleadoId, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var ahora = reloj.GetUtcNow();
        var descartado = e.DescartarCambioPropuesto(ahora);
        if (descartado.IsFailure)
        {
            return descartado;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, e.EmpresaId, Autoservicio.TipoBandeja, e.Id, "Descartada", usuario.Nombre));
        bitacora.Registrar(e.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Descartó los datos que propuso {e.Nombre}", e.Codigo, null, "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ProponerDatosDesdePortalHandler(IEmpleados empleados, IEventosExpediente eventos, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : ICommandHandler<ProponerDatosDesdePortalCommand>
{
    public async Task<Result> Handle(ProponerDatosDesdePortalCommand c, CancellationToken cancellationToken)
    {
        var r = await Autoservicio.DeLaPersonaAsync(empleados, c.EmpleadoId, c.Documento, c.Correo, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var ahora = reloj.GetUtcNow();
        var d = c.Datos;
        var propuesta = e.ProponerDatos(d.Celular, d.Ciudad, d.Eps, d.FondoPension, d.CajaCompensacion, ahora);
        if (propuesta.IsFailure)
        {
            return Result.Failure(propuesta.Error);
        }

        // Decisión humana: RRHH revisa y aplica en la ficha del empleado
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, e.EmpresaId, e.EmpresaUsuariaId, "Expediente", Autoservicio.TipoBandeja, e.Id,
            $"Actualizar datos · {e.Nombre}", $"{e.Nombre} propuso desde su portal {propuesta.Value.Cambios.Count} {(propuesta.Value.Cambios.Count == 1 ? "cambio" : "cambios")} en sus datos.",
            null, null, [new RazonPropuesta("+", "Lo escribió el propio empleado en su portal", "Portal del empleado")],
            [.. propuesta.Value.Cambios.Select(x => new CampoPropuesta("Cambio", x))], null, $"/consola/empleados/{e.Id}", false, null));
        bitacora.Registrar(e.EmpresaId, TipoActor.Titular, e.Nombre, "Propuso cambios a sus datos desde su portal", string.Join(" · ", propuesta.Value.Cambios),
            "Espera la decisión de RRHH", "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ConfirmarDatosDesdePortalHandler(IEmpleados empleados, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : ICommandHandler<ConfirmarDatosDesdePortalCommand>
{
    public async Task<Result> Handle(ConfirmarDatosDesdePortalCommand c, CancellationToken cancellationToken)
    {
        var r = await Autoservicio.DeLaPersonaAsync(empleados, c.EmpleadoId, c.Documento, c.Correo, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var confirmado = e.ConfirmarDatos(reloj.GetUtcNow());
        if (confirmado.IsFailure)
        {
            return confirmado;
        }

        bitacora.Registrar(e.EmpresaId, TipoActor.Titular, e.Nombre, "Confirmó que sus datos siguen iguales", e.Codigo, "Campaña de actualización de datos", "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class EntregarSoporteDesdePortalHandler(IEmpleados empleados, IAlmacenArchivos almacen, IEventosExpediente eventos, IBitacora bitacora,
    IUnidadExpediente unidad, TimeProvider reloj) : ICommandHandler<EntregarSoporteDesdePortalCommand>
{
    public async Task<Result> Handle(EntregarSoporteDesdePortalCommand c, CancellationToken cancellationToken)
    {
        var r = await Autoservicio.DeLaPersonaAsync(empleados, c.EmpleadoId, c.Documento, c.Correo, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        if (e.Estado == EstadoEmpleado.Retirado)
        {
            return Result.Failure(EmpleadoErrors.Retirado);
        }

        var s = c.Soporte;
        var ahora = reloj.GetUtcNow();
        var archivo = almacen.Guardar(e.EmpresaId, s.NombreArchivo, s.TipoContenido, s.Contenido, ahora);
        // Si ya hay un documento con ese tipo y nombre, el nuevo queda como versión; si no, entra como documento nuevo
        var existente = e.Documentos.FirstOrDefault(d => d.Tipo == s.Tipo && string.Equals(d.Nombre, s.Nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        var doc = e.GuardarDocumento(existente?.Id, s.Tipo, s.Nombre, archivo, s.NombreArchivo, s.VenceEn, "Lo subió el empleado desde su portal", e.Nombre, ahora);
        if (doc.IsFailure)
        {
            return Result.Failure(doc.Error);
        }

        // La IA lo lee y RRHH decide en la bandeja (actualización inteligente, E2)
        if (e.PedirLectura(doc.Value.Id, ahora).IsSuccess)
        {
            eventos.Publicar(Lecturas.Solicitud(e, doc.Value, ahora));
        }

        bitacora.Registrar(e.EmpresaId, TipoActor.Titular, e.Nombre, $"Subió «{doc.Value.Nombre}» desde su portal", $"{e.Codigo} · {s.NombreArchivo}",
            "La IA lo lee y RRHH decide", "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
