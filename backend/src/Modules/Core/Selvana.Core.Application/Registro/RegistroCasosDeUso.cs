using FluentValidation;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Sesion;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Empresas;
using Selvana.Core.Domain.Empresas;
using Selvana.Core.Domain.Usuarios;
using Selvana.Expediente.Contracts;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Application.Registro;

/// <summary>
/// Sprint 10 · Autoservicio de alta: la empresa se registra sola (razón social, NIT, sector y tamaño), quien la registra queda como administrador
/// y arranca una prueba gratuita de 30 días. Devuelve la sesión para entrar de una vez.
/// </summary>
public sealed record RegistrarEmpresaCommand(
    string Empresa, string Nit, bool EsTemporal, string Sector, string Tamano, string Nombre, string Correo, string Contrasena, bool AceptaTerminos)
    : ICommand<SesionDto>;

public static class RegistroErrors
{
    public static readonly Error CorreoEnUso = Error.Conflict("Registro.CorreoEnUso",
        "Ese correo ya entra a Selvana. Inicie sesión con él o use otro correo para la empresa nueva.");
}

internal sealed class RegistrarEmpresaValidator : AbstractValidator<RegistrarEmpresaCommand>
{
    public RegistrarEmpresaValidator()
    {
        RuleFor(c => c.Empresa).NotEmpty().WithMessage("Escriba la razón social.").MaximumLength(200);
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escriba su nombre.").MaximumLength(200);
        RuleFor(c => c.Correo).NotEmpty().EmailAddress().WithMessage("Escriba un correo válido, por ejemplo nombre@empresa.co.");
        RuleFor(c => c.Contrasena).Must(Usuario.ContrasenaAceptable).WithMessage("La contraseña necesita al menos 10 caracteres, con letras y números.");
        RuleFor(c => c.AceptaTerminos).Equal(true).WithMessage("Para crear la cuenta acepte los términos y la política de tratamiento de datos.");
        RuleFor(c => c.Sector).MaximumLength(100);
        RuleFor(c => c.Tamano).MaximumLength(50);
    }
}

internal sealed class RegistrarEmpresaHandler(IEmpresas empresas, IUsuarios usuarios, IHasherContrasenas hasher, ILecturaSesion lectura, IBitacora bitacora,
    IEventosSalientes eventos, IUnitOfWork unidad, TimeProvider reloj) : ICommandHandler<RegistrarEmpresaCommand, SesionDto>
{
    public async Task<Result<SesionDto>> Handle(RegistrarEmpresaCommand c, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var empresa = Empresa.Registrar(Guid.CreateVersion7(), c.Empresa, c.Nit, c.EsTemporal, c.Sector, c.Tamano, ahora);
        if (empresa.IsFailure)
        {
            return empresa.Error;
        }

        if (await empresas.ExisteNitAsync(empresa.Value.Nit, cancellationToken))
        {
            return EmpresaErrors.YaRegistrada;
        }

        // Un correo, una cuenta: así el ingreso nunca duda a qué empresa entrar
        if ((await usuarios.PorCorreoAsync(Usuario.NormalizarCorreo(c.Correo), cancellationToken)).Count > 0)
        {
            return RegistroErrors.CorreoEnUso;
        }

        var dueno = Usuario.RegistrarDueno(empresa.Value.Id, c.Correo, c.Nombre, hasher.Calcular(c.Contrasena), ahora);
        if (dueno.IsFailure)
        {
            return dueno.Error;
        }

        dueno.Value.RegistrarIngreso(ahora);
        empresas.Agregar(empresa.Value);
        usuarios.Agregar(dueno.Value);
        eventos.Publicar(new EmpresaRegistrada(Guid.CreateVersion7(), ahora, empresa.Value.Id, empresa.Value.Nombre, empresa.Value.EsTemporal));
        await bitacora.RegistrarAsync(empresa.Value.Id, TipoActor.Persona, dueno.Value.Nombre, "Registró la empresa en Selvana",
            $"{empresa.Value.Nombre} · NIT {empresa.Value.Nit}", $"Prueba gratuita de {Empresa.DiasPrueba} días, hasta el {empresa.Value.PruebaHasta:dd/MM/yyyy}",
            "Empresa", empresa.Value.Id, cancellationToken: cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);

        var sesion = await lectura.ObtenerAsync(dueno.Value.Id, cancellationToken);
        return sesion is null ? EmpresaErrors.NoExiste : sesion;
    }
}

/* ---------- Recorrido guiado: primeros pasos de una empresa nueva ---------- */

public sealed record PasoInicialDto(string Clave, string Titulo, string Detalle, string Enlace, bool Hecho);

public sealed record PrimerosPasosDto(IReadOnlyList<PasoInicialDto> Pasos, int Hechos, int? DiasDePrueba, DateTimeOffset? PruebaHasta);

/// <summary>Los primeros pasos de la empresa, cada uno marcado como hecho según los datos (no hay que marcarlo a mano).</summary>
public sealed record ObtenerPrimerosPasosQuery : IQuery<PrimerosPasosDto>;

/// <summary>Conteos propios de Core para el recorrido guiado.</summary>
public interface ILecturaPrimerosPasos
{
    Task<(int Cargos, int Usuarios)> ConteosAsync(Guid empresaId, CancellationToken cancellationToken);
}

internal sealed class ObtenerPrimerosPasosHandler(IContextoUsuario usuario, IEmpresas empresas, ILecturaPrimerosPasos lectura, IResumenReclutamiento reclutamiento,
    IPlantaActiva planta, TimeProvider reloj) : IQueryHandler<ObtenerPrimerosPasosQuery, PrimerosPasosDto>
{
    public async Task<Result<PrimerosPasosDto>> Handle(ObtenerPrimerosPasosQuery q, CancellationToken ct)
    {
        var empresa = await empresas.ObtenerAsync(usuario.EmpresaId, ct);
        if (empresa is null)
        {
            return EmpresaErrors.NoExiste;
        }

        var (cargos, personas) = await lectura.ConteosAsync(empresa.Id, ct);
        var r = await reclutamiento.ResumenAsync(empresa.Id, ct);
        var empleados = (await planta.ListarAsync(empresa.Id, ct)).Count;
        PasoInicialDto[] pasos =
        [
            new("estructura", "Arme su estructura", "Sedes, áreas y cargos: cárguelos de la nómina o créelos a mano; la IA propone el perfil de cada cargo.",
                "/consola/empresa/estructura", cargos > 0),
            new("planta", "Cargue su planta", "Suba el archivo de empleados de la nómina: Selvana arma el expediente de cada persona.", "/consola/empleados", empleados > 0),
            new("equipo", "Invite a su equipo", "RRHH, analistas y líderes, cada uno con su rol y sus permisos.", "/consola/empresa/usuarios", personas > 1),
            new("vacante", "Cree su primera vacante", "Escríbala en una frase y la IA la redacta; usted la revisa y la aprueba.", "/consola/vacantes", r.Vacantes > 0),
            new("postulantes", "Reciba postulantes", "Publíquela y comparta el enlace del portal de empleo: la IA lee cada hoja de vida y la califica.",
                "/consola/vacantes", r.Postulaciones > 0),
        ];
        var ahora = reloj.GetUtcNow();
        return new PrimerosPasosDto(pasos, pasos.Count(p => p.Hecho), empresa.DiasDePrueba(ahora), empresa.PruebaHasta);
    }
}
