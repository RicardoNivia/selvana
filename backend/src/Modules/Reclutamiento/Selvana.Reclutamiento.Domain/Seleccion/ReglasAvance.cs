using System.Globalization;
using Selvana.Reclutamiento.Domain.Candidatos;

namespace Selvana.Reclutamiento.Domain.Seleccion;

/// <summary>Sugerir: la IA propone el siguiente paso y una persona lo acepta. Automatico: la IA hace sola los pasos de bajo riesgo.</summary>
public enum ModoAvance
{
    Sugerir,
    Automatico,
}

/// <summary>
/// Criterios de paso entre etapas de una vacante (selección estructurada): se ven en pantalla, se pueden cambiar y la IA los aplica igual a todos.
/// En modo automático la IA solo invita a la preselección y envía pruebas; nunca descarta ni pasa a terna: eso siempre lo decide una persona.
/// </summary>
public sealed class ReglasAvance
{
    public ModoAvance Modo { get; set; } = ModoAvance.Sugerir;

    /// <summary>Afinidad mínima (0 a 100) para pasar de recibida a preselección.</summary>
    public int UmbralAfinidad { get; set; } = 70;

    /// <summary>Cuánto puede pasarse la aspiración salarial del máximo de la vacante (en %) sin pedir revisión.</summary>
    public int ToleranciaSalarial { get; set; } = 10;
}

/// <summary>Lo que se sabe de una postulación para decidir su siguiente paso. Nada de datos protegidos.</summary>
public sealed record SenalesPostulacion(
    EtapaPostulacion Etapa, int? Puntaje, string ResultadoFiltros, string? MotivoFiltro, string? EstadoConversacion, bool? Interesado, decimal? Aspiracion,
    string? EstadoPruebas, int? PromedioPruebas, bool? CumpleCortes, bool TieneBateria, decimal SalarioMaximo);

/// <summary>Un criterio de paso: cumple (true), no cumple (false) o aún no se sabe (null), con su evidencia.</summary>
public sealed record CriterioAvance(string Texto, bool? Cumple, string Evidencia);

/// <summary>
/// El siguiente paso que propone la IA para una postulación. <see cref="Accion"/>: Avanzar, Descartar, Invitar, EnviarPruebas, Esperar, Revisar o Ninguna.
/// <see cref="Automatica"/> dice si en modo automático la IA lo puede hacer sola (solo invitar a preselección y enviar pruebas).
/// </summary>
public sealed record SugerenciaAvance(string Accion, EtapaPostulacion? Destino, string Titulo, IReadOnlyList<CriterioAvance> Criterios, bool Automatica);

public static class EvaluadorAvance
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static SugerenciaAvance Evaluar(SenalesPostulacion s, ReglasAvance r) => s.Etapa switch
    {
        EtapaPostulacion.Recibida => Recibida(s, r),
        EtapaPostulacion.Preseleccion => Preseleccion(s, r),
        EtapaPostulacion.Pruebas => Pruebas(s),
        EtapaPostulacion.Terna => new("Contratar", null, "Lista para la decisión de contratar", [new("Aprobada para la terna por una persona", true, "Tablero")], false),
        _ => new("Ninguna", null, "Descartada con motivo registrado", [], false),
    };

    private static SugerenciaAvance Recibida(SenalesPostulacion s, ReglasAvance r)
    {
        var filtros = s.ResultadoFiltros switch
        {
            "NoCumple" => new CriterioAvance("Cumple los requisitos excluyentes", false, s.MotivoFiltro ?? "Filtros ATS"),
            "Cumple" or "Rescatada" => new CriterioAvance("Cumple los requisitos excluyentes", true, s.ResultadoFiltros == "Rescatada" ? "Rescatada por una persona" : "Filtros ATS"),
            "SinEvidencia" => new CriterioAvance("Cumple los requisitos excluyentes", null, s.MotivoFiltro ?? "Falta evidencia en la hoja de vida"),
            _ => new CriterioAvance("Cumple los requisitos excluyentes", null, "La vacante no tiene filtros"),
        };
        var afinidad = new CriterioAvance($"Afinidad de {r.UmbralAfinidad} o más", s.Puntaje is { } p ? p >= r.UmbralAfinidad : null,
            s.Puntaje is { } q ? $"Puntaje {q}/100" : "La IA aún no la califica");
        List<CriterioAvance> criterios = [filtros, afinidad];

        if (filtros.Cumple == false)
        {
            return new("Descartar", EtapaPostulacion.Descartada, $"No cumple: {s.MotivoFiltro}", criterios, false);
        }

        if (s.Puntaje is null)
        {
            return new("Esperar", null, "La IA está calificando su hoja de vida", criterios, false);
        }

        return afinidad.Cumple == true
            ? new("Avanzar", EtapaPostulacion.Preseleccion, "Invitar a la preselección", criterios, true)
            : new("Revisar", null, "Afinidad por debajo del umbral: revísela antes de decidir", criterios, false);
    }

    private static SugerenciaAvance Preseleccion(SenalesPostulacion s, ReglasAvance r)
    {
        var maximo = s.SalarioMaximo * (1 + r.ToleranciaSalarial / 100m);
        var chat = new CriterioAvance("Terminó la conversación de preselección", s.EstadoConversacion switch
        {
            "Completada" => true,
            null => false,
            _ => null,
        }, s.EstadoConversacion switch
        {
            null => "No ha sido invitada al chat",
            "Invitada" => "Invitada; aún no responde",
            "Abierta" => "Respondiendo el chat",
            "Escalada" => "El agente pidió ayuda a una persona",
            _ => "Chat completo",
        });
        var interes = new CriterioAvance("Confirma interés en la vacante", s.Interesado, s.Interesado is null ? "Sin respuesta todavía" : s.Interesado.Value ? "Dijo que sí" : "Dijo que no");
        var salario = new CriterioAvance($"Aspiración dentro del rango (+{r.ToleranciaSalarial} %)", s.Aspiracion is { } a ? a <= maximo : null,
            s.Aspiracion is { } x ? $"Aspira a {x.ToString("C0", Colombia)} · máximo {s.SalarioMaximo.ToString("C0", Colombia)}" : "Sin aspiración todavía");
        List<CriterioAvance> criterios = [chat, interes, salario];

        if (s.EstadoConversacion is null)
        {
            return new("Invitar", null, "Invitarla al chat de preselección", criterios, true);
        }

        if (s.EstadoConversacion == "Escalada")
        {
            return new("Revisar", null, "El agente de preselección pidió ayuda", criterios, false);
        }

        if (s.EstadoConversacion != "Completada")
        {
            return new("Esperar", null, "Esperando sus respuestas en el chat", criterios, false);
        }

        if (s.Interesado == false)
        {
            return new("Descartar", EtapaPostulacion.Descartada, "Dijo que no le interesa la vacante", criterios, false);
        }

        if (salario.Cumple == false)
        {
            return new("Revisar", null, "Su aspiración supera el rango: decida si conversa una contraoferta", criterios, false);
        }

        return s.TieneBateria
            ? new("Avanzar", EtapaPostulacion.Pruebas, "Enviarle las pruebas", criterios, true)
            : new("Avanzar", EtapaPostulacion.Terna, "Pasar a la terna (la vacante no tiene pruebas)", criterios, false);
    }

    private static SugerenciaAvance Pruebas(SenalesPostulacion s)
    {
        var terminadas = new CriterioAvance("Presentó todas las pruebas", s.EstadoPruebas switch
        {
            "Terminada" => true,
            null or "Rechazada" => false,
            _ => null,
        }, s.EstadoPruebas switch
        {
            null => "No se le han enviado",
            "Enviada" => "Enlace enviado; no ha empezado",
            "EnCurso" => "Las está presentando",
            "Rechazada" => "No dio el consentimiento",
            _ => s.PromedioPruebas is { } p ? $"Promedio {p}/100" : "Terminadas",
        });
        var cortes = new CriterioAvance("Supera los puntajes de corte", s.CumpleCortes, s.CumpleCortes is null ? "Sin resultados todavía" : s.CumpleCortes.Value ? "Todas sobre el corte" : "Alguna bajo el corte");
        List<CriterioAvance> criterios = [terminadas, cortes];

        return s.EstadoPruebas switch
        {
            null => new("EnviarPruebas", null, "Enviarle las pruebas", criterios, true),
            "Enviada" or "EnCurso" => new("Esperar", null, "Esperando que termine las pruebas", criterios, false),
            "Rechazada" => new("Revisar", null, "No quiso presentar las pruebas", criterios, false),
            _ when s.CumpleCortes == false => new("Revisar", null, "No superó un corte: la prueba orienta, la decisión es de una persona", criterios, false),
            _ => new("Avanzar", EtapaPostulacion.Terna, "Pasar a la terna", criterios, false),
        };
    }
}
