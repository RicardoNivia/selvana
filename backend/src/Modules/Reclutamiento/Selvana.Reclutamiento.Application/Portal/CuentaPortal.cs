using FluentValidation;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.Reclutamiento.Domain.Postulaciones;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Portal;

/// <summary>La persona con sesión en el portal (ADR 0013); null si no ha entrado.</summary>
public interface IPersonaActual
{
    Guid? CuentaId { get; }
}

public interface ICuentasCandidato
{
    Task<CuentaCandidato?> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    Task<CuentaCandidato?> ObtenerPorCorreoAsync(string correo, CancellationToken cancellationToken);

    Task<bool> ExisteDocumentoAsync(string tipoDocumento, string documento, CancellationToken cancellationToken);

    void Agregar(CuentaCandidato cuenta);
}

/// <summary>Contraseñas del portal: PBKDF2, igual que la consola.</summary>
public interface IHasherClavesPortal
{
    string Calcular(string contrasena);

    bool Verificar(string? hash, string contrasena);
}

/// <summary>Autorización Ley 1581 del perfil del portal: el responsable es Selvana; cada empresa recibe la suya al postularse.</summary>
public static class PoliticaDatosPortal
{
    public const string Version = "portal-v1";

    public const string Texto =
        "Autorizo a Selvana a tratar mis datos personales (hoja de vida y datos de contacto) para crear mi perfil, mostrarme vacantes " +
        "que encajen conmigo, permitirme postularme y ver el estado de mis procesos, según la Ley 1581 de 2012. Al postularme a una " +
        "vacante autorizo también a la empresa que la publica. Puedo consultar, actualizar o pedir que se supriman mis datos en cualquier momento.";
}

/// <summary>El portal reúne la hoja de vida leída: experiencia, estudios, cursos y habilidades.</summary>
public sealed record DatosPerfilDto(
    string Nombre, string Documento, string Celular, string Correo, string Ciudad, string LinkedIn,
    IReadOnlyList<ExperienciaDto> Experiencia, IReadOnlyList<FormacionDto> Formacion, IReadOnlyList<string> Cursos, IReadOnlyList<string> Habilidades);

public sealed record HojaDeVidaPerfilDto(Guid Id, string Estado, string? Mensaje, string NombreArchivo, DatosPerfilDto? Datos, string VersionAutorizacion, string TextoAutorizacion);

public sealed record MiCuentaDto(Guid Id, string Correo, string TipoDocumento, DatosPerfilDto Datos, string? NombreArchivoHojaDeVida, DateTimeOffset? HojaDeVidaActualizadaEn,
    string VersionAutorizacion, DateTimeOffset AutorizadaEn, DateTimeOffset CreadaEn);

public sealed record SesionPortal(Guid CuentaId, string Nombre);

/// <summary>Lo que la persona confirma o corrige de sus datos.</summary>
public sealed record DatosEditables(string Nombre, string Celular, string Ciudad, string? LinkedIn);

/// <summary>Ya con cuenta, sube su hoja de vida y la IA la lee, igual que en la postulación.</summary>
public sealed record SubirHojaDeVidaPerfilCommand(string NombreArchivo, string TipoContenido, byte[] Contenido) : ICommand<Guid>;

public sealed record ObtenerHojaDeVidaPerfilQuery(Guid BorradorId) : IQuery<HojaDeVidaPerfilDto>;

/// <summary>Registro con lo mínimo: correo, nombre, tipo y número de identificación, contraseña y autorización. Queda con sesión.</summary>
public sealed record RegistrarCuentaCommand(
    string Correo, string Contrasena, string Nombre, string TipoDocumento, string NumeroDocumento, bool AceptaAutorizacion, string Ip, string Navegador)
    : ICommand<SesionPortal>;

public sealed record IngresarCuentaCommand(string Correo, string Contrasena) : ICommand<SesionPortal>;

public sealed record ObtenerMiCuentaQuery : IQuery<MiCuentaDto>;

public sealed record ActualizarMisDatosCommand(DatosEditables Datos) : ICommand;

/// <summary>Reemplaza la hoja de vida con una nueva ya leída por la IA y lo que la persona confirmó de ella.</summary>
public sealed record ActualizarMiHojaDeVidaCommand(Guid BorradorId, DatosEditables Datos) : ICommand;

internal sealed class SubirHojaDeVidaPerfilValidator : AbstractValidator<SubirHojaDeVidaPerfilCommand>
{
    public SubirHojaDeVidaPerfilValidator() => RuleFor(c => c.Contenido).Must(c => c.Length is > 0 and <= 5 * 1024 * 1024)
        .WithMessage("Sube tu hoja de vida en PDF, Word o imagen, de máximo 5 MB.");
}

internal sealed class DatosEditablesValidator : AbstractValidator<DatosEditables>
{
    public DatosEditablesValidator()
    {
        RuleFor(d => d.Nombre).NotEmpty().WithMessage("Escribe tu nombre completo.").MaximumLength(200);
        RuleFor(d => d.Celular).MaximumLength(30);
        RuleFor(d => d.Ciudad).MaximumLength(100);
    }
}

internal sealed class RegistrarCuentaValidator : AbstractValidator<RegistrarCuentaCommand>
{
    public RegistrarCuentaValidator()
    {
        RuleFor(c => c.Correo).NotEmpty().WithMessage("Escribe tu correo.").MaximumLength(200);
        RuleFor(c => c.Contrasena).MinimumLength(8).WithMessage("Tu contraseña debe tener al menos 8 caracteres.").MaximumLength(200);
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escribe tu nombre completo.").MaximumLength(200);
        RuleFor(c => c.TipoDocumento).NotEmpty().WithMessage("Elige el tipo de identificación.");
        RuleFor(c => c.NumeroDocumento).NotEmpty().WithMessage("Escribe tu número de identificación.").MaximumLength(30);
    }
}

internal sealed class ActualizarMisDatosValidator : AbstractValidator<ActualizarMisDatosCommand>
{
    public ActualizarMisDatosValidator() => RuleFor(c => c.Datos).SetValidator(new DatosEditablesValidator());
}

internal sealed class ActualizarMiHojaDeVidaValidator : AbstractValidator<ActualizarMiHojaDeVidaCommand>
{
    public ActualizarMiHojaDeVidaValidator() => RuleFor(c => c.Datos).SetValidator(new DatosEditablesValidator());
}

/// <summary>El perfil no es de una vacante ni de una empresa: el borrador va sin ninguna (Guid vacío).</summary>
internal static class HojaDeVidaPerfil
{
    public static readonly Guid SinEmpresa = Guid.Empty;

    public static DatosPerfilDto Dto(DatosCandidato d) => new(d.Nombre, d.Documento, d.Celular, d.Correo, d.Ciudad, d.LinkedIn,
        [.. d.Experiencia.Select(x => new ExperienciaDto(x.Cargo, x.Empresa, x.Desde, x.Hasta))],
        [.. d.Formacion.Select(x => new FormacionDto(x.Titulo, x.Institucion, x.Anio))], d.Cursos, d.Habilidades);

    /// <summary>Lo leído de la hoja de vida, con lo que la persona confirmó o corrigió encima.</summary>
    public static DatosCandidato Combinar(DatosCandidato? leidos, string documento, string correo, DatosEditables e) => new()
    {
        Nombre = e.Nombre,
        Documento = documento,
        Celular = e.Celular,
        Correo = correo,
        Ciudad = e.Ciudad,
        LinkedIn = e.LinkedIn ?? leidos?.LinkedIn ?? "",
        Experiencia = leidos?.Experiencia ?? [],
        Formacion = leidos?.Formacion ?? [],
        Cursos = leidos?.Cursos ?? [],
        Habilidades = leidos?.Habilidades ?? [],
    };

    /// <summary>Un borrador de perfil vigente y ya leído (o que falló y la persona escribe sus datos).</summary>
    public static async Task<Result<BorradorPostulacion>> ObtenerLeidoAsync(IBorradoresPostulacion borradores, Guid id, DateTimeOffset ahora, CancellationToken ct)
    {
        var b = await borradores.ObtenerAsync(id, ct);
        if (b is null || b.VacanteId != Guid.Empty || b.Vencido(ahora))
        {
            return BorradorErrors.NoExiste;
        }

        return b.Estado == EstadoLectura.Leyendo ? BorradorErrors.Leyendo : b;
    }
}

internal sealed class SubirHojaDeVidaPerfilHandler(
    IBorradoresPostulacion borradores, IAlmacenHojasDeVida almacen, IEventosReclutamiento eventos, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<SubirHojaDeVidaPerfilCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SubirHojaDeVidaPerfilCommand c, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var nombre = Path.GetFileName(c.NombreArchivo);
        var archivoId = almacen.Guardar(HojaDeVidaPerfil.SinEmpresa, nombre, c.TipoContenido, c.Contenido, ahora);
        var borrador = BorradorPostulacion.Crear(Guid.Empty, HojaDeVidaPerfil.SinEmpresa, archivoId, nombre, ahora);
        borradores.Agregar(borrador);
        eventos.Publicar(new HojaDeVidaRecibida(Guid.CreateVersion7(), ahora, HojaDeVidaPerfil.SinEmpresa, borrador.Id, archivoId, nombre,
            c.TipoContenido, "su perfil en el portal de empleo"));
        await unidad.SaveChangesAsync(cancellationToken);
        return borrador.Id;
    }
}

internal sealed class ObtenerHojaDeVidaPerfilHandler(IBorradoresPostulacion borradores, TimeProvider reloj)
    : IQueryHandler<ObtenerHojaDeVidaPerfilQuery, HojaDeVidaPerfilDto>
{
    public async Task<Result<HojaDeVidaPerfilDto>> Handle(ObtenerHojaDeVidaPerfilQuery query, CancellationToken cancellationToken)
    {
        var b = await borradores.ObtenerAsync(query.BorradorId, cancellationToken);
        if (b is null || b.VacanteId != Guid.Empty || b.Vencido(reloj.GetUtcNow()))
        {
            return BorradorErrors.NoExiste;
        }

        return new HojaDeVidaPerfilDto(b.Id, b.Estado.ToString(), b.Mensaje, b.NombreArchivo, b.DatosLeidos is { } d ? HojaDeVidaPerfil.Dto(d) : null,
            PoliticaDatosPortal.Version, PoliticaDatosPortal.Texto);
    }
}

internal sealed class RegistrarCuentaHandler(ICuentasCandidato cuentas, IHasherClavesPortal hasher, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<RegistrarCuentaCommand, SesionPortal>
{
    public async Task<Result<SesionPortal>> Handle(RegistrarCuentaCommand c, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var correo = CuentaCandidato.NormalizarCorreo(c.Correo);
        if (await cuentas.ObtenerPorCorreoAsync(correo, cancellationToken) is not null)
        {
            return CuentaErrors.YaExiste;
        }

        // Una identificación, una cuenta: si ya existe, por ahora la recupera soporte
        var tipo = c.TipoDocumento.Trim().ToUpperInvariant();
        var documento = CuentaCandidato.NormalizarDocumento(tipo, c.NumeroDocumento);
        if (await cuentas.ExisteDocumentoAsync(tipo, documento, cancellationToken))
        {
            return CuentaErrors.DocumentoYaRegistrado;
        }

        var autorizacion = c.AceptaAutorizacion
            ? new AutorizacionDatos
            {
                Version = PoliticaDatosPortal.Version,
                Texto = PoliticaDatosPortal.Texto,
                AceptadaEn = ahora,
                Ip = c.Ip,
                Navegador = c.Navegador.Length <= 300 ? c.Navegador : c.Navegador[..300],
                Huella = AutorizacionDatos.CalcularHuella(PoliticaDatosPortal.Version, PoliticaDatosPortal.Texto, ahora, c.Ip, c.Navegador, documento),
            }
            : null;

        var cuenta = CuentaCandidato.Registrar(correo, hasher.Calcular(c.Contrasena), c.Nombre, tipo, c.NumeroDocumento, autorizacion, ahora);
        if (cuenta.IsFailure)
        {
            return cuenta.Error;
        }

        cuenta.Value.Ingresar(contrasenaValida: true, ahora);
        cuentas.Agregar(cuenta.Value);
        await unidad.SaveChangesAsync(cancellationToken);
        return new SesionPortal(cuenta.Value.Id, cuenta.Value.Datos.Nombre);
    }
}

internal sealed class IngresarCuentaHandler(ICuentasCandidato cuentas, IHasherClavesPortal hasher, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<IngresarCuentaCommand, SesionPortal>
{
    public async Task<Result<SesionPortal>> Handle(IngresarCuentaCommand c, CancellationToken cancellationToken)
    {
        var cuenta = await cuentas.ObtenerPorCorreoAsync(CuentaCandidato.NormalizarCorreo(c.Correo), cancellationToken);

        // Sin cuenta se calcula igual, para no revelar por el tiempo de respuesta qué correos existen
        var valida = hasher.Verificar(cuenta?.ContrasenaHash, c.Contrasena);
        if (cuenta is null)
        {
            return CuentaErrors.CredencialesInvalidas;
        }

        var r = cuenta.Ingresar(valida, reloj.GetUtcNow());
        await unidad.SaveChangesAsync(cancellationToken);
        return r.IsSuccess ? new SesionPortal(cuenta.Id, cuenta.Datos.Nombre) : r.Error;
    }
}

internal sealed class ObtenerMiCuentaHandler(IPersonaActual persona, ICuentasCandidato cuentas) : IQueryHandler<ObtenerMiCuentaQuery, MiCuentaDto>
{
    public async Task<Result<MiCuentaDto>> Handle(ObtenerMiCuentaQuery query, CancellationToken cancellationToken)
    {
        var c = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        return c is null
            ? CuentaErrors.NoExiste
            : new MiCuentaDto(c.Id, c.Correo, c.TipoDocumento, HojaDeVidaPerfil.Dto(c.Datos), c.ArchivoHojaDeVidaId is null ? null : c.NombreArchivoHojaDeVida,
                c.HojaDeVidaActualizadaEn, c.Autorizacion.Version, c.Autorizacion.AceptadaEn, c.CreadaEn);
    }
}

internal sealed class ActualizarMisDatosHandler(IPersonaActual persona, ICuentasCandidato cuentas, IUnidadReclutamiento unidad)
    : ICommandHandler<ActualizarMisDatosCommand>
{
    public async Task<Result> Handle(ActualizarMisDatosCommand c, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return Result.Failure(CuentaErrors.NoExiste);
        }

        cuenta.ActualizarDatos(HojaDeVidaPerfil.Combinar(cuenta.Datos, cuenta.Documento, cuenta.Correo, c.Datos));
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ActualizarMiHojaDeVidaHandler(
    IPersonaActual persona, ICuentasCandidato cuentas, IBorradoresPostulacion borradores, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<ActualizarMiHojaDeVidaCommand>
{
    public async Task<Result> Handle(ActualizarMiHojaDeVidaCommand c, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return Result.Failure(CuentaErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var b = await HojaDeVidaPerfil.ObtenerLeidoAsync(borradores, c.BorradorId, ahora, cancellationToken);
        if (b.IsFailure)
        {
            return Result.Failure(b.Error);
        }

        cuenta.ActualizarHojaDeVida(b.Value.ArchivoId, b.Value.NombreArchivo,
            HojaDeVidaPerfil.Combinar(b.Value.DatosLeidos, cuenta.Documento, cuenta.Correo, c.Datos), ahora);
        borradores.Quitar(b.Value);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
