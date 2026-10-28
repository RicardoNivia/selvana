using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Contracts.Empresas;
using Selvana.Core.Contracts.Estructura;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Vacantes;

/// <summary>
/// Vacante en un minuto: la persona escribe una frase y el agente redactor prepara el borrador.
/// Con un cargo del catálogo, el borrador parte de su perfil; sin cargo, se redacta desde cero.
/// </summary>
public sealed record SolicitarVacanteCommand(string Frase, Guid? EmpresaUsuariaId, Guid? CargoId = null) : ICommand<Guid>;

public sealed record EditarVacanteCommand(
    Guid VacanteId, string Titulo, string Cargo, string Ciudad, int Cupos, string Jornada, string Descripcion,
    decimal SalarioMinimo, decimal SalarioMaximo, IReadOnlyList<string> Requisitos, IReadOnlyList<string> PreguntasFiltro,
    string? CodigoMunicipio = null, bool EmpresaConfidencial = false, bool SalarioOculto = false) : ICommand;

/// <summary>Envía la requisición a aprobación (RRHH → gerencia); la última aprobación la publica en el portal.</summary>
public sealed record SolicitarAprobacionVacanteCommand(Guid VacanteId) : ICommand;

public sealed record AprobarRequisicionCommand(Guid VacanteId) : ICommand;

public sealed record DevolverRequisicionCommand(Guid VacanteId, string Motivo) : ICommand;

public sealed record CerrarVacanteCommand(Guid VacanteId) : ICommand;

internal sealed class SolicitarVacanteValidator : AbstractValidator<SolicitarVacanteCommand>
{
    public SolicitarVacanteValidator() =>
        RuleFor(c => c.Frase).NotEmpty().WithMessage("Describa en una frase lo que necesita.").MaximumLength(1000);
}

internal sealed class EditarVacanteValidator : AbstractValidator<EditarVacanteCommand>
{
    public EditarVacanteValidator()
    {
        RuleFor(c => c.Titulo).NotEmpty().WithMessage("Escriba el título del aviso.").MaximumLength(200);
        RuleFor(c => c.Ciudad).NotEmpty().WithMessage("Escriba la ciudad.").MaximumLength(100);
        RuleFor(c => c.Cupos).InclusiveBetween(1, 10_000).WithMessage("Los cupos deben estar entre 1 y 10.000.");
        RuleFor(c => c.Descripcion).NotEmpty().WithMessage("Escriba la descripción.").MaximumLength(4000);
        RuleFor(c => c.SalarioMinimo).GreaterThan(0).WithMessage("Escriba el salario mínimo del rango.");
        RuleFor(c => c.PreguntasFiltro).Must(p => p.Count <= 8).WithMessage("Use máximo 8 preguntas filtro.");
    }
}

internal sealed class SolicitarVacanteHandler(
    IUsuarioActual usuario, IDirectorioEmpresas empresas, ICatalogoCargos catalogo, IVacantes vacantes, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<SolicitarVacanteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SolicitarVacanteCommand command, CancellationToken cancellationToken)
    {
        var empresa = await empresas.ObtenerAsync(usuario.EmpresaId, cancellationToken);
        if (empresa is null)
        {
            return Error.NotFound("Empresa.NoExiste", "La empresa no existe.");
        }

        EmpresaUsuariaResumen? usuaria = null;
        if (command.EmpresaUsuariaId is { } id)
        {
            usuaria = usuario.Ve(id) ? empresa.EmpresasUsuarias.FirstOrDefault(e => e.Id == id && e.Activa) : null;
            if (usuaria is null)
            {
                return OrigenVacante.EmpresaUsuariaInvalida;
            }
        }

        CargoCatalogo? cargo = null;
        if (command.CargoId is { } cargoId && (cargo = await catalogo.ObtenerAsync(empresa.Id, cargoId, cancellationToken)) is null)
        {
            return OrigenVacante.CargoInvalido;
        }

        var ahora = reloj.GetUtcNow();
        var resultado = Vacante.Solicitar(empresa.Id, empresa.Nombre, usuaria?.Id, usuaria?.Nombre, command.Frase, usuario.Nombre, ahora, cargo?.Id, cargo?.Nombre);
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        var vacante = resultado.Value;
        vacantes.Agregar(vacante);
        eventos.Publicar(new VacanteSolicitada(Guid.CreateVersion7(), ahora, empresa.Id, vacante.Id, vacante.Frase, usuaria?.Nombre, OrigenVacante.Perfil(cargo)));
        bitacora.Registrar(empresa.Id, TipoActor.Persona, usuario.Nombre, $"Pidió la vacante {vacante.Codigo} a la IA", $"«{vacante.Frase}»{(usuaria is null ? "" : $" · {usuaria.Nombre}")}",
            cargo is null ? "Desde cero" : $"Desde el cargo {cargo.Nombre} del catálogo ({(cargo.EstadoPerfil == "Aprobado" ? "perfil aprobado" : cargo.EstadoPerfil == "BorradorIa" ? "perfil en borrador" : "sin perfil")})",
            "Vacante", vacante.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return vacante.Id;
    }
}

internal sealed class EditarVacanteHandler(IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IUnidadReclutamiento unidad) : ICommandHandler<EditarVacanteCommand>
{
    public async Task<Result> Handle(EditarVacanteCommand c, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var resultado = vacante.Editar(new ContenidoVacante(c.Titulo, c.Cargo, c.Ciudad, c.Cupos, c.Jornada, c.Descripcion,
            c.SalarioMinimo, c.SalarioMaximo, c.Requisitos, c.PreguntasFiltro, c.CodigoMunicipio, c.EmpresaConfidencial, c.SalarioOculto));
        if (resultado.IsSuccess)
        {
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Editó la vacante {vacante.Codigo}", vacante.Titulo,
                vacante.ModeloIa is null ? null : "Lo que cambió queda confirmado por una persona", "Vacante", vacante.Id);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }
}

internal sealed class DevolverRequisicionValidator : AbstractValidator<DevolverRequisicionCommand>
{
    public DevolverRequisicionValidator() =>
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Diga por qué devuelve la requisición, para que puedan ajustarla.").MaximumLength(500);
}

internal sealed class SolicitarAprobacionVacanteHandler(
    IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IEventosReclutamiento eventos, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<SolicitarAprobacionVacanteCommand>
{
    public async Task<Result> Handle(SolicitarAprobacionVacanteCommand command, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(command.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var estabaEnBorrador = vacante.Estado == EstadoVacante.Borrador;
        var ahora = reloj.GetUtcNow();
        var resultado = vacante.SolicitarAprobacion(usuario.Nombre, usuario.Rol, ahora);
        if (resultado.IsFailure || !estabaEnBorrador)
        {
            return resultado;
        }

        var automaticas = vacante.Aprobaciones.Where(a => a.Automatica).Select(a => FlujoRequisicion.Nombre(a.Etapa)).ToList();
        bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Envió la requisición {vacante.Codigo} a aprobación",
            $"{vacante.Titulo} · {vacante.Ciudad} · {vacante.Cupos} cupos",
            automaticas.Count == 0 ? null : $"Aprobada por su rol en: {string.Join(", ", automaticas)}", "Vacante", vacante.Id);
        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, vacante.EmpresaId, BandejaRequisicion.TipoBorrador, vacante.Id,
            vacante.EditadaPorPersona ? "Enviada a aprobación con ajustes" : "Enviada a aprobación como la propuso la IA", usuario.Nombre));
        BandejaRequisicion.Avisar(vacante, eventos, bitacora, usuario.Nombre, ahora);
        await unidad.SaveChangesAsync(cancellationToken);
        return resultado;
    }
}

internal sealed class AprobarRequisicionHandler(
    IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IEventosReclutamiento eventos, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<AprobarRequisicionCommand>
{
    public async Task<Result> Handle(AprobarRequisicionCommand command, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(command.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var etapa = vacante.EtapaPendiente;
        var ahora = reloj.GetUtcNow();
        var resultado = vacante.Aprobar(usuario.Nombre, usuario.Rol, ahora);
        if (resultado.IsSuccess)
        {
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre,
                $"Aprobó la requisición {vacante.Codigo} por {FlujoRequisicion.Nombre(etapa!.Value)}", vacante.Titulo, null, "Vacante", vacante.Id);
            BandejaRequisicion.Avisar(vacante, eventos, bitacora, usuario.Nombre, ahora);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }
}

internal sealed class DevolverRequisicionHandler(
    IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IEventosReclutamiento eventos, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<DevolverRequisicionCommand>
{
    public async Task<Result> Handle(DevolverRequisicionCommand command, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(command.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var etapa = vacante.EtapaPendiente;
        var ahora = reloj.GetUtcNow();
        var resultado = vacante.Devolver(usuario.Nombre, usuario.Rol, command.Motivo, ahora);
        if (resultado.IsSuccess)
        {
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre,
                $"Devolvió la requisición {vacante.Codigo} desde {FlujoRequisicion.Nombre(etapa!.Value)}", vacante.Titulo, $"Motivo: {vacante.MotivoDevolucion}",
                "Vacante", vacante.Id);
            eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, vacante.EmpresaId, BandejaRequisicion.Tipo, vacante.Id,
                $"Devuelta: {vacante.MotivoDevolucion}", usuario.Nombre));
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }
}

/// <summary>Qué ve la bandeja de la requisición: la etapa pendiente solo para quienes la aprueban; publicada, sale de la bandeja.</summary>
internal static class BandejaRequisicion
{
    public const string Tipo = "Requisicion";
    public const string TipoBorrador = "VacanteRedactada";

    private static readonly System.Globalization.CultureInfo Pesos = new("es-CO");

    public static void Avisar(Vacante vacante, IEventosReclutamiento eventos, IBitacora bitacora, string por, DateTimeOffset ahora)
    {
        if (vacante.Estado == EstadoVacante.Publicada)
        {
            eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, vacante.EmpresaId, Tipo, vacante.Id, "Aprobada y publicada en el portal", por));
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Sistema, "Flujo de requisiciones", $"Publicó la vacante {vacante.Codigo} en el portal",
                $"{vacante.Titulo} · {vacante.Ciudad} · {vacante.Cupos} cupos",
                $"Aprobada por {string.Join(" y ", vacante.Aprobaciones.Select(a => $"{FlujoRequisicion.Nombre(a.Etapa)} ({a.Por})"))}", "Vacante", vacante.Id);
            return;
        }

        if (vacante.EtapaPendiente is not { } etapa)
        {
            return;
        }

        var salario = vacante.SalarioMaximo > vacante.SalarioMinimo
            ? $"{vacante.SalarioMinimo.ToString("C0", Pesos)} – {vacante.SalarioMaximo.ToString("C0", Pesos)}"
            : vacante.SalarioMinimo.ToString("C0", Pesos);
        var anteriores = vacante.Aprobaciones.Select(a => new CampoPropuesta($"Aprobó {FlujoRequisicion.Nombre(a.Etapa)}", a.Automatica ? $"{a.Por} (pidió la vacante)" : a.Por));
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, vacante.EmpresaId, vacante.EmpresaUsuariaId, "Reclutamiento", Tipo, vacante.Id,
            $"Requisición por aprobar · {FlujoRequisicion.Nombre(etapa)} · {vacante.Titulo}",
            $"{vacante.Ciudad} · {vacante.Cupos} {(vacante.Cupos == 1 ? "cupo" : "cupos")} · la pidió {vacante.AprobacionPedidaPor}",
            null, null, [],
            [new CampoPropuesta("Cargo", vacante.Cargo), new CampoPropuesta("Rango salarial", salario), new CampoPropuesta("Jornada", vacante.Jornada), .. anteriores],
            new AccionPropuesta(etapa == FlujoRequisicion.Etapas[^1] ? "Aprobar y publicar" : "Aprobar requisición", "POST",
                $"/api/v1/reclutamiento/vacantes/{vacante.Id}/aprobar"),
            $"/consola/vacantes/{vacante.Id}", false, null, FlujoRequisicion.QuienesAprueban(etapa)));
    }
}

internal sealed class CerrarVacanteHandler(IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IEventosReclutamiento eventos, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<CerrarVacanteCommand>
{
    public async Task<Result> Handle(CerrarVacanteCommand command, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerAsync(command.VacanteId, cancellationToken);
        if (vacante is null || vacante.EmpresaId != usuario.EmpresaId || !usuario.Ve(vacante.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var resultado = vacante.Cerrar(reloj.GetUtcNow());
        if (resultado.IsSuccess)
        {
            bitacora.Registrar(vacante.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Cerró la vacante {vacante.Codigo}", vacante.Titulo, null, "Vacante", vacante.Id);
            eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), reloj.GetUtcNow(), vacante.EmpresaId, "VacanteRedactada", vacante.Id, "Vacante cerrada", usuario.Nombre));
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }
}
