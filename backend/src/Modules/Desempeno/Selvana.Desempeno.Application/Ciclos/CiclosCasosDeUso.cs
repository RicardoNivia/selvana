using FluentValidation;
using Selvana.AI.Contracts.Desempeno;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Desempeno.Application.Abstracciones;
using Selvana.Desempeno.Contracts.Ia;
using Selvana.Desempeno.Domain.Ciclos;
using Selvana.Desempeno.Domain.Evaluaciones;
using Selvana.Expediente.Contracts;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Application.Ciclos;

/* ---------- DTO ---------- */

public sealed record RazonDto(string Signo, string Texto, string Fuente);

public sealed record CompetenciaDto(string Nombre, string Descripcion, int Peso);

public sealed record ObjetivoBaseDto(string Descripcion, string Meta, int Peso);

public sealed record FormularioDto(
    string Cargo, int Personas, IReadOnlyList<CompetenciaDto> Competencias, IReadOnlyList<ObjetivoBaseDto> Objetivos, string Origen, IReadOnlyList<RazonDto> Razones,
    string? Modelo, bool Preparando, string? Error, string? AprobadoPor, DateTimeOffset? AprobadoEn);

public sealed record AlertaSesgoDto(string Tipo, string Texto, string Evidencia);

public sealed record PersonaMatrizDto(Guid EvaluacionId, string Nombre, string Cargo, decimal Puntaje, bool Calibrada);

/// <summary>Una caja del 9-box con quiénes están (1 = bajo potencial y bajo desempeño … 9 = alto y alto).</summary>
public sealed record CajaMatrizDto(int Caja, string Nombre, IReadOnlyList<PersonaMatrizDto> Personas);

public sealed record CicloDto(
    Guid Id, string Nombre, string Tipo, string Alcance, DateOnly Desde, DateOnly Hasta, int PesoObjetivos, string Estado, IReadOnlyList<FormularioDto> Formularios,
    IReadOnlyDictionary<string, int> Conteo, IReadOnlyList<AlertaSesgoDto> Sesgos, IReadOnlyList<CajaMatrizDto> Matriz, decimal? Promedio, string CreadoPor,
    DateTimeOffset CreadoEn);

/* ---------- Consultas y comandos ---------- */

public sealed record ListarCiclosQuery(int? Pagina, int? Tamano) : IQuery<Pagina<FilaCicloDto>>;

public sealed record ObtenerCicloQuery(Guid Id) : IQuery<CicloDto>;

/// <summary>Crea el ciclo con un formulario por cada cargo de la planta activa (sin contenido todavía).</summary>
public sealed record CrearCicloCommand(string Nombre, string Tipo, string Alcance, DateOnly Desde, DateOnly Hasta, int PesoObjetivos) : ICommand<Guid>;

/// <summary>La IA propone el formulario de un cargo (o de todos los que aún no tienen contenido si <see cref="Cargo"/> es null).</summary>
public sealed record PedirFormularioIaCommand(Guid CicloId, string? Cargo) : ICommand<int>;

/// <summary>Camino manual: la plantilla base por reglas para un cargo (o para todos los que aún no tienen contenido).</summary>
public sealed record UsarPlantillaBaseCommand(Guid CicloId, string? Cargo) : ICommand<int>;

/// <summary>Edición a mano del formulario; queda sin aprobar hasta que alguien lo apruebe.</summary>
public sealed record GuardarFormularioCommand(Guid CicloId, string Cargo, IReadOnlyList<CompetenciaDto> Competencias, IReadOnlyList<ObjetivoBaseDto> Objetivos)
    : ICommand;

public sealed record AprobarFormularioCommand(Guid CicloId, string Cargo) : ICommand;

/// <summary>Arranca el ciclo: crea la evaluación de cada empleado activo con el formulario de su cargo. Devuelve cuántas.</summary>
public sealed record AbrirCicloCommand(Guid CicloId) : ICommand<int>;

public sealed record PasarACalibracionCommand(Guid CicloId) : ICommand;

public sealed record CerrarCicloCommand(Guid CicloId) : ICommand;

internal static class Ciclos
{
    public const string TipoBandeja = "FormulariosDesempeno";

    public static readonly string[] NombresCaja =
    [
        "Bajo potencial · bajo desempeño", "Bajo potencial · desempeño medio", "Bajo potencial · alto desempeño",
        "Potencial medio · bajo desempeño", "Núcleo del equipo", "Potencial medio · alto desempeño",
        "Alto potencial · bajo desempeño", "Alto potencial · desempeño medio", "Estrella",
    ];

    public static async Task<Result<Ciclo>> DelUsuarioAsync(ICiclos ciclos, IUsuarioActual usuario, Guid id, CancellationToken ct) =>
        await ciclos.PorIdAsync(usuario.EmpresaId, id, ct) is { } c ? c : CicloErrors.NoExiste;

    public static IEnumerable<FormularioCargo> Elegidos(Ciclo c, string? cargo) =>
        cargo is null ? c.Formularios.Where(f => f.Competencias.Count == 0 && !f.Preparando) : c.Formularios.Where(f => string.Equals(f.Cargo, cargo, StringComparison.OrdinalIgnoreCase));

    /// <summary>En la bandeja queda una sola decisión por ciclo: los formularios que esperan aprobación.</summary>
    public static void AvisarFormularios(IEventosDesempeno eventos, Ciclo c, DateTimeOffset ahora)
    {
        var pendientes = c.Formularios.Where(f => f.Competencias.Count > 0 && f.AprobadoEn is null && !f.Preparando).ToList();
        if (pendientes.Count == 0)
        {
            return;
        }

        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, c.EmpresaId, null, "Desempeno", TipoBandeja, c.Id,
            $"Formularios por aprobar · {c.Nombre}",
            $"{pendientes.Count} {(pendientes.Count == 1 ? "formulario espera" : "formularios esperan")} su revisión antes de abrir el ciclo.",
            pendientes.Any(f => f.Origen == "IA") ? "Diseñador de evaluaciones" : null, pendientes.FirstOrDefault(f => f.Modelo is not null)?.Modelo,
            [.. pendientes.SelectMany(f => f.Razones).Take(4).Select(r => new Core.Contracts.Bandeja.RazonPropuesta(r.Signo, r.Texto, r.Fuente))],
            [.. pendientes.Take(6).Select(f => new CampoPropuesta(f.Cargo, $"{f.Competencias.Count} competencias · {f.Objetivos.Count} objetivos · {f.Origen}"))],
            null, $"/consola/desempeno/{c.Id}", false, c.Hasta));
    }
}

/* ---------- Validadores ---------- */

internal sealed class CrearCicloValidator : AbstractValidator<CrearCicloCommand>
{
    public CrearCicloValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Póngale nombre al ciclo, por ejemplo «Evaluación anual 2026».").MaximumLength(150);
        RuleFor(c => c.Tipo).Must(t => Enum.TryParse<TipoCiclo>(t, true, out _)).WithMessage("Elija el tipo de ciclo.");
        RuleFor(c => c.Alcance).Must(t => Enum.TryParse<AlcanceEvaluacion>(t, true, out _)).WithMessage("Elija si es de 90°, 180° o 360°.");
        RuleFor(c => c.PesoObjetivos).InclusiveBetween(0, 100).WithMessage("El peso de los objetivos va de 0 a 100.");
    }
}

internal sealed class GuardarFormularioValidator : AbstractValidator<GuardarFormularioCommand>
{
    public GuardarFormularioValidator()
    {
        RuleForEach(c => c.Competencias).ChildRules(x =>
        {
            x.RuleFor(y => y.Nombre).NotEmpty().WithMessage("Cada competencia necesita nombre.").MaximumLength(120);
            x.RuleFor(y => y.Descripcion).MaximumLength(400);
            x.RuleFor(y => y.Peso).InclusiveBetween(1, 100);
        });
        RuleForEach(c => c.Objetivos).ChildRules(x =>
        {
            x.RuleFor(y => y.Descripcion).NotEmpty().WithMessage("Cada objetivo necesita descripción.").MaximumLength(300);
            x.RuleFor(y => y.Meta).MaximumLength(200);
            x.RuleFor(y => y.Peso).InclusiveBetween(1, 100);
        });
    }
}

/* ---------- Consultas ---------- */

internal sealed class ListarCiclosHandler(IUsuarioActual usuario, ILecturaDesempeno lectura) : IQueryHandler<ListarCiclosQuery, Pagina<FilaCicloDto>>
{
    public async Task<Result<Pagina<FilaCicloDto>>> Handle(ListarCiclosQuery q, CancellationToken ct) =>
        await lectura.CiclosAsync(usuario.EmpresaId, new PaginaSolicitada(q.Pagina, q.Tamano), ct);
}

internal sealed class ObtenerCicloHandler(IUsuarioActual usuario, ICiclos ciclos, ILecturaDesempeno lectura, IPlantaActiva planta)
    : IQueryHandler<ObtenerCicloQuery, CicloDto>
{
    public async Task<Result<CicloDto>> Handle(ObtenerCicloQuery q, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, q.Id, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var c = r.Value;
        var personas = (await planta.ListarAsync(c.EmpresaId, ct)).Where(e => usuario.Ve(e.EmpresaUsuariaId))
            .GroupBy(e => e.Cargo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var calificadas = await lectura.CalificadasAsync(c.EmpresaId, c.Id, usuario.EmpresasUsuarias, ct);
        var sesgos = SesgosCalificacion.Revisar([.. calificadas.Select(x => (x.Evaluador, x.Area, x.Puntaje))]);
        var matriz = Enumerable.Range(1, 9).Select(caja => new CajaMatrizDto(caja, Ciclos.NombresCaja[caja - 1],
            [.. calificadas.Where(x => x.Caja == caja).OrderByDescending(x => x.Puntaje).Select(x => new PersonaMatrizDto(x.Id, x.Nombre, x.Cargo, x.Puntaje, x.Calibrada))])).ToList();

        return new CicloDto(c.Id, c.Nombre, c.Tipo.ToString(), c.Alcance.ToString(), c.Desde, c.Hasta, c.PesoObjetivos, c.Estado.ToString(),
            [.. c.Formularios.Select(f => new FormularioDto(f.Cargo, personas.GetValueOrDefault(f.Cargo),
                [.. f.Competencias.Select(x => new CompetenciaDto(x.Nombre, x.Descripcion, x.Peso))],
                [.. f.Objetivos.Select(x => new ObjetivoBaseDto(x.Descripcion, x.Meta, x.Peso))], f.Origen,
                [.. f.Razones.Select(x => new RazonDto(x.Signo, x.Texto, x.Fuente))], f.Modelo, f.Preparando, f.Error, f.AprobadoPor, f.AprobadoEn))],
            await lectura.ConteoAsync(c.EmpresaId, c.Id, usuario.EmpresasUsuarias, ct),
            [.. sesgos.Select(s => new AlertaSesgoDto(s.Tipo, s.Texto, s.Evidencia))], matriz,
            calificadas.Count == 0 ? null : Math.Round(calificadas.Average(x => x.Puntaje), 2), c.CreadoPor, c.CreadoEn);
    }
}

/* ---------- Comandos ---------- */

internal sealed class CrearCicloHandler(IUsuarioActual usuario, ICiclos ciclos, IPlantaActiva planta, IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<CrearCicloCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearCicloCommand c, CancellationToken ct)
    {
        var cargos = (await planta.ListarAsync(usuario.EmpresaId, ct)).Select(e => e.Cargo).ToList();
        if (cargos.Count == 0)
        {
            return Error.Validation("Desempeno.SinPlanta", "La empresa no tiene empleados activos. Cargue la planta en Empleados antes de crear el ciclo.");
        }

        var ciclo = Ciclo.Crear(usuario.EmpresaId, c.Nombre, Enum.Parse<TipoCiclo>(c.Tipo, true), Enum.Parse<AlcanceEvaluacion>(c.Alcance, true), c.Desde, c.Hasta,
            c.PesoObjetivos, cargos, usuario.Nombre, reloj.GetUtcNow());
        if (ciclo.IsFailure)
        {
            return ciclo.Error;
        }

        ciclos.Agregar(ciclo.Value);
        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Creó el ciclo de desempeño «{ciclo.Value.Nombre}»",
            $"{ciclo.Value.Formularios.Count} cargos · {ciclo.Value.Alcance}", null, "Ciclo", ciclo.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return ciclo.Value.Id;
    }
}

internal sealed class PedirFormularioIaHandler(IUsuarioActual usuario, ICiclos ciclos, IEventosDesempeno eventos, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<PedirFormularioIaCommand, int>
{
    public async Task<Result<int>> Handle(PedirFormularioIaCommand c, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, c.CicloId, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var ciclo = r.Value;
        var ahora = reloj.GetUtcNow();
        var pedidos = 0;
        foreach (var f in Ciclos.Elegidos(ciclo, c.Cargo).ToList())
        {
            var pedido = ciclo.PedirFormularioIa(f.Cargo, ahora);
            if (pedido.IsFailure)
            {
                return pedido.Error;
            }

            // Sin datos de personas: solo el cargo y el tipo de ciclo
            eventos.Publicar(new FormularioCargoSolicitado(Guid.CreateVersion7(), ahora, ciclo.EmpresaId, ciclo.Id, f.Cargo, ciclo.Tipo.ToString(), ciclo.PesoObjetivos));
            pedidos++;
        }

        await unidad.SaveChangesAsync(ct);
        return pedidos;
    }
}

internal sealed class UsarPlantillaBaseHandler(IUsuarioActual usuario, ICiclos ciclos, IEventosDesempeno eventos, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<UsarPlantillaBaseCommand, int>
{
    public async Task<Result<int>> Handle(UsarPlantillaBaseCommand c, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, c.CicloId, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var ciclo = r.Value;
        var ahora = reloj.GetUtcNow();
        var usados = 0;
        foreach (var f in Ciclos.Elegidos(ciclo, c.Cargo).ToList())
        {
            var (competencias, objetivos, razones) = PlantillaBase.Para(f.Cargo);
            var guardado = ciclo.GuardarFormulario(f.Cargo, competencias, objetivos, "Plantilla base", razones, null, ahora);
            if (guardado.IsFailure)
            {
                return guardado.Error;
            }

            usados++;
        }

        Ciclos.AvisarFormularios(eventos, ciclo, ahora);
        await unidad.SaveChangesAsync(ct);
        return usados;
    }
}

internal sealed class GuardarFormularioHandler(IUsuarioActual usuario, ICiclos ciclos, IUnidadDesempeno unidad, TimeProvider reloj) : ICommandHandler<GuardarFormularioCommand>
{
    public async Task<Result> Handle(GuardarFormularioCommand c, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, c.CicloId, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var guardado = r.Value.GuardarFormulario(c.Cargo,
            [.. c.Competencias.Select(x => new Competencia { Nombre = x.Nombre.Trim(), Descripcion = (x.Descripcion ?? "").Trim(), Peso = x.Peso })],
            [.. c.Objetivos.Select(x => new ObjetivoBase { Descripcion = x.Descripcion.Trim(), Meta = (x.Meta ?? "").Trim(), Peso = x.Peso })],
            "Manual", [], null, reloj.GetUtcNow());
        if (guardado.IsFailure)
        {
            return guardado;
        }

        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class AprobarFormularioHandler(IUsuarioActual usuario, ICiclos ciclos, IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<AprobarFormularioCommand>
{
    public async Task<Result> Handle(AprobarFormularioCommand c, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, c.CicloId, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var ciclo = r.Value;
        var f = ciclo.Formulario(c.Cargo);
        var aprobado = ciclo.AprobarFormulario(c.Cargo, usuario.Nombre, reloj.GetUtcNow());
        if (aprobado.IsFailure)
        {
            return aprobado;
        }

        bitacora.Registrar(ciclo.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Aprobó el formulario de «{c.Cargo}» en «{ciclo.Nombre}»",
            $"{f!.Competencias.Count} competencias · {f.Objetivos.Count} objetivos", f.Origen == "IA" ? "Decisión humana sobre la propuesta de la IA" : f.Origen,
            "Ciclo", ciclo.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class AbrirCicloHandler(IUsuarioActual usuario, ICiclos ciclos, IEvaluaciones evaluaciones, IPlantaActiva planta, IEventosDesempeno eventos,
    IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj) : ICommandHandler<AbrirCicloCommand, int>
{
    public async Task<Result<int>> Handle(AbrirCicloCommand c, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, c.CicloId, ct);
        if (r.IsFailure)
        {
            return r.Error;
        }

        var ciclo = r.Value;
        var ahora = reloj.GetUtcNow();
        var abierto = ciclo.Abrir(ahora);
        if (abierto.IsFailure)
        {
            return abierto.Error;
        }

        var creadas = 0;
        if (!await evaluaciones.HayDelCicloAsync(ciclo.Id, ct))
        {
            foreach (var e in await planta.ListarAsync(ciclo.EmpresaId, ct))
            {
                if (ciclo.Formulario(e.Cargo) is { } f)
                {
                    evaluaciones.Agregar(Evaluacion.Crear(ciclo, f, e.EmpleadoId, e.Codigo, e.Nombre, e.Documento, e.Correo, e.Area, e.Sede, e.EmpresaUsuariaId, ahora));
                    creadas++;
                }
            }
        }

        eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, ciclo.EmpresaId, Ciclos.TipoBandeja, ciclo.Id, "Ciclo abierto", usuario.Nombre));
        bitacora.Registrar(ciclo.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Abrió el ciclo de desempeño «{ciclo.Nombre}»", $"{creadas} evaluaciones",
            null, "Ciclo", ciclo.Id);
        await unidad.SaveChangesAsync(ct);
        return creadas;
    }
}

internal sealed class PasarACalibracionHandler(IUsuarioActual usuario, ICiclos ciclos, IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<PasarACalibracionCommand>
{
    public async Task<Result> Handle(PasarACalibracionCommand c, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, c.CicloId, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var hecho = r.Value.PasarACalibracion(reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(r.Value.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Pasó «{r.Value.Nombre}» al comité de calibración", r.Value.Nombre, null, "Ciclo",
            r.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class CerrarCicloHandler(IUsuarioActual usuario, ICiclos ciclos, IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj)
    : ICommandHandler<CerrarCicloCommand>
{
    public async Task<Result> Handle(CerrarCicloCommand c, CancellationToken ct)
    {
        var r = await Ciclos.DelUsuarioAsync(ciclos, usuario, c.CicloId, ct);
        if (r.IsFailure)
        {
            return Result.Failure(r.Error);
        }

        var hecho = r.Value.Cerrar(reloj.GetUtcNow());
        if (hecho.IsFailure)
        {
            return hecho;
        }

        bitacora.Registrar(r.Value.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Cerró el ciclo de desempeño «{r.Value.Nombre}»", r.Value.Nombre, null, "Ciclo",
            r.Value.Id);
        await unidad.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Llegó la propuesta de la IA: queda en el formulario sin aprobar y la bandeja avisa a RRHH.</summary>
internal sealed class FormularioCargoGeneradoConsumer(ICiclos ciclos, IEventosDesempeno eventos, IBitacora bitacora, IUnidadDesempeno unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<FormularioCargoGenerado>
{
    public async Task Handle(FormularioCargoGenerado e, CancellationToken ct)
    {
        var ciclo = await ciclos.PorIdAsync(e.EmpresaId, e.CicloId, ct);
        if (ciclo?.Formulario(e.Cargo) is not { Preparando: true })
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        var guardado = e.Error is null
            ? ciclo.GuardarFormulario(e.Cargo, [.. e.Competencias.Select(x => new Competencia { Nombre = x.Nombre, Descripcion = x.Descripcion, Peso = x.Peso })],
                [.. e.Objetivos.Select(x => new ObjetivoBase { Descripcion = x.Descripcion, Meta = x.Meta, Peso = x.Peso })], "IA",
                [.. e.Razones.Select(x => new Domain.Ciclos.RazonPropuesta { Signo = x.Signo, Texto = x.Texto, Fuente = x.Fuente })], e.Modelo, ahora)
            : Result.Failure(Error.Failure("Ia", e.Error));
        if (guardado.IsFailure)
        {
            ciclo.FallaFormulario(e.Cargo, e.Error ?? $"La propuesta de la IA no cumple las reglas ({guardado.Error.Description}). Use la plantilla base o hágalo a mano.", ahora);
        }
        else
        {
            Ciclos.AvisarFormularios(eventos, ciclo, ahora);
        }

        bitacora.Registrar(ciclo.EmpresaId, TipoActor.Ia, "Diseñador de evaluaciones", $"Propuso el formulario de «{e.Cargo}»",
            guardado.IsSuccess ? $"{e.Competencias.Count} competencias · {e.Objetivos.Count} objetivos · {e.Modelo}" : "No se pudo usar la propuesta",
            "Espera la aprobación de RRHH", "Ciclo", ciclo.Id);
        await unidad.SaveChangesAsync(ct);
    }
}
