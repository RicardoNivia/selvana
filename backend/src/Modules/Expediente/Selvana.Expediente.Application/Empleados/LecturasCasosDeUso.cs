using FluentValidation;
using Selvana.AI.Contracts.Expediente;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Contracts.Ia;
using Selvana.Expediente.Domain.Empleados;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Application.Empleados;

/// <summary>Pedir (o volver a pedir) que la IA lea la versión vigente de un documento de la carpeta.</summary>
public sealed record PedirLecturaDocumentoCommand(Guid EmpleadoId, Guid DocumentoId) : ICommand;

public sealed record CambioAplicadoRequest(string Clave, string Valor);

/// <summary>Decisión humana: aplicar al expediente los cambios elegidos (con el valor confirmado o corregido).</summary>
public sealed record AplicarLecturaCommand(Guid EmpleadoId, Guid DocumentoId, IReadOnlyList<CambioAplicadoRequest> Cambios) : ICommand;

public sealed record DescartarLecturaCommand(Guid EmpleadoId, Guid DocumentoId) : ICommand;

internal static class Lecturas
{
    public const string TipoBandeja = "ActualizacionExpediente";

    /// <summary>El lector de la IA recibe PDF e imágenes; el Word queda para revisión manual.</summary>
    public static bool Legible(string tipoContenido) => tipoContenido is "application/pdf" or "image/jpeg" or "image/png" or "image/webp";

    public static DocumentoEmpleadoRecibido Solicitud(Empleado e, DocumentoEmpleado d, DateTimeOffset ahora) =>
        new(Guid.CreateVersion7(), ahora, e.EmpresaId, e.Id, d.Id, d.Vigente!.ArchivoId, $"{d.Tipo}: {d.Nombre}", TipoContenido(d.Vigente.NombreArchivo));

    /// <summary>Tipo de contenido por la extensión del archivo guardado; vacío si la IA no lo lee (por ejemplo, Word).</summary>
    public static string TipoContenido(string nombre) => Path.GetExtension(nombre).ToUpperInvariant() switch
    {
        ".PDF" => "application/pdf",
        ".JPG" or ".JPEG" => "image/jpeg",
        ".PNG" => "image/png",
        ".WEBP" => "image/webp",
        _ => "",
    };
}

internal sealed class AplicarLecturaValidator : AbstractValidator<AplicarLecturaCommand>
{
    public AplicarLecturaValidator() => RuleForEach(c => c.Cambios).ChildRules(x => x.RuleFor(y => y.Valor).MaximumLength(200));
}

internal sealed class PedirLecturaDocumentoHandler(IUsuarioActual usuario, IEmpleados empleados, IEventosExpediente eventos, IUnidadExpediente unidad, TimeProvider reloj)
    : ICommandHandler<PedirLecturaDocumentoCommand>
{
    public async Task<Result> Handle(PedirLecturaDocumentoCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.EmpleadoId, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var d = e.Documentos.FirstOrDefault(x => x.Id == c.DocumentoId);
        if (d?.Vigente is null || !Lecturas.Legible(Lecturas.TipoContenido(d.Vigente.NombreArchivo)))
        {
            return Result.Failure(Error.Validation("Expediente.NoLegible", "La IA lee documentos en PDF o en foto. Revise este a mano."));
        }

        var ahora = reloj.GetUtcNow();
        var pedido = e.PedirLectura(d.Id, ahora);
        if (pedido.IsFailure)
        {
            return Result.Failure(pedido.Error);
        }

        eventos.Publicar(Lecturas.Solicitud(e, d, ahora));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class AplicarLecturaHandler(IUsuarioActual usuario, IEmpleados empleados, IEventosExpediente eventos, IBitacora bitacora, IUnidadExpediente unidad,
    TimeProvider reloj) : ICommandHandler<AplicarLecturaCommand>
{
    public async Task<Result> Handle(AplicarLecturaCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.EmpleadoId, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var ahora = reloj.GetUtcNow();
        var hecho = e.AplicarLectura(c.DocumentoId, [.. c.Cambios.Select(x => new CambioAplicado(x.Clave, x.Valor))], usuario.Nombre, ahora);
        if (hecho.IsFailure)
        {
            return hecho;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, e.EmpresaId, Lecturas.TipoBandeja, c.DocumentoId, "Cambios aplicados", usuario.Nombre));
        bitacora.Registrar(e.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aplicó la actualización del expediente de {e.Nombre}",
            c.Cambios.Count == 0 ? "Sin cambios (solo revisó el documento)" : string.Join(" · ", e.Novedades[^1].Cambios),
            "Decisión humana sobre la propuesta de la IA", "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DescartarLecturaHandler(IUsuarioActual usuario, IEmpleados empleados, IEventosExpediente eventos, IBitacora bitacora, IUnidadExpediente unidad,
    TimeProvider reloj) : ICommandHandler<DescartarLecturaCommand>
{
    public async Task<Result> Handle(DescartarLecturaCommand c, CancellationToken cancellationToken)
    {
        var r = await Expedientes.DelUsuarioAsync(empleados, usuario, c.EmpleadoId, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var e = r.Value;
        var ahora = reloj.GetUtcNow();
        var hecho = e.DescartarLectura(c.DocumentoId, usuario.Nombre, ahora);
        if (hecho.IsFailure)
        {
            return hecho;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, e.EmpresaId, Lecturas.TipoBandeja, c.DocumentoId, "Descartada", usuario.Nombre));
        bitacora.Registrar(e.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Descartó la propuesta de la IA para {e.Nombre}", e.Codigo, null, "Empleado", e.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>La IA leyó el documento: las reglas proponen los cambios y, si hay algo que decidir, va a la bandeja.</summary>
internal sealed class DocumentoEmpleadoLeidoConsumer(IEmpleados empleados, IEventosExpediente eventos, IBitacora bitacora, IUnidadExpediente unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<DocumentoEmpleadoLeido>
{
    public async Task Handle(DocumentoEmpleadoLeido e, CancellationToken cancellationToken)
    {
        var emp = await empleados.PorIdAsync(e.EmpresaId, e.EmpleadoId, cancellationToken);
        if (emp is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        if (e.Error is not null)
        {
            if (emp.FallaLectura(e.DocumentoId, e.ArchivoId, e.Error, ahora))
            {
                await unidad.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var porRevisar = emp.RegistrarLectura(e.DocumentoId, e.ArchivoId, e.TipoDetectado, e.Legible, e.TipoCorrecto,
            [.. e.Campos.Select(x => new CampoLeido { Campo = x.Campo, Valor = x.Valor })], e.Observaciones,
            e.Razones.Select(x => new RazonLectura { Signo = x.Signo, Texto = x.Texto, Fuente = x.Fuente }), e.Modelo, e.CostoUsd, ahora);
        var d = emp.Documentos.First(x => x.Id == e.DocumentoId);
        var lectura = d.Vigente?.Lectura;
        if (porRevisar && lectura is not null)
        {
            eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, emp.EmpresaId, emp.EmpresaUsuariaId, "Expediente", Lecturas.TipoBandeja, d.Id,
                $"Actualizar el expediente · {emp.Nombre}",
                lectura.Cambios.Count > 0
                    ? $"La IA leyó «{d.Nombre}» y propone {lectura.Cambios.Count} {(lectura.Cambios.Count == 1 ? "cambio" : "cambios")}. Revíselos y aplíquelos."
                    : $"La IA leyó «{d.Nombre}» y hay observaciones por revisar.",
                "Lector de documentos", e.Modelo, [.. e.Razones.Select(x => new RazonPropuesta(x.Signo, x.Texto, x.Fuente))],
                [.. lectura.Cambios.Select(x => new CampoPropuesta(x.Etiqueta, $"{(string.IsNullOrWhiteSpace(x.Actual) ? "—" : x.Actual)} → {x.Propuesto}"))],
                null, $"/consola/empleados/{emp.Id}?pestana=carpeta", true, null));
        }

        bitacora.Registrar(emp.EmpresaId, TipoActor.Ia, "Lector de documentos", $"Leyó «{d.Nombre}» de {emp.Nombre}",
            $"{emp.Codigo} · {lectura?.Cambios.Count ?? 0} cambios propuestos", porRevisar ? "Espera la decisión de RRHH" : "Sin cambios", "Empleado", emp.Id);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}
