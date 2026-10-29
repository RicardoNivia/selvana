using FluentValidation;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Privacidad;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Contracts;

namespace Selvana.Reclutamiento.Application.Portal;

/* P4 · Mis documentos: la persona los sube una vez a su portal y los entrega a los procesos de contratación que se los pidan. */

public sealed record TipoDocumentoPersonaDto(string Clave, string Nombre, bool Vence);

public sealed record EnvioDocumentoDto(string Empresa, DateTimeOffset En);

/// <summary><paramref name="Vigencia"/>: Vigente, PorVencer, Vencido o SinVencimiento.</summary>
public sealed record MiDocumentoDto(
    Guid Id, string Tipo, string Nombre, string NombreArchivo, DateOnly? VenceEn, string Vigencia, DateTimeOffset SubidoEn, IReadOnlyList<EnvioDocumentoDto> Envios);

/// <summary>Lo que le pide un proceso; <paramref name="PuedeEnviar"/> si tiene ese documento vigente y el proceso aún lo espera.</summary>
public sealed record DocumentoPedidoDto(
    Guid ProcesoId, string Empresa, string Cargo, string Tipo, string Nombre, bool Obligatorio, string Estado, string? MotivoRechazo, bool PuedeEnviar);

public sealed record MisDocumentosDto(IReadOnlyList<TipoDocumentoPersonaDto> Tipos, IReadOnlyList<MiDocumentoDto> Documentos, IReadOnlyList<DocumentoPedidoDto> Pedidos);

public sealed record ObtenerMisDocumentosQuery : IQuery<MisDocumentosDto>;

public sealed record SubirMiDocumentoCommand(string Tipo, DateOnly? VenceEn, string NombreArchivo, string TipoContenido, byte[] Contenido) : ICommand<Guid>;

public sealed record QuitarMiDocumentoCommand(Guid Id) : ICommand;

public sealed record DescargarMiDocumentoQuery(Guid Id) : IQuery<ArchivoDto>;

/// <summary>Entrega a un proceso el documento de ese tipo que la persona tiene en su portal. Con su clic autoriza el envío a esa empresa.</summary>
public sealed record EnviarMiDocumentoCommand(Guid ProcesoId, string Tipo) : ICommand;

internal static class MisDocumentos
{
    public const int DiasAviso = 30;
    public const int TamanoMaximo = 10 * 1024 * 1024;
    public static readonly string[] TiposArchivo = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    /// <summary>Estados en los que el proceso todavía espera el documento de la persona.</summary>
    public static bool LoEspera(string estado) => estado is "Pendiente" or "Rechazado";

    public static string Vigencia(DateOnly? vence, DateOnly hoy) => vence switch
    {
        null => "SinVencimiento",
        var v when v < hoy => "Vencido",
        var v when v <= hoy.AddDays(DiasAviso) => "PorVencer",
        _ => "Vigente",
    };

    public static async Task<Result<CuentaCandidato>> CuentaAsync(IPersonaActual persona, ICuentasCandidato cuentas, CancellationToken ct) =>
        persona.CuentaId is { } id && await cuentas.ObtenerAsync(id, ct) is { } c ? c : CuentaErrors.NoExiste;
}

internal sealed class SubirMiDocumentoValidator : AbstractValidator<SubirMiDocumentoCommand>
{
    public SubirMiDocumentoValidator()
    {
        RuleFor(c => c.Tipo).NotEmpty().WithMessage("Elige qué documento es.");
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El archivo está vacío. Vuelve a subirlo.")
            .Must(c => c.Length <= MisDocumentos.TamanoMaximo).WithMessage("El archivo pesa más de 10 MB. Sube un PDF más liviano o una foto.");
        RuleFor(c => c.TipoContenido).Must(t => MisDocumentos.TiposArchivo.Contains(t)).WithMessage("Sube el documento en PDF o como foto (JPG o PNG).");
    }
}

internal sealed class ObtenerMisDocumentosHandler(IPersonaActual persona, ICuentasCandidato cuentas, IProcesosDeLaPersona procesos, TimeProvider reloj)
    : IQueryHandler<ObtenerMisDocumentosQuery, MisDocumentosDto>
{
    public async Task<Result<MisDocumentosDto>> Handle(ObtenerMisDocumentosQuery query, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var c = r.Value;
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var pedidos = await procesos.DocumentosPedidosAsync(c.Documento, c.Correo, cancellationToken);
        var vigentes = c.Documentos.Where(d => d.VenceEn is not { } v || v >= hoy).Select(d => d.Tipo).ToHashSet();

        return new MisDocumentosDto(
            [.. TiposDocumentoPersona.Todos.Select(t => new TipoDocumentoPersonaDto(t.Clave, t.Nombre, t.Vence))],
            [.. c.Documentos.OrderBy(d => TiposDocumentoPersona.Todos.ToList().FindIndex(t => t.Clave == d.Tipo)).Select(d => new MiDocumentoDto(
                d.Id, d.Tipo, TiposDocumentoPersona.Nombre(d.Tipo) ?? d.Tipo, d.NombreArchivo, d.VenceEn, MisDocumentos.Vigencia(d.VenceEn, hoy), d.SubidoEn,
                [.. d.Envios.OrderByDescending(e => e.En).Select(e => new EnvioDocumentoDto(e.Empresa, e.En))]))],
            [.. pedidos.Select(p => new DocumentoPedidoDto(p.ProcesoId, p.Empresa, p.Cargo, p.Tipo, p.Nombre, p.Obligatorio, p.Estado, p.MotivoRechazo,
                MisDocumentos.LoEspera(p.Estado) && vigentes.Contains(p.Tipo)))]);
    }
}

internal sealed class SubirMiDocumentoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IAlmacenHojasDeVida almacen, IUnidadReclutamiento unidad,
    TimeProvider reloj) : ICommandHandler<SubirMiDocumentoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SubirMiDocumentoCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var ahora = reloj.GetUtcNow();
        var nombre = Path.GetFileName(c.NombreArchivo);
        // Los documentos del portal no son de ninguna empresa: se guardan cifrados como la hoja de vida del perfil
        var archivo = almacen.Guardar(HojaDeVidaPerfil.SinEmpresa, nombre, c.TipoContenido, c.Contenido, ahora);
        var guardado = r.Value.GuardarDocumento(c.Tipo, archivo, nombre, c.TipoContenido, c.VenceEn, ahora);
        if (guardado.IsFailure)
        {
            return guardado.Error;
        }

        if (guardado.Value.ArchivoAnterior is { } anterior)
        {
            await almacen.EliminarAsync([anterior], cancellationToken);
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return guardado.Value.Documento.Id;
    }
}

internal sealed class QuitarMiDocumentoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IAlmacenHojasDeVida almacen, IUnidadReclutamiento unidad)
    : ICommandHandler<QuitarMiDocumentoCommand>
{
    public async Task<Result> Handle(QuitarMiDocumentoCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var archivo = r.Value.QuitarDocumento(c.Id);
        if (archivo.IsFailure)
        {
            return archivo;
        }

        await almacen.EliminarAsync([archivo.Value], cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class DescargarMiDocumentoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IAlmacenHojasDeVida almacen)
    : IQueryHandler<DescargarMiDocumentoQuery, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(DescargarMiDocumentoQuery query, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var d = r.Value.Documentos.FirstOrDefault(x => x.Id == query.Id);
        if (d is null || await almacen.LeerAsync(d.ArchivoId, cancellationToken) is not { } a)
        {
            return DocumentosPersonaErrors.NoExiste;
        }

        return new ArchivoDto(a.Nombre, a.TipoContenido, a.Contenido);
    }
}

internal sealed class EnviarMiDocumentoHandler(IPersonaActual persona, ICuentasCandidato cuentas, IAlmacenHojasDeVida almacen, IProcesosDeLaPersona procesos,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<EnviarMiDocumentoCommand>
{
    public async Task<Result> Handle(EnviarMiDocumentoCommand c, CancellationToken cancellationToken)
    {
        var r = await MisDocumentos.CuentaAsync(persona, cuentas, cancellationToken);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var cuenta = r.Value;
        var ahora = reloj.GetUtcNow();
        var doc = cuenta.DocumentoParaEnviar(c.Tipo, DateOnly.FromDateTime(ahora.UtcDateTime));
        if (doc.IsFailure)
        {
            return doc;
        }

        var pedido = (await procesos.DocumentosPedidosAsync(cuenta.Documento, cuenta.Correo, cancellationToken))
            .FirstOrDefault(p => p.ProcesoId == c.ProcesoId && p.Tipo == c.Tipo);
        if (pedido is null || !MisDocumentos.LoEspera(pedido.Estado))
        {
            return Result.Failure(PortalErrors.PendienteNoExiste);
        }

        if (await almacen.LeerAsync(doc.Value.ArchivoId, cancellationToken) is not { } archivo)
        {
            return Result.Failure(DocumentosPersonaErrors.NoExiste);
        }

        var entregado = await procesos.EntregarDocumentoAsync(cuenta.Documento, cuenta.Correo,
            new EntregaDesdePortal(c.ProcesoId, c.Tipo, archivo.Nombre, archivo.TipoContenido, archivo.Contenido), cancellationToken);
        if (entregado.IsFailure)
        {
            return entregado;
        }

        cuenta.RegistrarEnvio(doc.Value.Id, c.ProcesoId, pedido.Empresa, ahora);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
