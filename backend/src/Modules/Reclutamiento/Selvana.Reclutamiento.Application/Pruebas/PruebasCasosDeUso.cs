using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Pruebas;

// ---------- DTO ----------

public sealed record PruebaEnBateriaDto(string Codigo, int Peso, int? Corte);

public sealed record PruebaSuiteDto(string Codigo, string Version, string Nombre, string Mide, string Tipo, int Minutos, int Preguntas, bool Validada, bool Sensible);

public sealed record BateriaDto(string Cargo, IReadOnlyList<PruebaEnBateriaDto> Pruebas, bool EsSugerida, string? Razon, string? ActualizadaPor, DateTimeOffset? ActualizadaEn);

public sealed record EnvioPruebasDto(Guid PostulacionId, string Titular, string Token, DateTimeOffset VenceEn);

public sealed record PruebaPublicaDto(string Codigo, string Nombre, string Mide, string Instrucciones, int Minutos, int Preguntas, string Estado);

public sealed record AplicacionPublicaDto(
    string Nombre, string Empresa, string Vacante, string Estado, DateTimeOffset VenceEn, bool Vencida, string ConsentimientoVersion, string ConsentimientoTexto,
    bool ConsentimientoAceptado, IReadOnlyList<PruebaPublicaDto> Pruebas);

public sealed record OpcionPublicaDto(string Id, string Texto);

public sealed record PreguntaPublicaDto(string Id, string Texto, IReadOnlyList<OpcionPublicaDto> Opciones);

/// <summary>Las preguntas sin la clave de calificación.</summary>
public sealed record PreguntasPruebaDto(string Codigo, string Nombre, string Instrucciones, string Tipo, int SegundosRestantes, IReadOnlyList<PreguntaPublicaDto> Preguntas);

public sealed record RespuestaDto(string PreguntaId, string OpcionId, int Milisegundos);

public sealed record ResultadoPruebaDto(
    string Codigo, string Nombre, string Estado, int? Puntaje, int? Corte, bool? PasaCorte, int Peso, bool Sensible, bool Validada, IReadOnlyList<string> Alertas);

public sealed record ResultadoPostulanteDto(
    Guid AplicacionId, Guid PostulacionId, string Titular, string Estado, DateTimeOffset EnviadaEn, DateTimeOffset VenceEn, bool Vencida, int? Total,
    bool Provisional, IReadOnlyList<ResultadoPruebaDto> Pruebas);

public sealed record DimensionDto(string Nombre, int Puntaje);

public sealed record ResultadoSensibleDto(string Codigo, string Nombre, IReadOnlyList<DimensionDto> Dimensiones, string Nota);

// ---------- Puertos ----------

public interface IBateriasCargo
{
    Task<BateriaCargo?> ObtenerAsync(Guid empresaId, string claveCargo, CancellationToken cancellationToken);

    void Agregar(BateriaCargo bateria);
}

public interface IAplicacionesPruebas
{
    Task<AplicacionPruebas?> ObtenerPorTokenAsync(string huella, CancellationToken cancellationToken);

    Task<AplicacionPruebas?> ObtenerPorPostulacionAsync(Guid empresaId, Guid postulacionId, CancellationToken cancellationToken);

    Task<AplicacionPruebas?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    void Agregar(AplicacionPruebas aplicacion);
}

/// <summary>Enlaces de un solo uso para presentar las pruebas: el token va al candidato y solo su huella se guarda.</summary>
public interface ITokensPruebas
{
    (string Token, string Huella) Generar();

    string Huella(string token);
}

public interface ILecturaPruebas
{
    Task<IReadOnlyList<ResultadoPostulanteDto>> ResultadosAsync(Guid empresaId, Guid vacanteId, DateTimeOffset ahora, CancellationToken cancellationToken);

    /// <summary>Empresa (o empresa usuaria) y título de la vacante para la pantalla del candidato.</summary>
    Task<(string Empresa, string Vacante)?> VacanteAsync(Guid vacanteId, CancellationToken cancellationToken);
}

/// <summary>Texto que acepta el candidato antes de presentar pruebas (Ley 1581, art. 6: datos sensibles, respuesta opcional).</summary>
public static class PoliticaPruebas
{
    public const string Version = "p1";

    public static string Texto(string empresa) =>
        $"Autorizo a {empresa} y a Selvana a tratar los resultados de estas pruebas, incluidos los de personalidad, que son datos sensibles, " +
        "solo para este proceso de selección. Sé que no estoy obligado a responder preguntas sobre datos sensibles y que los resultados " +
        "los revisa una persona: ninguna decisión sobre mí se toma solo con un puntaje. Puedo pedir conocerlos o que se eliminen.";
}

internal static class Mapeo
{
    public static PruebaEnBateria ADominio(PruebaEnBateriaDto p) => new() { Codigo = p.Codigo, Peso = p.Peso, Corte = p.Corte };

    public static PruebaEnBateriaDto ADto(PruebaEnBateria p) => new(p.Codigo, p.Peso, p.Corte);
}

// ---------- Consultas ----------

public sealed record ListarSuiteQuery : IQuery<IReadOnlyList<PruebaSuiteDto>>;

internal sealed class ListarSuiteHandler : IQueryHandler<ListarSuiteQuery, IReadOnlyList<PruebaSuiteDto>>
{
    public Task<Result<IReadOnlyList<PruebaSuiteDto>>> Handle(ListarSuiteQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success<IReadOnlyList<PruebaSuiteDto>>(
            [.. SuitePsicotecnica.Todas.Select(p => new PruebaSuiteDto(p.Codigo, p.Version, p.Nombre, p.Mide, p.Tipo.ToString(), p.Minutos, p.Preguntas.Count, p.Validada, p.Sensible))]));
}

/// <summary>La batería guardada para el cargo o, si no hay, la sugerida.</summary>
public sealed record ObtenerBateriaCargoQuery(string Cargo) : IQuery<BateriaDto>;

internal sealed class ObtenerBateriaCargoHandler(IUsuarioActual usuario, IBateriasCargo baterias) : IQueryHandler<ObtenerBateriaCargoQuery, BateriaDto>
{
    public async Task<Result<BateriaDto>> Handle(ObtenerBateriaCargoQuery q, CancellationToken cancellationToken)
    {
        var b = await baterias.ObtenerAsync(usuario.EmpresaId, Baterias.ClaveCargo(q.Cargo), cancellationToken);
        if (b is not null)
        {
            return new BateriaDto(b.CargoNombre, [.. b.Pruebas.Select(Mapeo.ADto)], false, null, b.ActualizadaPor, b.ActualizadaEn);
        }

        var s = Baterias.Sugerir(q.Cargo);
        return new BateriaDto(q.Cargo, [.. s.Pruebas.Select(Mapeo.ADto)], true, s.Razon, null, null);
    }
}

/// <summary>Resultados de las pruebas de los postulantes de una vacante (sin el detalle de los datos sensibles).</summary>
public sealed record ListarResultadosPruebasQuery(Guid VacanteId) : IQuery<IReadOnlyList<ResultadoPostulanteDto>>;

internal sealed class ListarResultadosPruebasHandler(IUsuarioActual usuario, IVacantes vacantes, ILecturaPruebas lectura, TimeProvider reloj)
    : IQueryHandler<ListarResultadosPruebasQuery, IReadOnlyList<ResultadoPostulanteDto>>
{
    public async Task<Result<IReadOnlyList<ResultadoPostulanteDto>>> Handle(ListarResultadosPruebasQuery q, CancellationToken cancellationToken)
    {
        var v = await vacantes.ObtenerAsync(q.VacanteId, cancellationToken);
        if (v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        return Result.Success(await lectura.ResultadosAsync(usuario.EmpresaId, q.VacanteId, reloj.GetUtcNow(), cancellationToken));
    }
}

/// <summary>Pantalla del candidato: qué pruebas tiene y si ya aceptó el tratamiento. Sin sesión, con el enlace.</summary>
public sealed record ObtenerAplicacionQuery(string Token) : IQuery<AplicacionPublicaDto>;

internal sealed class ObtenerAplicacionHandler(IAplicacionesPruebas aplicaciones, ITokensPruebas tokens, ILecturaPruebas lectura, TimeProvider reloj)
    : IQueryHandler<ObtenerAplicacionQuery, AplicacionPublicaDto>
{
    public async Task<Result<AplicacionPublicaDto>> Handle(ObtenerAplicacionQuery q, CancellationToken cancellationToken)
    {
        var a = await aplicaciones.ObtenerPorTokenAsync(tokens.Huella(q.Token), cancellationToken);
        if (a is null)
        {
            return AplicacionErrors.NoExiste;
        }

        var (empresa, vacante) = await lectura.VacanteAsync(a.VacanteId, cancellationToken) ?? ("la empresa", "la vacante");
        var nombre = a.Titular.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return new AplicacionPublicaDto(nombre, empresa, vacante, a.Estado.ToString(), a.VenceEn, a.Vencida(reloj.GetUtcNow()), PoliticaPruebas.Version,
            PoliticaPruebas.Texto(empresa), a.ConsentimientoEn is not null,
            [.. a.Pruebas.Select(p => (p, d: SuitePsicotecnica.Buscar(p.Codigo)!))
                .Select(x => new PruebaPublicaDto(x.p.Codigo, x.d.Nombre, x.d.Mide, x.d.Instrucciones, x.d.Minutos, x.d.Preguntas.Count, x.p.Estado.ToString()))]);
    }
}

// ---------- Comandos de la consola ----------

public sealed record GuardarBateriaCargoCommand(string Cargo, IReadOnlyList<PruebaEnBateriaDto> Pruebas) : ICommand;

internal sealed class GuardarBateriaCargoValidator : AbstractValidator<GuardarBateriaCargoCommand>
{
    public GuardarBateriaCargoValidator()
    {
        RuleFor(c => c.Cargo).NotEmpty().WithMessage(BateriaErrors.CargoVacio.Description).MaximumLength(200);
        RuleFor(c => c.Pruebas).NotEmpty().WithMessage("Elija al menos una prueba.");
    }
}

internal sealed class GuardarBateriaCargoHandler(IUsuarioActual usuario, IBateriasCargo baterias, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<GuardarBateriaCargoCommand>
{
    public async Task<Result> Handle(GuardarBateriaCargoCommand c, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var pruebas = c.Pruebas.Select(Mapeo.ADominio).ToList();
        var b = await baterias.ObtenerAsync(usuario.EmpresaId, Baterias.ClaveCargo(c.Cargo), cancellationToken);
        if (b is null)
        {
            var creada = BateriaCargo.Crear(usuario.EmpresaId, c.Cargo, pruebas, usuario.Nombre, ahora);
            if (creada.IsFailure)
            {
                return Result.Failure(creada.Error);
            }

            b = creada.Value;
            baterias.Agregar(b);
        }
        else
        {
            var cambio = b.Cambiar(c.Cargo, pruebas, usuario.Nombre, ahora);
            if (cambio.IsFailure)
            {
                return cambio;
            }
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Configuró las pruebas del cargo {b.CargoNombre}",
            string.Join(" · ", b.Pruebas.Select(p => $"{SuitePsicotecnica.Buscar(p.Codigo)?.Nombre} (peso {p.Peso}{(p.Corte is null ? "" : $", corte {p.Corte}")})")),
            null, "BateriaCargo", b.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GuardarBateriaVacanteCommand(Guid VacanteId, IReadOnlyList<PruebaEnBateriaDto> Pruebas) : ICommand;

internal sealed class GuardarBateriaVacanteHandler(IUsuarioActual usuario, IVacantes vacantes, IBitacora bitacora, IUnidadReclutamiento unidad)
    : ICommandHandler<GuardarBateriaVacanteCommand>
{
    public async Task<Result> Handle(GuardarBateriaVacanteCommand c, CancellationToken cancellationToken)
    {
        var v = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return Result.Failure(VacanteErrors.NoExiste);
        }

        var r = v.ConfigurarPruebas(c.Pruebas.Select(Mapeo.ADominio));
        if (r.IsFailure)
        {
            return r;
        }

        bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Ajustó las pruebas de la vacante {v.Codigo}",
            string.Join(" · ", v.Pruebas.Select(p => SuitePsicotecnica.Buscar(p.Codigo)?.Nombre)), null, "Vacante", v.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Envía la batería de la vacante a los postulantes elegidos. A quien ya tenía pruebas se le genera un enlace nuevo.</summary>
public sealed record EnviarPruebasCommand(Guid VacanteId, IReadOnlyList<Guid> PostulacionIds) : ICommand<IReadOnlyList<EnvioPruebasDto>>;

internal sealed class EnviarPruebasValidator : AbstractValidator<EnviarPruebasCommand>
{
    public EnviarPruebasValidator() =>
        RuleFor(c => c.PostulacionIds).NotEmpty().WithMessage("Elija al menos un postulante.").Must(p => p.Count <= 200).WithMessage("Máximo 200 postulantes por envío.");
}

internal sealed class EnviarPruebasHandler(
    IUsuarioActual usuario, IVacantes vacantes, ICandidatos candidatos, IAplicacionesPruebas aplicaciones, ITokensPruebas tokens, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<EnviarPruebasCommand, IReadOnlyList<EnvioPruebasDto>>
{
    private static readonly Error SinBateria = Error.Validation("Pruebas.SinBateria", "Esta vacante no tiene pruebas configuradas. Elija la batería primero.");

    public async Task<Result<IReadOnlyList<EnvioPruebasDto>>> Handle(EnviarPruebasCommand c, CancellationToken cancellationToken)
    {
        var v = await vacantes.ObtenerAsync(c.VacanteId, cancellationToken);
        if (v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return VacanteErrors.NoExiste;
        }

        if (v.Pruebas.Count == 0)
        {
            return SinBateria;
        }

        var ahora = reloj.GetUtcNow();
        var enviados = new List<EnvioPruebasDto>();
        foreach (var postulacionId in c.PostulacionIds.Distinct())
        {
            var candidato = await candidatos.ObtenerPorPostulacionAsync(usuario.EmpresaId, postulacionId, cancellationToken);
            var postulacion = candidato?.Postulaciones.FirstOrDefault(p => p.Id == postulacionId);
            if (candidato is null || postulacion is null || postulacion.VacanteId != v.Id || candidato.Suprimido)
            {
                continue;
            }

            var (token, huella) = tokens.Generar();
            var existente = await aplicaciones.ObtenerPorPostulacionAsync(usuario.EmpresaId, postulacionId, cancellationToken);
            if (existente is null)
            {
                aplicaciones.Agregar(AplicacionPruebas.Enviar(v.EmpresaId, v.Id, postulacionId, candidato.Id, candidato.Datos.Nombre, v.Pruebas, huella, usuario.Nombre, ahora));
            }
            else
            {
                existente.Reenviar(huella, usuario.Nombre, ahora);
            }

            enviados.Add(new EnvioPruebasDto(postulacionId, candidato.Datos.Nombre, token, ahora.Add(AplicacionPruebas.Vigencia)));
        }

        if (enviados.Count > 0)
        {
            bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Envió pruebas a {enviados.Count} {(enviados.Count == 1 ? "postulante" : "postulantes")} de la vacante {v.Codigo}",
                string.Join(" · ", v.Pruebas.Select(p => SuitePsicotecnica.Buscar(p.Codigo)?.Nombre)), "Enlaces de un solo uso, vencen en 72 horas", "Vacante", v.Id);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return enviados;
    }
}

/// <summary>Abre el detalle de un resultado sensible (personalidad). Queda en la bitácora como acceso a datos sensibles.</summary>
public sealed record VerResultadoSensibleCommand(Guid AplicacionId, string Codigo) : ICommand<ResultadoSensibleDto>;

internal sealed class VerResultadoSensibleHandler(IUsuarioActual usuario, IAplicacionesPruebas aplicaciones, IVacantes vacantes, IBitacora bitacora, IUnidadReclutamiento unidad)
    : ICommandHandler<VerResultadoSensibleCommand, ResultadoSensibleDto>
{
    public async Task<Result<ResultadoSensibleDto>> Handle(VerResultadoSensibleCommand c, CancellationToken cancellationToken)
    {
        var a = await aplicaciones.ObtenerAsync(usuario.EmpresaId, c.AplicacionId, cancellationToken);
        var p = a?.Pruebas.FirstOrDefault(x => x.Codigo == c.Codigo && x.Estado == EstadoPrueba.Terminada);
        var d = SuitePsicotecnica.Buscar(c.Codigo);
        var v = a is null ? null : await vacantes.ObtenerAsync(a.VacanteId, cancellationToken);
        if (a is null || p is null || d is null || v is null || !usuario.Ve(v.EmpresaUsuariaId))
        {
            return AplicacionErrors.NoExiste;
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Vio el resultado de {d.Nombre} de {a.Titular}", $"Vacante {v.Codigo}",
            "Dato sensible · acceso permitido por su rol", "AplicacionPruebas", a.Id, datoSensible: true);
        await unidad.SaveChangesAsync(cancellationToken);
        return new ResultadoSensibleDto(d.Codigo, d.Nombre, [.. p.Dimensiones.Select(x => new DimensionDto(x.Nombre, x.Puntaje))],
            d.Validada
                ? "Interpretación a cargo del psicólogo del equipo."
                : "Prueba en validación: los puntajes son orientativos, no hay baremos colombianos todavía y no cuentan para decidir. No hay rasgos buenos o malos.");
    }
}

// ---------- Comandos del candidato (sin sesión, con el enlace) ----------

public sealed record ResponderConsentimientoCommand(string Token, bool Acepta) : ICommand;

internal sealed class ResponderConsentimientoHandler(IAplicacionesPruebas aplicaciones, ITokensPruebas tokens, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<ResponderConsentimientoCommand>
{
    public async Task<Result> Handle(ResponderConsentimientoCommand c, CancellationToken cancellationToken)
    {
        var a = await aplicaciones.ObtenerPorTokenAsync(tokens.Huella(c.Token), cancellationToken);
        if (a is null)
        {
            return Result.Failure(AplicacionErrors.NoExiste);
        }

        var r = a.ResponderConsentimiento(c.Acepta, reloj.GetUtcNow());
        if (r.IsSuccess)
        {
            bitacora.Registrar(a.EmpresaId, TipoActor.Titular, a.Titular, c.Acepta ? $"Aceptó el tratamiento de resultados de pruebas {PoliticaPruebas.Version}" : "Decidió no presentar las pruebas",
                $"{a.Pruebas.Count} pruebas asignadas", null, "AplicacionPruebas", a.Id, datoSensible: c.Acepta);
            await unidad.SaveChangesAsync(cancellationToken);
        }

        return r;
    }
}

public sealed record IniciarPruebaCommand(string Token, string Codigo) : ICommand<PreguntasPruebaDto>;

internal sealed class IniciarPruebaHandler(IAplicacionesPruebas aplicaciones, ITokensPruebas tokens, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<IniciarPruebaCommand, PreguntasPruebaDto>
{
    public async Task<Result<PreguntasPruebaDto>> Handle(IniciarPruebaCommand c, CancellationToken cancellationToken)
    {
        var a = await aplicaciones.ObtenerPorTokenAsync(tokens.Huella(c.Token), cancellationToken);
        if (a is null)
        {
            return AplicacionErrors.NoExiste;
        }

        var segundos = a.Iniciar(c.Codigo, reloj.GetUtcNow());
        if (segundos.IsFailure)
        {
            return segundos.Error;
        }

        await unidad.SaveChangesAsync(cancellationToken);
        var d = SuitePsicotecnica.Buscar(c.Codigo)!;
        return new PreguntasPruebaDto(d.Codigo, d.Nombre, d.Instrucciones, d.Tipo.ToString(), segundos.Value,
            [.. d.Preguntas.Select(p => new PreguntaPublicaDto(p.Id, p.Texto, [.. p.Opciones.Select(o => new OpcionPublicaDto(o.Id, o.Texto))]))]);
    }
}

public sealed record TerminarPruebaCommand(string Token, string Codigo, IReadOnlyList<RespuestaDto> Respuestas, int CambiosDePestana) : ICommand;

internal sealed class TerminarPruebaValidator : AbstractValidator<TerminarPruebaCommand>
{
    public TerminarPruebaValidator() => RuleFor(c => c.Respuestas).Must(r => r.Count <= 200);
}

internal sealed class TerminarPruebaHandler(IAplicacionesPruebas aplicaciones, ITokensPruebas tokens, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj)
    : ICommandHandler<TerminarPruebaCommand>
{
    public async Task<Result> Handle(TerminarPruebaCommand c, CancellationToken cancellationToken)
    {
        var a = await aplicaciones.ObtenerPorTokenAsync(tokens.Huella(c.Token), cancellationToken);
        if (a is null)
        {
            return Result.Failure(AplicacionErrors.NoExiste);
        }

        var r = a.Terminar(c.Codigo, [.. c.Respuestas.Select(x => new RespuestaPrueba { PreguntaId = x.PreguntaId, OpcionId = x.OpcionId, Milisegundos = x.Milisegundos })],
            c.CambiosDePestana, reloj.GetUtcNow());
        if (r.IsFailure)
        {
            return r;
        }

        var p = a.Pruebas.First(x => x.Codigo == c.Codigo);
        bitacora.Registrar(a.EmpresaId, TipoActor.Titular, a.Titular, $"Presentó la prueba {SuitePsicotecnica.Buscar(c.Codigo)?.Nombre}",
            $"{p.Respondidas} respuestas{(p.Puntaje is null ? "" : $" · {p.Puntaje}/100")}", p.Alertas.Count > 0 ? string.Join(" · ", p.Alertas) : null,
            "AplicacionPruebas", a.Id, datoSensible: p.Puntaje is null);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
