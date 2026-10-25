using FluentValidation;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Ranking;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Recontacto;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Recontacto;

public interface ISugerenciasBanco
{
    Task<SugerenciaBanco?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SugerenciaBanco>> DeVacanteAsync(Guid empresaId, Guid vacanteId, CancellationToken cancellationToken);

    void Agregar(SugerenciaBanco sugerencia);
}

/// <summary>Lo que el recontacto lee del banco de talento (sin tracking).</summary>
public interface IBancoTalento
{
    /// <summary>Candidatos no suprimidos cuyo texto de búsqueda contiene todos los términos del cargo (con sus postulaciones), los más recientes primero.</summary>
    Task<IReadOnlyList<Candidato>> PorCargoAsync(Guid empresaId, IReadOnlyList<string> terminos, int maximo, CancellationToken cancellationToken);

    Task<Candidato?> ObtenerAsync(Guid empresaId, Guid candidatoId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> NombresAsync(Guid empresaId, IReadOnlyCollection<Guid> candidatoIds, CancellationToken cancellationToken);
}

/// <summary>Una persona sugerida del banco para la vacante, con lo que encontraron las reglas y lo que dijo la IA.</summary>
public sealed record SugerenciaBancoDto(
    Guid Id, Guid CandidatoId, string Nombre, string Documento, string Ciudad, int PuntajeReglas, IReadOnlyList<CoincidenciaBancoDto> Coincidencias,
    AfinidadBancoDto? Afinidad, string Estado, string? DecididaPor, DateTimeOffset? DecididaEn, string? Motivo, bool YaSePostulo, int PostulacionesPrevias,
    string? Celular, string? Correo, DateTimeOffset CreadaEn);

public sealed record CoincidenciaBancoDto(string Tipo, string Texto, string Evidencia);

public sealed record RazonBancoDto(string Signo, string Texto, string Evidencia);

public sealed record AfinidadBancoDto(string Estado, int? Puntaje, IReadOnlyList<RazonBancoDto> Razones, IReadOnlyList<string> Cumplidos, IReadOnlyList<string> Faltantes,
    string? Modelo, string? Mensaje);

/// <summary>Lo que necesita la persona para invitar: el enlace de la vacante (con el canal Banco) y un mensaje listo para copiar.</summary>
public sealed record InvitacionBancoDto(string Ruta, string Mensaje, string Celular, string Correo);

public interface ILecturaRecontacto
{
    Task<Pagina<SugerenciaBancoDto>> SugerenciasAsync(Guid empresaId, Guid vacanteId, PaginaSolicitada pagina, CancellationToken cancellationToken);
}

/// <summary>
/// Busca en el banco de talento a quien encaje con la vacante publicada y pide a la IA calificarlos. <see cref="AlPublicar"/> = lo lanza
/// la publicación (si la vacante aún no está publicada no hace nada); si no, lo pide una persona («Buscar de nuevo en el banco»).
/// Devuelve cuántas personas nuevas sugirió.
/// </summary>
public sealed record BuscarEnBancoCommand(Guid VacanteId, bool AlPublicar = false) : ICommand<int>;

public sealed record ListarSugerenciasBancoQuery(Guid VacanteId, int? Pagina, int? Tamano) : IQuery<Pagina<SugerenciaBancoDto>>;

/// <summary>Decisión humana: invitar a la persona a postularse (ella decide si se postula, con su propia autorización).</summary>
public sealed record InvitarDesdeBancoCommand(Guid SugerenciaId) : ICommand<InvitacionBancoDto>;

/// <summary>Decisión humana: la persona no encaja, con su motivo.</summary>
public sealed record DescartarSugerenciaBancoCommand(Guid SugerenciaId, string Motivo) : ICommand;

public static class RecontactoBandeja
{
    public const string Tipo = "RecontactoBanco";

    /// <summary>Cuántos candidatos revisan las reglas y a cuántos califica la IA por búsqueda (presupuesto de IA).</summary>
    public const int MaximoRevisados = 500;
    public const int MaximoSugeridos = 8;

    public static readonly Error NoPublicada = Error.Conflict("Recontacto.NoPublicada", "Solo se busca en el banco cuando la vacante está publicada.");

    /// <summary>
    /// Deja la propuesta en la bandeja cuando la IA terminó de calificar a todos: «N personas del banco encajan», con la razón principal de cada una.
    /// Si ya no queda nadie por decidir, la propuesta sale de la bandeja.
    /// </summary>
    public static void Avisar(Vacante v, IReadOnlyList<SugerenciaBanco> sugerencias, IReadOnlyDictionary<Guid, string> nombres, IEventosReclutamiento eventos,
        string por, DateTimeOffset ahora)
    {
        if (sugerencias.Any(s => s.Calculando))
        {
            return;
        }

        var pendientes = sugerencias.Where(s => s.Estado == EstadoSugerenciaBanco.Sugerida).OrderByDescending(s => s.PuntajeOrden).ToList();
        if (pendientes.Count == 0)
        {
            eventos.Publicar(new PropuestaResuelta(Guid.CreateVersion7(), ahora, v.EmpresaId, Tipo, v.Id, "Ya se decidió sobre todas las personas del banco", por));
            return;
        }

        string Nombre(SugerenciaBanco s) => nombres.TryGetValue(s.CandidatoId, out var n) ? n : "Candidato";
        var razones = pendientes.Take(5).Select(s =>
        {
            var principal = s.Afinidad?.Razones.FirstOrDefault(r => r.Signo == "+");
            var coincidencia = s.Coincidencias.FirstOrDefault();
            return principal is not null
                ? new RazonPropuesta("+", $"{Nombre(s)} · {s.PuntajeOrden}/100: {principal.Texto}", principal.Evidencia)
                : new RazonPropuesta("+", $"{Nombre(s)} · {s.PuntajeOrden}/100: {coincidencia?.Texto ?? "coincide con la vacante"}", coincidencia?.Evidencia ?? "Reglas");
        }).ToList();
        var modelo = pendientes.Select(s => s.Afinidad?.Modelo).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
        eventos.Publicar(new PropuestaPublicada(Guid.CreateVersion7(), ahora, v.EmpresaId, v.EmpresaUsuariaId, "Reclutamiento", Tipo, v.Id,
            $"{pendientes.Count} {(pendientes.Count == 1 ? "persona del banco encaja" : "personas del banco encajan")} · {v.Titulo}",
            "Se postularon antes a su empresa y autorizaron que les ofrezcan vacantes similares. Revise sus razones e invite a quien quiera: cada uno decide si se postula.",
            "Agente de ranking", modelo, razones,
            [new CampoPropuesta("Personas", string.Join(", ", pendientes.Take(5).Select(Nombre)) + (pendientes.Count > 5 ? $" y {pendientes.Count - 5} más" : "")),
             new CampoPropuesta("Siguiente", "Al invitar, la persona recibe el enlace de la vacante y se postula ella misma. Nadie queda postulado solo")],
            null, $"/consola/vacantes/{v.Id}?pestana=banco", false, null));
    }

    public static async Task<Vacante?> VacanteVisibleAsync(IVacantes vacantes, IUsuarioActual usuario, Guid id, CancellationToken ct)
    {
        var v = await vacantes.ObtenerAsync(id, ct);
        return v is null || v.EmpresaId != usuario.EmpresaId || !usuario.Ve(v.EmpresaUsuariaId) ? null : v;
    }

    /// <summary>El perfil anónimo sin respuestas (no se postuló a esta vacante): sin nombre, documento, contacto ni variables protegidas.</summary>
    public static PerfilAnonimo Anonimo(DatosCandidato d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return new PerfilAnonimo(d.Ciudad,
            [.. d.Experiencia.Select(e => new ExperienciaAnonima(e.Cargo, e.Empresa, e.Desde, e.Hasta))],
            [.. d.Formacion.Select(f => string.IsNullOrWhiteSpace(f.Institucion) ? f.Titulo : $"{f.Titulo} · {f.Institucion}")],
            d.Cursos, d.Habilidades, []);
    }
}

internal sealed class BuscarEnBancoHandler(
    IUsuarioActual usuario, IVacantes vacantes, IBancoTalento banco, ISugerenciasBanco sugerencias, ICalibracionRanking calibracion,
    IEventosReclutamiento eventos, IBitacora bitacora, IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<BuscarEnBancoCommand, int>
{
    public async Task<Result<int>> Handle(BuscarEnBancoCommand c, CancellationToken cancellationToken)
    {
        if (await RecontactoBandeja.VacanteVisibleAsync(vacantes, usuario, c.VacanteId, cancellationToken) is not { } v)
        {
            return VacanteErrors.NoExiste;
        }

        if (v.Estado != EstadoVacante.Publicada)
        {
            return c.AlPublicar ? 0 : RecontactoBandeja.NoPublicada;
        }

        var existentes = await sugerencias.DeVacanteAsync(v.EmpresaId, v.Id, cancellationToken);
        if (c.AlPublicar && existentes.Count > 0)
        {
            // Ya se buscó al publicar; volver a buscar es decisión de una persona
            return 0;
        }

        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var cargo = string.IsNullOrWhiteSpace(v.Cargo) ? v.Titulo : v.Cargo;
        var terminos = BusquedaTalento.Interpretar(Normalizacion.Cargo(cargo)).Terminos;
        var yaSugeridos = existentes.Select(s => s.CandidatoId).ToHashSet();
        var candidatos = terminos.Count == 0 ? [] : await banco.PorCargoAsync(v.EmpresaId, terminos, RecontactoBandeja.MaximoRevisados, cancellationToken);
        var elegidos = candidatos
            .Where(x => !yaSugeridos.Contains(x.Id) && RecontactoBanco.PuedeRecontactarse(x, v.Id))
            .Select(x => (Candidato: x, Evaluacion: RecontactoBanco.Evaluar(v, x.Datos, hoy)))
            .Where(x => x.Evaluacion.Encaja)
            .OrderByDescending(x => x.Evaluacion.Puntaje).ThenByDescending(x => x.Candidato.ActualizadoEn)
            .Take(RecontactoBandeja.MaximoSugeridos).ToList();

        var actor = c.AlPublicar ? "Recontacto del banco (reglas)" : usuario.Nombre;
        if (elegidos.Count == 0)
        {
            bitacora.Registrar(v.EmpresaId, c.AlPublicar ? TipoActor.Sistema : TipoActor.Persona, actor, $"Buscó en el banco de talento para la vacante {v.Codigo}",
                $"{candidatos.Count} perfiles revisados · nadie nuevo encaja", "Solo se proponen personas que autorizaron recibir vacantes similares", "Vacante", v.Id);
            await unidad.SaveChangesAsync(cancellationToken);
            return 0;
        }

        var criterios = await calibracion.CriteriosAsync(v.EmpresaId, v.Cargo, v.Id, cancellationToken);
        foreach (var (candidato, evaluacion) in elegidos)
        {
            var s = SugerenciaBanco.Crear(v.EmpresaId, v.Id, candidato.Id, evaluacion, ahora);
            s.PedirAfinidad();
            sugerencias.Agregar(s);
            // El agente de ranking responde con el Id de la sugerencia en lugar de una postulación
            eventos.Publicar(new AfinidadSolicitada(Guid.CreateVersion7(), ahora, v.EmpresaId, s.Id, v.Id, PerfilParaRanking.Vacante(v),
                RecontactoBandeja.Anonimo(candidato.Datos), criterios));
        }

        bitacora.Registrar(v.EmpresaId, c.AlPublicar ? TipoActor.Sistema : TipoActor.Persona, actor, $"Buscó en el banco de talento para la vacante {v.Codigo}",
            $"{candidatos.Count} perfiles revisados · {elegidos.Count} encajan por cargo, ciudad o requisitos y van al agente de ranking",
            "Solo personas que autorizaron recibir vacantes similares. Una persona decide a quién invitar", "Vacante", v.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return elegidos.Count;
    }
}

/// <summary>
/// El agente de ranking respondió por una sugerencia del banco (el Id viaja como PostulacionId). Si no es una sugerencia, no hace nada:
/// la postulación la atiende <see cref="AfinidadCalculadaConsumer"/>. Cuando terminan todas, la propuesta va a la bandeja.
/// </summary>
internal sealed class AfinidadBancoCalculadaConsumer(
    ISugerenciasBanco sugerencias, IVacantes vacantes, IBancoTalento banco, IEventosReclutamiento eventos, IBitacora bitacora, IUnidadReclutamiento unidad,
    TimeProvider reloj) : IIntegrationEventConsumer<AfinidadCalculada>
{
    public async Task Handle(AfinidadCalculada e, CancellationToken cancellationToken)
    {
        var s = await sugerencias.ObtenerAsync(e.EmpresaId, e.PostulacionId, cancellationToken);
        if (s is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        if (e.Error is not null)
        {
            s.RegistrarFallaAfinidad(e.Error);
            bitacora.Registrar(e.EmpresaId, TipoActor.Ia, "Agente de ranking", "No pudo calificar a una persona del banco", e.Error,
                "Queda con el puntaje de las reglas; una persona decide", "SugerenciaBanco", s.Id);
        }
        else
        {
            s.RegistrarAfinidad(e.Puntaje, e.Razones.Select(x => new RazonPuntaje { Signo = x.Signo, Texto = x.Texto, Evidencia = x.Evidencia }),
                e.RequisitosCumplidos, e.RequisitosFaltantes, e.Modelo, ahora, e.RazonesRetiradas);
            bitacora.Registrar(e.EmpresaId, TipoActor.Ia, "Agente de ranking", $"Calificó a una persona del banco: {e.Puntaje}/100",
                $"{e.Razones.Count} razones con evidencia · {e.Modelo}", "Sugerencia sobre el perfil anónimo. Una persona decide si la invita",
                "SugerenciaBanco", s.Id);
        }

        var v = await vacantes.ObtenerAsync(s.VacanteId, cancellationToken);
        if (v is not null)
        {
            var todas = await sugerencias.DeVacanteAsync(e.EmpresaId, v.Id, cancellationToken);
            var nombres = await banco.NombresAsync(e.EmpresaId, [.. todas.Select(x => x.CandidatoId)], cancellationToken);
            RecontactoBandeja.Avisar(v, todas, nombres, eventos, "Agente de ranking", ahora);
        }

        await unidad.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class ListarSugerenciasBancoHandler(IUsuarioActual usuario, IVacantes vacantes, ILecturaRecontacto lectura)
    : IQueryHandler<ListarSugerenciasBancoQuery, Pagina<SugerenciaBancoDto>>
{
    public async Task<Result<Pagina<SugerenciaBancoDto>>> Handle(ListarSugerenciasBancoQuery q, CancellationToken ct) =>
        await RecontactoBandeja.VacanteVisibleAsync(vacantes, usuario, q.VacanteId, ct) is { } v
            ? await lectura.SugerenciasAsync(v.EmpresaId, v.Id, new PaginaSolicitada(q.Pagina, q.Tamano), ct)
            : VacanteErrors.NoExiste;
}

internal sealed class InvitarDesdeBancoHandler(
    IUsuarioActual usuario, IVacantes vacantes, ISugerenciasBanco sugerencias, IBancoTalento banco, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<InvitarDesdeBancoCommand, InvitacionBancoDto>
{
    public async Task<Result<InvitacionBancoDto>> Handle(InvitarDesdeBancoCommand c, CancellationToken cancellationToken)
    {
        var s = await sugerencias.ObtenerAsync(usuario.EmpresaId, c.SugerenciaId, cancellationToken);
        var v = s is null ? null : await RecontactoBandeja.VacanteVisibleAsync(vacantes, usuario, s.VacanteId, cancellationToken);
        var candidato = s is null ? null : await banco.ObtenerAsync(usuario.EmpresaId, s.CandidatoId, cancellationToken);
        if (s is null || v is null || candidato is null)
        {
            return SugerenciaBancoErrors.NoExiste;
        }

        if (v.Estado != EstadoVacante.Publicada)
        {
            return RecontactoBandeja.NoPublicada;
        }

        var ahora = reloj.GetUtcNow();
        // Se revisa de nuevo: pudo revocar la autorización o postularse mientras tanto
        var r = s.Invitar(!candidato.Suprimido && RecontactoBanco.PuedeRecontactarse(candidato, v.Id), usuario.Nombre, ahora);
        if (r.IsFailure)
        {
            return r.Error;
        }

        bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Invitó a una persona del banco a postularse a la vacante {v.Codigo}",
            $"{candidato.Datos.Nombre} · reglas {s.PuntajeReglas}/100" + (s.Afinidad?.PuntajeIa is { } p ? $" · IA {p}/100" : ""),
            "La persona decide si se postula, con su propia autorización", "SugerenciaBanco", s.Id);
        await AvisarAsync(v, cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);

        var nombre = candidato.Datos.Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return new InvitacionBancoDto($"/empleos/{v.Codigo}/postular?utm_source=Banco",
            $"Hola{(nombre.Length > 0 ? $" {nombre}" : "")}, te escribimos de {v.EmpresaVisible}. Abrimos la vacante «{v.Titulo}» en {v.Ciudad} y, por tu experiencia, " +
            "creemos que te puede interesar. Si quieres postularte, entra aquí:",
            candidato.Datos.Celular, candidato.Datos.Correo);
    }

    private async Task AvisarAsync(Vacante v, CancellationToken ct)
    {
        var todas = await sugerencias.DeVacanteAsync(v.EmpresaId, v.Id, ct);
        RecontactoBandeja.Avisar(v, todas, await banco.NombresAsync(v.EmpresaId, [.. todas.Select(x => x.CandidatoId)], ct), eventos, usuario.Nombre, reloj.GetUtcNow());
    }
}

internal sealed class DescartarSugerenciaBancoValidator : AbstractValidator<DescartarSugerenciaBancoCommand>
{
    public DescartarSugerenciaBancoValidator() =>
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Diga por qué no encaja: queda en la auditoría.").MaximumLength(500);
}

internal sealed class DescartarSugerenciaBancoHandler(
    IUsuarioActual usuario, IVacantes vacantes, ISugerenciasBanco sugerencias, IBancoTalento banco, IEventosReclutamiento eventos, IBitacora bitacora,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<DescartarSugerenciaBancoCommand>
{
    public async Task<Result> Handle(DescartarSugerenciaBancoCommand c, CancellationToken cancellationToken)
    {
        var s = await sugerencias.ObtenerAsync(usuario.EmpresaId, c.SugerenciaId, cancellationToken);
        var v = s is null ? null : await RecontactoBandeja.VacanteVisibleAsync(vacantes, usuario, s.VacanteId, cancellationToken);
        if (s is null || v is null)
        {
            return Result.Failure(SugerenciaBancoErrors.NoExiste);
        }

        var ahora = reloj.GetUtcNow();
        var r = s.DescartarSugerencia(c.Motivo, usuario.Nombre, ahora);
        if (r.IsFailure)
        {
            return r;
        }

        bitacora.Registrar(v.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Decidió que una persona del banco no encaja en la vacante {v.Codigo}",
            $"Reglas {s.PuntajeReglas}/100" + (s.Afinidad?.PuntajeIa is { } p ? $" · IA {p}/100" : ""), $"Motivo: {s.Motivo}", "SugerenciaBanco", s.Id);
        var todas = await sugerencias.DeVacanteAsync(v.EmpresaId, v.Id, cancellationToken);
        RecontactoBandeja.Avisar(v, todas, await banco.NombresAsync(v.EmpresaId, [.. todas.Select(x => x.CandidatoId)], cancellationToken), eventos, usuario.Nombre, ahora);
        await unidad.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
