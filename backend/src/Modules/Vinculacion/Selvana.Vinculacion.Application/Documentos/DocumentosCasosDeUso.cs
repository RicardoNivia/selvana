using FluentValidation;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Application.Procesos;
using Selvana.Vinculacion.Contracts.Ia;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Documentos;

/// <summary>Guarda los documentos de ingreso cifrados. Solo se leen descifrados por el lector de IA y por quien tenga permiso.</summary>
public interface IAlmacenDocumentos
{
    Guid Guardar(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, DateTimeOffset ahora);

    Task<(string Nombre, string TipoContenido, byte[] Contenido)?> LeerAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Cifra datos sensibles (el concepto médico) para guardarlos en el agregado sin que nadie los vea en claro.</summary>
public interface ICifradoSensible
{
    string Cifrar(string texto);

    string Descifrar(string cifrado);
}

public sealed record ArchivoDto(string Nombre, string TipoContenido, byte[] Contenido);

public sealed record ConceptoMedicoDto(string Concepto, string? Restricciones, string RegistradoPor, DateTimeOffset? RegistradoEn);

public sealed record SubirDocumentoCandidatoCommand(string Token, string Tipo, string NombreArchivo, string TipoContenido, byte[] Contenido) : ICommand;

/// <summary>RRHH sube un documento: los del candidato pasan por la IA; antecedentes y examen médico se registran con su resultado.</summary>
public sealed record SubirDocumentoConsolaCommand(
    Guid ProcesoId, string Tipo, string NombreArchivo, string TipoContenido, byte[] Contenido, string? Antecedentes, string? Concepto, string? Restricciones) : ICommand;

public sealed record AprobarDocumentoCommand(Guid ProcesoId, string Tipo) : ICommand;

public sealed record RechazarDocumentoCommand(Guid ProcesoId, string Tipo, string Motivo) : ICommand;

/// <summary>Desde la bandeja: aprueba los documentos legibles, del tipo pedido y del titular. Devuelve cuántos.</summary>
public sealed record AprobarSinObservacionesCommand(Guid ProcesoId) : ICommand<int>;

/// <summary>Abre el archivo de un documento (queda en la bitácora). El examen médico va por <see cref="VerExamenMedicoCommand"/>.</summary>
public sealed record AbrirDocumentoCommand(Guid ProcesoId, string Tipo) : ICommand<ArchivoDto>;

/// <summary>Ver el concepto médico (dato sensible): solo con permiso, queda en la bitácora como dato sensible.</summary>
public sealed record VerExamenMedicoCommand(Guid ProcesoId, bool ConArchivo) : ICommand<(ConceptoMedicoDto Concepto, ArchivoDto? Archivo)>;

internal static class Docs
{
    public const string TipoBandeja = "DocumentosIngreso";
    public const int TamanoMaximo = 10 * 1024 * 1024;
    public static readonly string[] Tipos = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public static Result<TipoDocumentoIngreso> Tipo(string tipo) =>
        Enum.TryParse<TipoDocumentoIngreso>(tipo, ignoreCase: true, out var t) && Enum.IsDefined(t) ? t : DocumentosErrors.NoExiste;

    public static DocumentoIngresoRecibido Evento(ProcesoVinculacion p, DocumentoIngreso d, string tipoContenido, DateTimeOffset ahora) =>
        new(Guid.CreateVersion7(), ahora, p.EmpresaId, p.Id, d.Tipo.ToString(), RequisitosDocumentales.Nombre(d.Tipo), d.ArchivoId!.Value, d.NombreArchivo ?? "", tipoContenido);
}

internal sealed class SubirDocumentoCandidatoValidator : AbstractValidator<SubirDocumentoCandidatoCommand>
{
    public SubirDocumentoCandidatoValidator()
    {
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El archivo está vacío. Vuelve a subirlo.")
            .Must(c => c.Length <= Docs.TamanoMaximo).WithMessage("El archivo pesa más de 10 MB. Sube un PDF más liviano o una foto.");
        RuleFor(c => c.TipoContenido).Must(t => Docs.Tipos.Contains(t)).WithMessage("Sube el documento en PDF o como foto (JPG o PNG).");
    }
}

internal sealed class SubirDocumentoConsolaValidator : AbstractValidator<SubirDocumentoConsolaCommand>
{
    public SubirDocumentoConsolaValidator()
    {
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El archivo está vacío.").Must(c => c.Length <= Docs.TamanoMaximo).WithMessage("El archivo pesa más de 10 MB.");
        RuleFor(c => c.TipoContenido).Must(t => Docs.Tipos.Contains(t)).WithMessage("Suba el documento en PDF o como imagen (JPG o PNG).");
        RuleFor(c => c.Restricciones).MaximumLength(500);
    }
}

internal sealed class RechazarDocumentoValidator : AbstractValidator<RechazarDocumentoCommand>
{
    public RechazarDocumentoValidator() =>
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Diga por qué rechaza el documento: el candidato lo verá.").MaximumLength(300);
}

internal sealed class SubirDocumentoCandidatoHandler(IProcesos procesos, ITokensEnlace tokens, IAlmacenDocumentos almacen, IEventosVinculacion eventos, IBitacora bitacora,
    IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<SubirDocumentoCandidatoCommand>
{
    public async Task<Result> Handle(SubirDocumentoCandidatoCommand c, CancellationToken cancellationToken)
    {
        var p = await procesos.PorTokenAsync(tokens.Huella(c.Token), cancellationToken);
        if (p is null || p.Etapa == EtapaVinculacion.Cancelado)
        {
            return Result.Failure(ProcesoErrors.EnlaceNoExiste);
        }

        var tipo = Docs.Tipo(c.Tipo);
        if (tipo.IsFailure)
        {
            return tipo;
        }

        var ahora = reloj.GetUtcNow();
        p.AsegurarDocumentos();
        var archivo = almacen.Guardar(p.EmpresaId, c.NombreArchivo, c.TipoContenido, c.Contenido, ahora);
        var d = p.SubirDocumento(tipo.Value, archivo, c.NombreArchivo, desdeEnlace: true, p.Persona.Nombre, ahora);
        if (d.IsFailure)
        {
            return d;
        }

        eventos.Publicar(Docs.Evento(p, d.Value, c.TipoContenido, ahora));
        bitacora.Registrar(p.EmpresaId, TipoActor.Titular, p.Persona.Nombre, $"Subió «{RequisitosDocumentales.Nombre(tipo.Value)}»", p.Codigo,
            "La IA lo lee y RRHH lo aprueba", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// P4 · La persona entrega desde «Mis documentos» de su portal uno que ya tenía. Sigue el mismo camino que la subida por enlace:
/// la IA lo lee y RRHH lo aprueba. El proceso debe ser de esa persona (cédula y correo).
/// </summary>
public sealed record EntregarDocumentoDesdePortalCommand(
    Guid ProcesoId, string Documento, string Correo, string Tipo, string NombreArchivo, string TipoContenido, byte[] Contenido) : ICommand;

internal sealed class EntregarDocumentoDesdePortalValidator : AbstractValidator<EntregarDocumentoDesdePortalCommand>
{
    public EntregarDocumentoDesdePortalValidator()
    {
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El archivo está vacío. Vuelve a subirlo en Mis documentos.")
            .Must(c => c.Length <= Docs.TamanoMaximo).WithMessage("El archivo pesa más de 10 MB. Sube un PDF más liviano o una foto.");
        RuleFor(c => c.TipoContenido).Must(t => Docs.Tipos.Contains(t)).WithMessage("Este documento no está en PDF ni en foto. Súbelo de nuevo en Mis documentos.");
    }
}

internal sealed class EntregarDocumentoDesdePortalHandler(IProcesos procesos, IAlmacenDocumentos almacen, IEventosVinculacion eventos, IBitacora bitacora,
    IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<EntregarDocumentoDesdePortalCommand>
{
    public async Task<Result> Handle(EntregarDocumentoDesdePortalCommand c, CancellationToken cancellationToken)
    {
        var p = await procesos.PorIdAsync(c.ProcesoId, cancellationToken);
        if (p is null || p.Etapa == EtapaVinculacion.Cancelado || p.Persona.Documento != c.Documento
            || !string.Equals(p.Persona.Correo.Trim(), c.Correo.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(ProcesoErrors.EnlaceNoExiste);
        }

        var tipo = Docs.Tipo(c.Tipo);
        if (tipo.IsFailure)
        {
            return tipo;
        }

        var ahora = reloj.GetUtcNow();
        p.AsegurarDocumentos();
        var archivo = almacen.Guardar(p.EmpresaId, c.NombreArchivo, c.TipoContenido, c.Contenido, ahora);
        var d = p.SubirDocumento(tipo.Value, archivo, c.NombreArchivo, desdeEnlace: true, p.Persona.Nombre, ahora);
        if (d.IsFailure)
        {
            return d;
        }

        eventos.Publicar(Docs.Evento(p, d.Value, c.TipoContenido, ahora));
        bitacora.Registrar(p.EmpresaId, TipoActor.Titular, p.Persona.Nombre, $"Entregó «{RequisitosDocumentales.Nombre(tipo.Value)}» desde su portal", p.Codigo,
            "Lo tenía en Mis documentos; la IA lo lee y RRHH lo aprueba", "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class SubirDocumentoConsolaHandler(IUsuarioActual usuario, IProcesos procesos, IAlmacenDocumentos almacen, ICifradoSensible cifrado, IEventosVinculacion eventos,
    IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj) : ICommandHandler<SubirDocumentoConsolaCommand>
{
    public async Task<Result> Handle(SubirDocumentoConsolaCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r;
        }

        var tipo = Docs.Tipo(c.Tipo);
        if (tipo.IsFailure)
        {
            return tipo;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        p.AsegurarDocumentos();
        if (p.Documento(tipo.Value) is { LoCargaEmpresa: true })
        {
            ResultadoAntecedentes? antecedentes = null;
            string? concepto = null;
            bool? revisar = null;
            if (tipo.Value == TipoDocumentoIngreso.Antecedentes)
            {
                antecedentes = Enum.TryParse<ResultadoAntecedentes>(c.Antecedentes, true, out var a) && Enum.IsDefined(a) ? a : null;
            }
            else if (Enum.TryParse<ConceptoMedico>(c.Concepto, true, out var m) && Enum.IsDefined(m))
            {
                // El concepto y las restricciones se guardan cifrados; afuera solo se sabe si pide revisión
                concepto = cifrado.Cifrar($"{m}|{c.Restricciones?.Trim()}");
                revisar = m != ConceptoMedico.Apto;
            }

            var archivo = almacen.Guardar(p.EmpresaId, c.NombreArchivo, c.TipoContenido, c.Contenido, ahora);
            var registro = p.RegistrarSoporteEmpresa(tipo.Value, archivo, c.NombreArchivo, antecedentes, concepto, revisar, usuario.Nombre, ahora);
            if (registro.IsFailure)
            {
                return registro;
            }

            bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Registró «{RequisitosDocumentales.Nombre(tipo.Value)}»", p.Codigo,
                tipo.Value == TipoDocumentoIngreso.ExamenMedico ? "Concepto médico cifrado; no pasa por la IA" : antecedentes?.ToString(),
                "ProcesoVinculacion", p.Id, datoSensible: tipo.Value == TipoDocumentoIngreso.ExamenMedico);
        }
        else
        {
            var archivo = almacen.Guardar(p.EmpresaId, c.NombreArchivo, c.TipoContenido, c.Contenido, ahora);
            var d = p.SubirDocumento(tipo.Value, archivo, c.NombreArchivo, desdeEnlace: false, usuario.Nombre, ahora);
            if (d.IsFailure)
            {
                return d;
            }

            eventos.Publicar(Docs.Evento(p, d.Value, c.TipoContenido, ahora));
            bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Subió «{RequisitosDocumentales.Nombre(tipo.Value)}» por el candidato", p.Codigo,
                "La IA lo lee y RRHH lo aprueba", "ProcesoVinculacion", p.Id);
        }

        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>La IA leyó un documento: las reglas deciden si es del titular; con todo lo del candidato leído, va a la bandeja.</summary>
internal sealed class DocumentoIngresoLeidoConsumer(IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<DocumentoIngresoLeido>
{
    public async Task Handle(DocumentoIngresoLeido e, CancellationToken cancellationToken)
    {
        var p = await procesos.PorIdAsync(e.ProcesoId, cancellationToken);
        if (p is null || !Enum.TryParse<TipoDocumentoIngreso>(e.Tipo, out var tipo))
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        var campos = e.Campos.Select(x => new CampoLeido { Campo = x.Campo, Valor = x.Valor }).ToList();
        var lectura = new LecturaDocumento
        {
            TipoDetectado = e.TipoDetectado,
            Legible = e.Error is null && e.Legible,
            TipoCorrecto = e.Error is null && e.TipoCorrecto,
            Campos = campos,
            Observaciones = e.Error is null ? [.. e.Observaciones] : [$"La IA no pudo leerlo: {e.Error}. Revíselo usted."],
            Razones = [.. e.Razones.Select(r => new RazonOferta { Signo = r.Signo, Texto = r.Texto, Fuente = r.Fuente })],
            CoincideTitular = RequisitosDocumentales.CoincideTitular(campos, p.Persona.Nombre, p.Persona.Documento),
            Modelo = e.Modelo,
            CostoUsd = e.CostoUsd,
            Error = e.Error,
            LeidoEn = ahora,
        };
        if (!p.RegistrarLectura(tipo, e.ArchivoId, lectura, ahora))
        {
            return;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Ia, "Lector de documentos", $"Leyó «{RequisitosDocumentales.Nombre(tipo)}»",
            $"{p.Codigo} · {(lectura.CoincideTitular == true ? "coincide con el titular" : lectura.CoincideTitular == false ? "NO coincide con el titular" : "sin datos para comparar")}",
            "Espera revisión de RRHH", "ProcesoVinculacion", p.Id);

        if (p.CandidatoCompleto)
        {
            var porRevisar = p.Documentos.Where(d => d.Estado == EstadoDocumento.PorRevisar).ToList();
            var limpios = p.SinObservaciones;
            eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Vinculacion", Docs.TipoBandeja, p.Id,
                $"Documentos de ingreso por revisar · {p.Persona.Nombre}",
                $"{p.Persona.Nombre} subió todos sus documentos obligatorios. La IA los leyó: {limpios.Count} sin observaciones y {porRevisar.Count - limpios.Count} para mirar con calma.",
                "Lector de documentos", e.Modelo,
                [.. porRevisar.Take(5).Select(d => new RazonPropuesta(d.Lectura is { Legible: true, TipoCorrecto: true, CoincideTitular: true } ? "+" : "−",
                    $"{RequisitosDocumentales.Nombre(d.Tipo)}: {(d.Lectura?.CoincideTitular == true ? "coincide con el titular" : d.Lectura?.Observaciones.FirstOrDefault() ?? "revíselo")}",
                    "Documento"))],
                [new CampoPropuesta("Pendientes de la empresa", string.Join(", ", p.Documentos.Where(d => d.LoCargaEmpresa && d.Estado != EstadoDocumento.Aprobado).Select(d => RequisitosDocumentales.Nombre(d.Tipo))) is { Length: > 0 } x ? x : "Ninguno")],
                limpios.Count > 0 ? new AccionPropuesta($"Aprobar {limpios.Count} sin observaciones", "POST", $"/api/v1/vinculacion/procesos/{p.Id}/documentos/aprobar-sin-observaciones") : null,
                $"/consola/vinculacion/{p.Id}", true, p.Condiciones.FechaIngreso));
        }

        await unidad.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class AprobarDocumentoHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<AprobarDocumentoCommand>
{
    public async Task<Result> Handle(AprobarDocumentoCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        var tipo = Docs.Tipo(c.Tipo);
        if (r.IsFailure || tipo.IsFailure)
        {
            return r.IsFailure ? r : tipo;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        var a = p.AprobarDocumento(tipo.Value, usuario.Nombre, ahora);
        if (a.IsFailure)
        {
            return a;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó «{RequisitosDocumentales.Nombre(tipo.Value)}»", p.Codigo, null, "ProcesoVinculacion", p.Id);
        DocumentosFlujo.SiTermino(p, eventos, bitacora, usuario.Nombre, ahora);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class RechazarDocumentoHandler(IUsuarioActual usuario, IProcesos procesos, IBitacora bitacora, IUnidadVinculacion unidad, TimeProvider reloj)
    : ICommandHandler<RechazarDocumentoCommand>
{
    public async Task<Result> Handle(RechazarDocumentoCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        var tipo = Docs.Tipo(c.Tipo);
        if (r.IsFailure || tipo.IsFailure)
        {
            return r.IsFailure ? r : tipo;
        }

        var p = r.Value;
        var rechazo = p.RechazarDocumento(tipo.Value, c.Motivo, usuario.Nombre, reloj.GetUtcNow());
        if (rechazo.IsFailure)
        {
            return rechazo;
        }

        bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Rechazó «{RequisitosDocumentales.Nombre(tipo.Value)}»", p.Codigo, c.Motivo.Trim(),
            "ProcesoVinculacion", p.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class AprobarSinObservacionesHandler(IUsuarioActual usuario, IProcesos procesos, IEventosVinculacion eventos, IBitacora bitacora, IUnidadVinculacion unidad,
    TimeProvider reloj) : ICommandHandler<AprobarSinObservacionesCommand, int>
{
    public async Task<Result<int>> Handle(AprobarSinObservacionesCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var p = r.Value;
        var ahora = reloj.GetUtcNow();
        var tipos = p.SinObservaciones.Select(d => d.Tipo).ToList();
        foreach (var t in tipos)
        {
            p.AprobarDocumento(t, usuario.Nombre, ahora);
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Docs.TipoBandeja, p.Id, $"Aprobó {tipos.Count} documentos sin observaciones", usuario.Nombre));
        if (tipos.Count > 0)
        {
            bitacora.Registrar(p.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó {tipos.Count} documentos de ingreso sin observaciones", p.Codigo,
                string.Join(", ", tipos.Select(RequisitosDocumentales.Nombre)), "ProcesoVinculacion", p.Id);
        }

        DocumentosFlujo.SiTermino(p, eventos, bitacora, usuario.Nombre, ahora);
        await unidad.SaveChangesAsync(cancellationToken);
        return tipos.Count;
    }
}

internal static class DocumentosFlujo
{
    // Al pasar a afiliaciones las afiliaciones y el alta ya quedaron prellenadas (AsegurarAfiliaciones)
    /// <summary>Si con esta aprobación el proceso pasó a afiliaciones, sale de la bandeja y queda en la bitácora.</summary>
    public static void SiTermino(ProcesoVinculacion p, IEventosVinculacion eventos, IBitacora bitacora, string por, DateTimeOffset ahora)
    {
        if (p.Etapa != EtapaVinculacion.Afiliaciones)
        {
            return;
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, p.EmpresaId, Docs.TipoBandeja, p.Id, "Documentos completos", por));
        bitacora.Registrar(p.EmpresaId, TipoActor.Sistema, "Vinculación", "Documentos de ingreso completos: sigue afiliaciones", p.Codigo, null, "ProcesoVinculacion", p.Id);
    }
}

internal sealed class AbrirDocumentoHandler(IUsuarioActual usuario, IProcesos procesos, IAlmacenDocumentos almacen, IBitacora bitacora, IUnidadVinculacion unidad)
    : ICommandHandler<AbrirDocumentoCommand, ArchivoDto>
{
    public async Task<Result<ArchivoDto>> Handle(AbrirDocumentoCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        var tipo = Docs.Tipo(c.Tipo);
        if (r.IsFailure || tipo.IsFailure)
        {
            return r.IsFailure ? r.Error : tipo.Error;
        }

        // El examen médico es dato sensible: tiene su propio permiso
        if (tipo.Value == TipoDocumentoIngreso.ExamenMedico || r.Value.Documento(tipo.Value) is not { ArchivoId: { } id })
        {
            return DocumentosErrors.SinArchivo;
        }

        if (await almacen.LeerAsync(id, cancellationToken) is not { } a)
        {
            return DocumentosErrors.SinArchivo;
        }

        bitacora.Registrar(r.Value.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Abrió «{RequisitosDocumentales.Nombre(tipo.Value)}»", r.Value.Codigo, null,
            "ProcesoVinculacion", r.Value.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return new ArchivoDto(a.Nombre, a.TipoContenido, a.Contenido);
    }
}

internal sealed class VerExamenMedicoHandler(IUsuarioActual usuario, IProcesos procesos, IAlmacenDocumentos almacen, ICifradoSensible cifrado, IBitacora bitacora,
    IUnidadVinculacion unidad) : ICommandHandler<VerExamenMedicoCommand, (ConceptoMedicoDto Concepto, ArchivoDto? Archivo)>
{
    public async Task<Result<(ConceptoMedicoDto Concepto, ArchivoDto? Archivo)>> Handle(VerExamenMedicoCommand c, CancellationToken cancellationToken)
    {
        var r = await Ofertas.DelUsuarioAsync(procesos, usuario, c.ProcesoId, cancellationToken);
        if (r.IsFailure)
        {
            return r.Error;
        }

        if (r.Value.Documento(TipoDocumentoIngreso.ExamenMedico) is not { ConceptoCifrado: { } concepto } d)
        {
            return DocumentosErrors.SinArchivo;
        }

        var partes = cifrado.Descifrar(concepto).Split('|', 2);
        ArchivoDto? archivo = null;
        if (c.ConArchivo && d.ArchivoId is { } id && await almacen.LeerAsync(id, cancellationToken) is { } a)
        {
            archivo = new ArchivoDto(a.Nombre, a.TipoContenido, a.Contenido);
        }

        bitacora.Registrar(r.Value.EmpresaId, TipoActor.Persona, usuario.Nombre, c.ConArchivo ? "Abrió el examen médico de ingreso" : "Vio el concepto médico de ingreso",
            r.Value.Codigo, "Dato sensible: acceso con permiso VerDatosSensibles", "ProcesoVinculacion", r.Value.Id, datoSensible: true);
        await unidad.SaveChangesAsync(cancellationToken);
        return (new ConceptoMedicoDto(partes[0], partes.Length > 1 && partes[1].Length > 0 ? partes[1] : null, d.RevisadoPor ?? "", d.RevisadoEn), archivo);
    }
}
