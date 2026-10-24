using Selvana.SharedKernel.Results;

namespace Selvana.Vinculacion.Domain.Procesos;

public enum TipoAfiliacion
{
    Eps,
    Pension,
    Arl,
    CajaCompensacion,
}

public enum EstadoAfiliacion
{
    Pendiente,

    /// <summary>RRHH la radicó y espera confirmación de la entidad.</summary>
    Solicitada,
    Confirmada,

    /// <summary>No aplica (por ejemplo, pensión en contrato de aprendizaje en etapa lectiva).</summary>
    NoAplica,
}

public sealed class Afiliacion
{
    public TipoAfiliacion Tipo { get; init; }
    public EstadoAfiliacion Estado { get; set; }
    public string Entidad { get; set; } = "";

    /// <summary>De dónde salió la entidad sugerida («Certificado de afiliación a la EPS» leído por la IA) o null si la escribió una persona.</summary>
    public string? SugeridaDe { get; set; }
    public string? Radicado { get; set; }
    public DateOnly? FechaEfectiva { get; set; }

    /// <summary>Solo ARL: clase de riesgo de I a V.</summary>
    public int? ClaseRiesgo { get; set; }
    public string? Por { get; set; }
    public DateTimeOffset? En { get; set; }
}

/// <summary>Lo que necesita el alta en la nómina además del contrato. Lo prellenan los documentos leídos; editarlo lo confirma.</summary>
public sealed class DatosAlta
{
    public string Area { get; set; } = "";
    public string CentroCosto { get; set; } = "";
    public string Sede { get; set; } = "";
    public string Banco { get; set; } = "";
    public string TipoCuenta { get; set; } = "";
    public string NumeroCuenta { get; set; } = "";

    /// <summary>Campos que salieron de la lectura de la IA y nadie ha confirmado.</summary>
    public List<string> SugeridosPorIa { get; set; } = [];
    public string? EditadoPor { get; set; }
}

public sealed record AlertaAlta(NivelAlerta Nivel, string Texto, string Fuente);

public static class AltaErrors
{
    public static readonly Error NoEnAfiliaciones = Error.Conflict("Alta.NoEnEtapa", "Las afiliaciones se gestionan cuando los documentos están aprobados.");
    public static readonly Error SinEntidad = Error.Validation("Alta.SinEntidad", "Escriba la entidad (EPS, fondo, ARL o caja).");
    public static readonly Error ClaseRiesgo = Error.Validation("Alta.ClaseRiesgo", "La clase de riesgo de la ARL va de I a V.");
    public static readonly Error NoAplicaSoloPension = Error.Validation("Alta.NoAplica", "Solo la pensión puede no aplicar, y únicamente en contrato de aprendizaje.");
    public static readonly Error Incompleto = Error.Conflict("Alta.Incompleta", "Faltan afiliaciones por confirmar o datos del alta. Revise las alertas en rojo.");
    public static readonly Error SinConfirmarAlertas = Error.Validation("Alta.Alertas",
        "Hay alertas de antecedentes o del examen médico: confirme que las revisó antes de crear el empleado.");
}

/// <summary>Reglas del alta (sin IA): qué afiliaciones aplican, cuándo debe cubrir la ARL y qué falta para crear el empleado.</summary>
public static class ReglasAlta
{
    private static readonly string[] RiesgoAlto = ["altura", "electric", "soldad", "minero", "construc"];
    private static readonly string[] RiesgoMedio = ["montacarg", "bodega", "operari", "conductor", "mensajer", "alistador", "produc"];
    private static readonly string[] RiesgoBajo = ["asesor", "analista", "auxiliar administrativ", "cajer", "contact", "recepcion"];

    public static string Nombre(TipoAfiliacion t) => t switch
    {
        TipoAfiliacion.Eps => "EPS (salud)",
        TipoAfiliacion.Pension => "Fondo de pensiones",
        TipoAfiliacion.Arl => "ARL (riesgos laborales)",
        TipoAfiliacion.CajaCompensacion => "Caja de compensación",
        _ => t.ToString(),
    };

    /// <summary>Sugerencia de clase de riesgo por palabras del cargo; siempre la confirma RRHH.</summary>
    public static (int Clase, string Razon) ClaseSugerida(string cargo)
    {
        var c = (cargo ?? "").ToLowerInvariant();
        if (RiesgoAlto.Any(c.Contains))
        {
            return (4, "Cargo con trabajo en alturas, eléctrico o de obra");
        }

        if (RiesgoMedio.Any(c.Contains))
        {
            return (3, "Cargo operativo con maquinaria o manipulación de carga");
        }

        return RiesgoBajo.Any(c.Contains)
            ? (1, "Cargo administrativo o de oficina")
            : (2, "Sin datos suficientes: revise la clase según la actividad");
    }

    public static IReadOnlyList<AlertaAlta> Revisar(ProcesoVinculacion p, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(p);
        var alertas = new List<AlertaAlta>();
        var ingreso = p.Condiciones.FechaIngreso;
        foreach (var a in p.Afiliaciones.Where(a => a.Estado is EstadoAfiliacion.Pendiente or EstadoAfiliacion.Solicitada))
        {
            alertas.Add(new AlertaAlta(NivelAlerta.Bloquea, $"{Nombre(a.Tipo)}: {(a.Estado == EstadoAfiliacion.Solicitada ? "radicada, falta la confirmación" : "falta afiliar")}.", "Afiliaciones"));
        }

        if (p.Afiliaciones.FirstOrDefault(a => a.Tipo == TipoAfiliacion.Arl) is { } arl)
        {
            if (arl.Estado == EstadoAfiliacion.Confirmada && arl.FechaEfectiva is { } f && f > ingreso.AddDays(-1))
            {
                alertas.Add(new AlertaAlta(NivelAlerta.Bloquea, $"La ARL cubre desde el {f:dd/MM/yyyy}: debe cubrir al menos desde el día anterior al ingreso ({ingreso.AddDays(-1):dd/MM/yyyy}).",
                    "Decreto 1072 de 2015"));
            }
            else if (arl.Estado != EstadoAfiliacion.Confirmada && ingreso.DayNumber - hoy.DayNumber <= 2)
            {
                alertas.Add(new AlertaAlta(NivelAlerta.Bloquea, "Ingresa en menos de 2 días y la ARL no está confirmada: sin ARL no puede empezar a trabajar.", "Decreto 1072 de 2015"));
            }
        }

        var d = p.Alta;
        if (string.IsNullOrWhiteSpace(d.NumeroCuenta) || string.IsNullOrWhiteSpace(d.Banco))
        {
            alertas.Add(new AlertaAlta(NivelAlerta.Bloquea, "Faltan el banco o el número de cuenta para pagar la nómina.", "Datos del alta"));
        }

        if (string.IsNullOrWhiteSpace(d.CentroCosto))
        {
            alertas.Add(new AlertaAlta(NivelAlerta.Bloquea, "Falta el centro de costo.", "Datos del alta"));
        }

        if (d.SugeridosPorIa.Count > 0)
        {
            alertas.Add(new AlertaAlta(NivelAlerta.Revisar, $"Confirme lo que leyó la IA: {string.Join(", ", d.SugeridosPorIa)}.", "Datos del alta"));
        }

        if (p.Documento(TipoDocumentoIngreso.Antecedentes)?.Antecedentes == ResultadoAntecedentes.ConAnotaciones)
        {
            alertas.Add(new AlertaAlta(NivelAlerta.Revisar, "Los antecedentes tienen anotaciones: revíselos antes de crear el empleado.", "Antecedentes"));
        }

        if (p.Documento(TipoDocumentoIngreso.ExamenMedico)?.ConceptoRequiereRevision == true)
        {
            alertas.Add(new AlertaAlta(NivelAlerta.Revisar, "El examen médico pide revisión (restricciones o no apto): confirme que el cargo es compatible.", "Examen médico"));
        }

        return alertas;
    }

    /// <summary>Alertas de antecedentes o examen médico que una persona debe confirmar que revisó.</summary>
    public static bool RequiereConfirmarAlertas(ProcesoVinculacion p) =>
        p.Documento(TipoDocumentoIngreso.Antecedentes)?.Antecedentes == ResultadoAntecedentes.ConAnotaciones
        || p.Documento(TipoDocumentoIngreso.ExamenMedico)?.ConceptoRequiereRevision == true;
}
