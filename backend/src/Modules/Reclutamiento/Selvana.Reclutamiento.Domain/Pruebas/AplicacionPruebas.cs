using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Pruebas;

public enum EstadoAplicacion
{
    /// <summary>Se envió el enlace; el candidato no lo ha abierto o no ha aceptado.</summary>
    Enviada,
    EnCurso,
    Terminada,

    /// <summary>El candidato decidió no presentar las pruebas (no dio el consentimiento).</summary>
    Rechazada,
}

public enum EstadoPrueba
{
    Pendiente,
    EnCurso,
    Terminada,
}

public sealed class RespuestaPrueba
{
    public string PreguntaId { get; init; } = "";
    public string OpcionId { get; init; } = "";

    /// <summary>Milisegundos que tardó en responder (para detectar respuestas al azar).</summary>
    public int Milisegundos { get; init; }
}

/// <summary>Una prueba asignada al candidato, con su versión, sus respuestas y su resultado.</summary>
public sealed class PruebaAsignada
{
    public string Codigo { get; init; } = "";
    public string Version { get; init; } = "";
    public int Peso { get; init; } = 1;
    public int? Corte { get; init; }
    public EstadoPrueba Estado { get; set; }
    public DateTimeOffset? IniciadaEn { get; set; }
    public DateTimeOffset? TerminadaEn { get; set; }
    public List<RespuestaPrueba> Respuestas { get; set; } = [];
    public int? Puntaje { get; set; }
    public List<DimensionResultado> Dimensiones { get; set; } = [];
    public int Respondidas { get; set; }

    /// <summary>Señales para que una persona revise (no descalifican solas).</summary>
    public List<string> Alertas { get; set; } = [];
}

public static class AplicacionErrors
{
    public static readonly Error NoExiste = Error.NotFound("Pruebas.NoExiste", "Este enlace de pruebas no existe o ya no sirve.");
    public static readonly Error Vencida = Error.Conflict("Pruebas.Vencida", "El plazo para presentar las pruebas terminó. Si aún te interesa, escríbele a la empresa.");
    public static readonly Error SinConsentimiento = Error.Validation("Pruebas.SinConsentimiento", "Para presentar las pruebas primero debes aceptar el tratamiento de tus resultados.");
    public static readonly Error PruebaNoAsignada = Error.NotFound("Pruebas.NoAsignada", "Esa prueba no está en tu batería.");
    public static readonly Error YaTerminada = Error.Conflict("Pruebas.YaTerminada", "Ya presentaste esta prueba.");
    public static readonly Error NoIniciada = Error.Conflict("Pruebas.NoIniciada", "Primero abre la prueba para empezar.");
    public static readonly Error Rechazada = Error.Conflict("Pruebas.Rechazada", "Decidiste no presentar las pruebas.");
}

/// <summary>
/// Las pruebas que se le enviaron a un postulante. Se presentan con un enlace de un solo uso (se guarda su huella),
/// previa aceptación del tratamiento de resultados. Se califican al terminar cada prueba.
/// </summary>
public sealed class AplicacionPruebas : AggregateRoot<Guid>
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(72);

    /// <summary>Margen sobre el tiempo límite antes de marcar alerta (conexiones lentas).</summary>
    private static readonly TimeSpan Margen = TimeSpan.FromMinutes(1);

    private AplicacionPruebas(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public Guid VacanteId { get; private init; }
    public Guid PostulacionId { get; private init; }
    public Guid CandidatoId { get; private init; }
    public string Titular { get; private init; } = "";
    public string TokenHuella { get; private set; } = "";
    public List<PruebaAsignada> Pruebas { get; private set; } = [];
    public EstadoAplicacion Estado { get; private set; }
    public DateTimeOffset EnviadaEn { get; private set; }
    public string EnviadaPor { get; private set; } = "";
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? ConsentimientoEn { get; private set; }
    public DateTimeOffset? TerminadaEn { get; private set; }
    public int CambiosDePestana { get; private set; }

    public static AplicacionPruebas Enviar(
        Guid empresaId, Guid vacanteId, Guid postulacionId, Guid candidatoId, string titular, IEnumerable<PruebaEnBateria> bateria, string huella, string usuario,
        DateTimeOffset ahora) => new(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            VacanteId = vacanteId,
            PostulacionId = postulacionId,
            CandidatoId = candidatoId,
            Titular = titular,
            TokenHuella = huella,
            Pruebas = [.. bateria.Select(b => new PruebaAsignada
            {
                Codigo = b.Codigo, Version = SuitePsicotecnica.Buscar(b.Codigo)?.Version ?? "", Peso = b.Peso, Corte = b.Corte, Estado = EstadoPrueba.Pendiente,
            })],
            Estado = EstadoAplicacion.Enviada,
            EnviadaEn = ahora,
            EnviadaPor = usuario,
            VenceEn = ahora.Add(Vigencia),
        };

    /// <summary>Pruebas por presentar o a medias, y todavía en plazo: es un pendiente de la persona en su portal.</summary>
    public bool PorPresentar(DateTimeOffset ahora) => Estado is EstadoAplicacion.Enviada or EstadoAplicacion.EnCurso && ahora <= VenceEn;

    /// <summary>La persona las abre desde su portal (ADR 0013): enlace nuevo, mismo plazo.</summary>
    public bool AbrirDesdePortal(string huella, DateTimeOffset ahora)
    {
        if (!PorPresentar(ahora))
        {
            return false;
        }

        TokenHuella = huella;
        return true;
    }

    /// <summary>Reenviar: nuevo enlace y nuevo plazo; las pruebas ya terminadas se conservan.</summary>
    public void Reenviar(string huella, string usuario, DateTimeOffset ahora)
    {
        TokenHuella = huella;
        EnviadaPor = usuario;
        EnviadaEn = ahora;
        VenceEn = ahora.Add(Vigencia);
        if (Estado == EstadoAplicacion.Rechazada)
        {
            Estado = EstadoAplicacion.Enviada;
            ConsentimientoEn = null;
        }
    }

    public bool Vencida(DateTimeOffset ahora) => Estado is EstadoAplicacion.Enviada or EstadoAplicacion.EnCurso && ahora > VenceEn;

    public Result ResponderConsentimiento(bool acepta, DateTimeOffset ahora)
    {
        if (Vencida(ahora))
        {
            return Result.Failure(AplicacionErrors.Vencida);
        }

        if (Estado == EstadoAplicacion.Terminada)
        {
            return Result.Success();
        }

        if (!acepta)
        {
            Estado = EstadoAplicacion.Rechazada;
            return Result.Success();
        }

        ConsentimientoEn ??= ahora;
        Estado = EstadoAplicacion.EnCurso;
        return Result.Success();
    }

    /// <summary>Abre una prueba: empieza a correr su tiempo. Si ya estaba abierta, devuelve los segundos que le quedan.</summary>
    public Result<int> Iniciar(string codigo, DateTimeOffset ahora)
    {
        var verificacion = PuedeTrabajar(ahora);
        if (verificacion.IsFailure)
        {
            return verificacion.Error;
        }

        var p = Pruebas.FirstOrDefault(x => x.Codigo == codigo);
        var definicion = SuitePsicotecnica.Buscar(codigo);
        if (p is null || definicion is null)
        {
            return AplicacionErrors.PruebaNoAsignada;
        }

        if (p.Estado == EstadoPrueba.Terminada)
        {
            return AplicacionErrors.YaTerminada;
        }

        p.IniciadaEn ??= ahora;
        p.Estado = EstadoPrueba.EnCurso;
        var restante = p.IniciadaEn.Value.AddMinutes(definicion.Minutos) - ahora;
        return Math.Max(0, (int)restante.TotalSeconds);
    }

    /// <summary>Entrega las respuestas de una prueba, la califica y deja las alertas para revisión humana.</summary>
    public Result Terminar(string codigo, IReadOnlyList<RespuestaPrueba> respuestas, int cambiosDePestana, DateTimeOffset ahora)
    {
        var verificacion = PuedeTrabajar(ahora, toleraVencida: true);
        if (verificacion.IsFailure)
        {
            return verificacion;
        }

        var p = Pruebas.FirstOrDefault(x => x.Codigo == codigo);
        var definicion = SuitePsicotecnica.Buscar(codigo);
        if (p is null || definicion is null)
        {
            return Result.Failure(AplicacionErrors.PruebaNoAsignada);
        }

        if (p.Estado == EstadoPrueba.Terminada)
        {
            return Result.Failure(AplicacionErrors.YaTerminada);
        }

        if (p.IniciadaEn is null)
        {
            return Result.Failure(AplicacionErrors.NoIniciada);
        }

        var validas = respuestas.Where(r => definicion.Preguntas.Any(q => q.Id == r.PreguntaId && q.Opciones.Any(o => o.Id == r.OpcionId)))
            .GroupBy(r => r.PreguntaId).Select(g => g.Last()).ToList();
        var calificacion = CalificadorPruebas.Calificar(definicion, validas.ToDictionary(r => r.PreguntaId, r => r.OpcionId));

        p.Respuestas = validas;
        p.Puntaje = calificacion.Puntaje;
        p.Dimensiones = [.. calificacion.Dimensiones];
        p.Respondidas = calificacion.Respondidas;
        p.TerminadaEn = ahora;
        p.Estado = EstadoPrueba.Terminada;
        p.Alertas = Alertas(definicion, p, validas, cambiosDePestana, ahora);
        CambiosDePestana += Math.Max(0, cambiosDePestana);

        if (Pruebas.All(x => x.Estado == EstadoPrueba.Terminada))
        {
            Estado = EstadoAplicacion.Terminada;
            TerminadaEn = ahora;
        }

        return Result.Success();
    }

    /// <summary>Total ponderado de las pruebas terminadas que tienen puntaje (la personalidad no suma).</summary>
    public int? PuntajeTotal()
    {
        var conPuntaje = Pruebas.Where(p => p.Estado == EstadoPrueba.Terminada && p.Puntaje is not null).ToList();
        var pesos = conPuntaje.Sum(p => p.Peso);
        return pesos == 0 ? null : (int)Math.Round(conPuntaje.Sum(p => p.Puntaje!.Value * p.Peso) / (double)pesos);
    }

    private Result PuedeTrabajar(DateTimeOffset ahora, bool toleraVencida = false)
    {
        if (Estado == EstadoAplicacion.Rechazada)
        {
            return Result.Failure(AplicacionErrors.Rechazada);
        }

        if (ConsentimientoEn is null)
        {
            return Result.Failure(AplicacionErrors.SinConsentimiento);
        }

        // Una prueba que se empezó antes del vencimiento se puede entregar
        return !toleraVencida && Vencida(ahora) ? Result.Failure(AplicacionErrors.Vencida) : Result.Success();
    }

    private static List<string> Alertas(PruebaDefinicion definicion, PruebaAsignada p, List<RespuestaPrueba> respuestas, int cambiosDePestana, DateTimeOffset ahora)
    {
        var alertas = new List<string>();
        var duracion = ahora - p.IniciadaEn!.Value;
        if (duracion > TimeSpan.FromMinutes(definicion.Minutos) + Margen)
        {
            alertas.Add($"Entregó {(int)(duracion.TotalMinutes - definicion.Minutos)} min después del tiempo límite");
        }

        if (respuestas.Count < definicion.Preguntas.Count)
        {
            alertas.Add($"Dejó {definicion.Preguntas.Count - respuestas.Count} preguntas sin responder");
        }

        if (respuestas.Count >= 5 && respuestas.Average(r => r.Milisegundos) < 1500)
        {
            alertas.Add("Respondió demasiado rápido (menos de 2 segundos por pregunta en promedio)");
        }

        if (cambiosDePestana >= 3)
        {
            alertas.Add($"Salió de la prueba {cambiosDePestana} veces (cambió de pestaña o de aplicación)");
        }

        return alertas;
    }
}
