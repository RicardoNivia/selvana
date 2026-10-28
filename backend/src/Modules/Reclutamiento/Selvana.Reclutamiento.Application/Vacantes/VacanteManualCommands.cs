using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Empresas;
using Selvana.Core.Contracts.Estructura;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Vacantes;

/// <summary>
/// Una persona escribe la vacante en el formulario, sin pedírsela a la IA. Es de un solo cargo; <paramref name="Cupos"/>
/// son las posiciones disponibles. Hereda las pruebas psicotécnicas del cargo.
/// </summary>
public sealed record CrearVacanteCommand(
    Guid? EmpresaUsuariaId, Guid? CargoId, string Titulo, string Cargo, string Ciudad, string? CodigoMunicipio, int Cupos, string Jornada,
    string Descripcion, decimal SalarioMinimo, decimal SalarioMaximo, IReadOnlyList<string> Requisitos, IReadOnlyList<string> PreguntasFiltro,
    bool EmpresaConfidencial = false, bool SalarioOculto = false) : ICommand<Guid>;

/// <summary>Pide a la IA un borrador nuevo de una vacante en borrador, con otra frase (por ejemplo, la respuesta a sus aclaraciones).</summary>
public sealed record RedactarVacanteCommand(Guid VacanteId, string Frase) : ICommand;

internal sealed class CrearVacanteValidator : AbstractValidator<CrearVacanteCommand>
{
    public CrearVacanteValidator()
    {
        RuleFor(c => c.Titulo).NotEmpty().WithMessage("Escriba el título del aviso.").MaximumLength(200);
        RuleFor(c => c.Cargo).NotEmpty().WithMessage("Escriba o elija el cargo de la vacante.").MaximumLength(200);
        RuleFor(c => c.Ciudad).NotEmpty().WithMessage("Elija la ciudad o el municipio.").MaximumLength(100);
        RuleFor(c => c.CodigoMunicipio).Matches(@"^\d{5}$").When(c => !string.IsNullOrWhiteSpace(c.CodigoMunicipio))
            .WithMessage("El código DANE del municipio tiene 5 dígitos.");
        RuleFor(c => c.Cupos).InclusiveBetween(1, 10_000).WithMessage("Las posiciones disponibles deben estar entre 1 y 10.000.");
        RuleFor(c => c.Descripcion).NotEmpty().WithMessage("Escriba la descripción.").MaximumLength(4000);
        RuleFor(c => c.SalarioMinimo).GreaterThan(0).WithMessage("Escriba el salario mínimo del rango.");
        RuleFor(c => c.PreguntasFiltro).Must(p => p.Count <= 8).WithMessage("Use máximo 8 preguntas filtro.");
    }
}

internal sealed class RedactarVacanteValidator : AbstractValidator<RedactarVacanteCommand>
{
    public RedactarVacanteValidator() =>
        RuleFor(c => c.Frase).NotEmpty().WithMessage("Describa en una frase lo que necesita.").MaximumLength(1000);
}

internal sealed class CrearVacanteHandler(
    IUsuarioActual usuario, IDirectorioEmpresas empresas, ICatalogoCargos catalogo, IVacantes vacantes, IBateriasCargo baterias, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<CrearVacanteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearVacanteCommand c, CancellationToken cancellationToken)
    {
        var empresa = await empresas.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (empresa is null)
        {
            return Error.NotFound("Empresa.NoExiste", "La empresa no existe.");
        }

        EmpresaUsuariaResumen? usuaria = null;
        if (c.EmpresaUsuariaId is { } id && (usuaria = usuario.Ve(id) ? empresa.EmpresasUsuarias.FirstOrDefault(e => e.Id == id && e.Activa) : null) is null)
        {
            return OrigenVacante.EmpresaUsuariaInvalida;
        }

        CargoCatalogo? cargo = null;
        if (c.CargoId is { } cargoId && (cargo = await catalogo.ObtenerAsync(empresa.Id, cargoId, cancellationToken)) is null)
        {
            return OrigenVacante.CargoInvalido;
        }

        var nombreCargo = cargo?.Nombre ?? c.Cargo;
        var resultado = Vacante.CrearManual(empresa.Id, empresa.Nombre, usuaria?.Id, usuaria?.Nombre,
            new ContenidoVacante(c.Titulo, nombreCargo, c.Ciudad, c.Cupos, c.Jornada, c.Descripcion, c.SalarioMinimo, c.SalarioMaximo,
                c.Requisitos, c.PreguntasFiltro, c.CodigoMunicipio, c.EmpresaConfidencial, c.SalarioOculto),
            usuario.Nombre, reloj.GetUtcNow(), cargo?.Id);
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        var vacante = resultado.Value;
        var bateria = await baterias.ObtenerAsync(empresa.Id, Baterias.ClaveCargo(nombreCargo), cancellationToken);
        vacante.ConfigurarPruebas(bateria?.Pruebas ?? Baterias.Sugerir(nombreCargo).Pruebas);
        vacantes.Agregar(vacante);
        bitacora.Registrar(empresa.Id, TipoActor.Persona, usuario.Nombre, $"Creó a mano la vacante {vacante.Codigo}",
            $"{vacante.Titulo} · {vacante.Cupos} {(vacante.Cupos == 1 ? "posición" : "posiciones")} · {vacante.Ciudad}",
            cargo is null ? "Sin cargo del catálogo" : $"Del cargo {cargo.Nombre} del catálogo", "Vacante", vacante.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return vacante.Id;
    }
}

internal sealed class RedactarVacanteHandler(
    IUsuarioActual usuario, ICatalogoCargos catalogo, IVacantes vacantes, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<RedactarVacanteCommand>
{
    public async Task<Result> Handle(RedactarVacanteCommand c, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var resultado = vacante.PedirRedaccion(c.Frase);
        if (resultado.IsFailure)
        {
            return resultado;
        }

        var cargo = vacante.CargoId is { } cargoId ? await catalogo.ObtenerAsync(vacante.EmpresaId, cargoId, cancellationToken) : null;
        eventos.Publicar(new VacanteSolicitada(Guid.CreateVersion7(), reloj.GetUtcNow(), vacante.EmpresaId, vacante.Id, vacante.Frase,
            vacante.EmpresaUsuariaNombre, OrigenVacante.Perfil(cargo)));
        bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Pidió otro borrador de la vacante {vacante.Codigo} a la IA",
            $"«{vacante.Frase}»", null, "Vacante", vacante.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Reglas compartidas al crear una vacante: empresa usuaria y cargo del catálogo válidos, y el perfil que recibe la IA.</summary>
internal static class OrigenVacante
{
    public static readonly Error EmpresaUsuariaInvalida = Error.Validation("Vacante.EmpresaUsuaria", "Esa empresa usuaria no es de su empresa o está inactiva.");
    public static readonly Error CargoInvalido = Error.Validation("Vacante.Cargo", "Ese cargo no está en el catálogo o está inactivo.");

    /// <summary>Lo que el redactor recibe del cargo: el perfil si existe; si el cargo no tiene perfil, solo su nombre y área.</summary>
    public static PerfilCargoVacante? Perfil(CargoCatalogo? c) => c is null ? null
        : new PerfilCargoVacante(c.Nombre, c.Area, c.EstadoPerfil == "Aprobado", c.Mision, c.Funciones, c.Requisitos, c.RiesgoArl);
}
