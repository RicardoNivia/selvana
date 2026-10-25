using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Application.Procesos;

/// <summary>Condiciones que RRHH define (o ajusta) al aprobar la contratación.</summary>
public sealed record CondicionesRequest(string Tipo, decimal SalarioCop, DateOnly FechaIngreso, DateOnly? FechaFin, int PeriodoPruebaDias, string? Labor);

public sealed record CondicionesDto(
    string Tipo, string TipoNombre, decimal SalarioCop, DateOnly FechaIngreso, DateOnly? FechaFin, int PeriodoPruebaDias, string? Labor, string Jornada,
    string Ciudad, bool AuxilioTransporte, decimal AuxilioTransporteCop);

public sealed record AlertaDto(string Nivel, string Texto, string Fuente);

public sealed record RazonDto(string Signo, string Texto, string Fuente);

public sealed record ClausulaDto(string Titulo, string Texto);

public sealed record ContratacionSugeridaDto(
    Guid PostulacionId, string Nombre, string Documento, string Vacante, string CodigoVacante, string Cargo, string? EmpresaUsuaria, decimal SalarioMinimo,
    decimal SalarioMaximo, decimal? AspiracionSalarialCop, CondicionesDto Condiciones, IReadOnlyList<RazonDto> Razones, IReadOnlyList<AlertaDto> Alertas,
    Guid? ProcesoId);

public sealed record RespuestaOfertaDto(bool Acepta, string NombreEscrito, string? Motivo, DateTimeOffset En, string Ip, string Navegador, string Huella, bool Integra);

public sealed record OfertaDto(
    string Estado, string Carta, IReadOnlyList<ClausulaDto> Clausulas, IReadOnlyList<RazonDto> Razones, IReadOnlyList<string> AlertasIa, string? Modelo,
    decimal CostoUsd, string? ErrorIa, DateTimeOffset? RedactadaEn, string? EditadaPor, string? AprobadaPor, DateTimeOffset? AprobadaEn,
    DateTimeOffset? EnviadaEn, RespuestaOfertaDto? Respuesta);

public sealed record PersonaDto(string Nombre, string Documento, string Correo, string Celular, string Ciudad);

public sealed record CampoLeidoDto(string Campo, string Valor);

public sealed record LecturaDocumentoDto(
    string TipoDetectado, bool Legible, bool TipoCorrecto, bool? CoincideTitular, IReadOnlyList<CampoLeidoDto> Campos, IReadOnlyList<string> Observaciones,
    IReadOnlyList<RazonDto> Razones, string? Modelo, string? Error, DateTimeOffset LeidoEn);

/// <summary>Un documento de ingreso. El concepto médico nunca va aquí: solo si pide revisión.</summary>
public sealed record DocumentoDto(
    string Tipo, string Nombre, bool Obligatorio, bool LoCargaEmpresa, string Estado, string? NombreArchivo, string? SubidoPor, DateTimeOffset? SubidoEn,
    int Versiones, LecturaDocumentoDto? Lectura, string? RevisadoPor, DateTimeOffset? RevisadoEn, string? MotivoRechazo, string? Antecedentes,
    bool? ConceptoRequiereRevision);

public sealed record AfiliacionDto(
    string Tipo, string Nombre, string Estado, string Entidad, string? SugeridaDe, string? Radicado, DateOnly? FechaEfectiva, int? ClaseRiesgo, string? Por, DateTimeOffset? En);

public sealed record AltaDto(
    string Area, string CentroCosto, string Sede, string Banco, string TipoCuenta, string NumeroCuenta, IReadOnlyList<string> SugeridosPorIa, string? EditadoPor,
    IReadOnlyList<AlertaDto> Alertas, bool Listo, bool RequiereConfirmarAlertas, string ClaseRiesgoRazon, string? EmpleadoCreadoPor, DateTimeOffset? EmpleadoCreadoEn);

/// <summary>Lo que ve el candidato de cada documento suyo.</summary>
public sealed record DocumentoPublicoDto(string Tipo, string Nombre, bool Obligatorio, string Estado, string? NombreArchivo, string? MotivoRechazo);

public sealed record ProcesoDto(
    Guid Id, string Codigo, string Etapa, PersonaDto Persona, Guid VacanteId, string CodigoVacante, string Vacante, string Cargo, Guid PostulacionId,
    Guid CandidatoId, string Empresa, string? EmpresaUsuaria, decimal SalarioMinimoVacante, decimal SalarioMaximoVacante, CondicionesDto Condiciones,
    IReadOnlyList<AlertaDto> Alertas, OfertaDto Oferta, string AprobadaPor, string? MotivoCancelacion, DateTimeOffset CreadoEn, DateTimeOffset ActualizadoEn,
    IReadOnlyList<DocumentoDto> Documentos, IReadOnlyList<AfiliacionDto> Afiliaciones, AltaDto Alta, PlanInduccionDto? Induccion);

public sealed record TareaInduccionDto(
    Guid Id, string Titulo, string Descripcion, string Responsable, string? Asignado, int Dia, DateOnly Fecha, bool Obligatoria, string Fuente, bool DeIa,
    string Estado, string? Nota, string? CerradaPor, DateTimeOffset? CerradaEn, bool Vencida);

/// <summary>Plan de inducción con su avance; las tareas vencidas son las pendientes cuya fecha ya pasó.</summary>
public sealed record PlanInduccionDto(
    string Estado, IReadOnlyList<TareaInduccionDto> Tareas, IReadOnlyList<RazonDto> Razones, IReadOnlyList<string> AlertasIa, string? Modelo, decimal CostoUsd,
    string? ErrorIa, DateTimeOffset? RedactadoEn, string? EditadoPor, string? AprobadoPor, DateTimeOffset? AprobadoEn, int Hechas, int Vencidas, bool Completo,
    string? CerradoPor, DateTimeOffset? CerradoEn, IReadOnlyList<string> Responsables);

/// <summary>Lo que ve el trabajador de su inducción: qué, quién y cuándo, sin notas internas.</summary>
public sealed record TareaPublicaDto(string Titulo, string Descripcion, string Responsable, DateOnly Fecha, string Estado);

/// <summary>Lo que ve el candidato en su enlace: la oferta con su nombre, sin datos internos.</summary>
public sealed record OfertaPublicaDto(
    string Empresa, string? EmpresaUsuaria, string Nombre, string Cargo, string Estado, string Carta, IReadOnlyList<ClausulaDto> Clausulas,
    CondicionesDto Condiciones, DateTimeOffset? RespondidaEn, bool? Acepto, string? Huella, string Etapa, IReadOnlyList<DocumentoPublicoDto> Documentos,
    IReadOnlyList<TareaPublicaDto> Induccion);

public sealed record EnlaceOfertaDto(Guid ProcesoId, string Token);

internal static class Mapeo
{
    public static CondicionesDto Dto(CondicionesContratacion c, ParametrosLegales p) =>
        new(c.Tipo.ToString(), ReglasContrato.Nombre(c.Tipo), c.SalarioCop, c.FechaIngreso, c.FechaFin, c.PeriodoPruebaDias, c.Labor, c.Jornada, c.Ciudad,
            ReglasContrato.TieneAuxilioTransporte(c, p), p.AuxilioTransporteCop);

    public static AlertaDto Dto(AlertaContrato a) => new(a.Nivel.ToString(), a.Texto, a.Fuente);

    public static RazonDto Dto(RazonOferta r) => new(r.Signo, r.Texto, r.Fuente);

    public static IReadOnlyList<ClausulaDto> Clausulas(ProcesoVinculacion p) =>
        [.. p.Oferta.Clausulas.Select(c => new ClausulaDto(c.Titulo, p.Personalizar(c.Texto)))];

    public static ProcesoDto Dto(ProcesoVinculacion p, ParametrosLegales parametros, DateOnly hoy)
    {
        var o = p.Oferta;
        var r = o.Respuesta;
        return new ProcesoDto(p.Id, p.Codigo, p.Etapa.ToString(),
            new PersonaDto(p.Persona.Nombre, p.Persona.Documento, p.Persona.Correo, p.Persona.Celular, p.Persona.Ciudad),
            p.Vacante.VacanteId, p.Vacante.Codigo, p.Vacante.Titulo, p.Vacante.Cargo, p.Vacante.PostulacionId, p.Vacante.CandidatoId, p.Empresa, p.EmpresaUsuaria,
            p.Vacante.SalarioMinimo, p.Vacante.SalarioMaximo, Dto(p.Condiciones, parametros), Alertas(p, parametros, hoy),
            new OfertaDto(o.Estado.ToString(), p.Personalizar(o.Carta), Clausulas(p), [.. o.Razones.Select(Dto)], o.AlertasIa, o.Modelo, o.CostoUsd, o.ErrorIa,
                o.RedactadaEn, o.EditadaPor, o.AprobadaPor, o.AprobadaEn, o.EnviadaEn,
                r is null ? null : new RespuestaOfertaDto(r.Acepta, r.NombreEscrito, r.Motivo, r.En, r.Ip, r.Navegador, r.Huella, p.EvidenciaIntegra())),
            p.AprobadaPor, p.MotivoCancelacion, p.CreadoEn, p.ActualizadoEn, [.. DocumentosDe(p).Select(Dto)],
            [.. p.Afiliaciones.Select(a => new AfiliacionDto(a.Tipo.ToString(), ReglasAlta.Nombre(a.Tipo), a.Estado.ToString(), a.Entidad, a.SugeridaDe, a.Radicado, a.FechaEfectiva,
                a.ClaseRiesgo, a.Por, a.En))],
            new AltaDto(p.Alta.Area, p.Alta.CentroCosto, p.Alta.Sede, p.Alta.Banco, p.Alta.TipoCuenta, p.Alta.NumeroCuenta, p.Alta.SugeridosPorIa, p.Alta.EditadoPor,
                p.Afiliaciones.Count == 0 ? [] : [.. ReglasAlta.Revisar(p, hoy).Select(a => new AlertaDto(a.Nivel.ToString(), a.Texto, a.Fuente))], p.ListoParaCrearEmpleado(hoy),
                ReglasAlta.RequiereConfirmarAlertas(p), ReglasAlta.ClaseSugerida(p.Vacante.Cargo).Razon, p.EmpleadoCreadoPor, p.EmpleadoCreadoEn),
            Induccion(p, hoy));
    }

    public static PlanInduccionDto? Induccion(ProcesoVinculacion p, DateOnly hoy)
    {
        if (p.Induccion is not { } plan)
        {
            return null;
        }

        var aprobado = plan.Estado == EstadoPlanInduccion.Aprobado;
        var tareas = plan.Tareas.Select(t => new TareaInduccionDto(t.Id, t.Titulo, t.Descripcion, t.Responsable, t.Asignado, t.Dia, t.Fecha, t.Obligatoria, t.Fuente, t.DeIa,
            t.Estado.ToString(), t.Nota, t.CerradaPor, t.CerradaEn, aprobado && t.Estado == EstadoTarea.Pendiente && t.Fecha < hoy)).ToList();
        return new PlanInduccionDto(plan.Estado.ToString(), tareas, [.. plan.Razones.Select(Dto)], plan.AlertasIa, plan.Modelo, plan.CostoUsd, plan.ErrorIa, plan.RedactadoEn,
            plan.EditadoPor, plan.AprobadoPor, plan.AprobadoEn, plan.Tareas.Count(t => t.Estado != EstadoTarea.Pendiente), tareas.Count(t => t.Vencida), p.OnboardingCompleto,
            p.OnboardingCerradoPor, p.OnboardingCerradoEn, ResponsablesInduccion.Todos);
    }

    /// <summary>El trabajador ve su inducción solo cuando RRHH aprobó el plan.</summary>
    public static IReadOnlyList<TareaPublicaDto> InduccionPublica(ProcesoVinculacion p) =>
        p.Induccion is { Estado: EstadoPlanInduccion.Aprobado } plan && p.Etapa is EtapaVinculacion.Onboarding or EtapaVinculacion.Vinculado
            ? [.. plan.Tareas.Select(t => new TareaPublicaDto(t.Titulo, t.Descripcion, t.Responsable, t.Fecha, t.Estado.ToString()))]
            : [];

    /// <summary>Si aceptó antes de existir la lista, se muestra la que le corresponde (se guarda al primer cambio).</summary>
    public static IReadOnlyList<DocumentoIngreso> DocumentosDe(ProcesoVinculacion p) =>
        p.Documentos.Count > 0 || p.Oferta.Estado != EstadoOferta.Aceptada || p.Etapa == EtapaVinculacion.Cancelado ? p.Documentos : RequisitosDocumentales.Para(p.Condiciones.Tipo);

    public static DocumentoDto Dto(DocumentoIngreso d) => new(d.Tipo.ToString(), RequisitosDocumentales.Nombre(d.Tipo), d.Obligatorio, d.LoCargaEmpresa, d.Estado.ToString(),
        d.NombreArchivo, d.SubidoPor, d.SubidoEn, d.Versiones,
        d.Lectura is { } l ? new LecturaDocumentoDto(l.TipoDetectado, l.Legible, l.TipoCorrecto, l.CoincideTitular, [.. l.Campos.Select(c => new CampoLeidoDto(c.Campo, c.Valor))],
            l.Observaciones, [.. l.Razones.Select(Dto)], l.Modelo, l.Error, l.LeidoEn) : null,
        d.RevisadoPor, d.RevisadoEn, d.MotivoRechazo, d.Antecedentes?.ToString(), d.ConceptoRequiereRevision);

    public static IReadOnlyList<AlertaDto> Alertas(ProcesoVinculacion p, ParametrosLegales parametros, DateOnly hoy) =>
        [.. ReglasContrato.Revisar(p.Condiciones, parametros, hoy, p.EmpresaUsuaria, p.Vacante.SalarioMinimo, p.Vacante.SalarioMaximo).Select(Dto)];
}
