using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Clima.Domain.Pulsos;

/// <summary>Lo que respondió el empleado en su pulso: tres afirmaciones de 1 a 5, si recomendaría la empresa (0 a 10) y un comentario.</summary>
public sealed class RespuestaPulso
{
    public int Satisfaccion { get; init; }
    public int Claridad { get; init; }
    public int Apoyo { get; init; }
    public int Recomendaria { get; init; }
    public string Comentario { get; init; } = "";
    public DateTimeOffset En { get; init; }
}

/// <summary>Qué hizo RRHH con una alerta: la conversación con la persona y lo que acordaron.</summary>
public sealed class SeguimientoPulso
{
    public string Nota { get; init; } = "";
    public string Por { get; init; } = "";
    public DateTimeOffset En { get; init; }
}

public static class PulsoErrors
{
    public static readonly Error NoExiste = Error.NotFound("Clima.PulsoNoExiste", "Ese pulso no existe o ya no está pendiente.");
    public static readonly Error YaRespondido = Error.Conflict("Clima.PulsoRespondido", "Ya respondiste este pulso. ¡Gracias!");
    public static readonly Error Escala = Error.Validation("Clima.PulsoEscala", "Responde las tres preguntas de 1 a 5 y la recomendación de 0 a 10.");
    public static readonly Error SinNota = Error.Validation("Clima.SinNota", "Escriba qué conversó con la persona o qué acordaron.");
}

/// <summary>
/// C3 · Pulso de ingreso a los 30, 60 y 90 días. No es anónimo (sirve para alertar a tiempo), pero solo lo ve RRHH. Si las respuestas
/// son bajas queda en alerta de retiro temprano y la bandeja lo avisa; una persona conversa y registra el seguimiento.
/// </summary>
public sealed class Pulso : AggregateRoot<Guid>
{
    public static readonly int[] Hitos = [30, 60, 90];

    private Pulso(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public Guid EmpleadoId { get; private init; }
    public string Nombre { get; private init; } = "";
    public string Documento { get; private init; } = "";
    public string Correo { get; private init; } = "";
    public string Cargo { get; private init; } = "";
    public string Area { get; private init; } = "";
    public Guid? EmpresaUsuariaId { get; private init; }
    public DateOnly FechaIngreso { get; private init; }
    public int Hito { get; private init; }
    public DateOnly PendienteDesde { get; private init; }
    public RespuestaPulso? Respuesta { get; private set; }
    public bool Alerta { get; private set; }
    public SeguimientoPulso? Seguimiento { get; private set; }

    /// <summary>¿A qué hito llegó hoy la persona? El pulso se abre el día del hito y queda abierto 30 días.</summary>
    public static int? HitoVigente(DateOnly ingreso, DateOnly hoy) =>
        Hitos.Where(h => hoy >= ingreso.AddDays(h) && hoy < ingreso.AddDays(h + 30)).Select(h => (int?)h).FirstOrDefault();

    public static Pulso Programar(Guid empresaId, Guid empleadoId, string nombre, string documento, string correo, string cargo, string area, Guid? empresaUsuariaId,
        DateOnly ingreso, int hito) => new(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            EmpleadoId = empleadoId,
            Nombre = nombre,
            Documento = documento,
            Correo = correo,
            Cargo = cargo,
            Area = area,
            EmpresaUsuariaId = empresaUsuariaId,
            FechaIngreso = ingreso,
            Hito = hito,
            PendienteDesde = ingreso.AddDays(hito),
        };

    public Result Responder(int satisfaccion, int claridad, int apoyo, int recomendaria, string? comentario, DateTimeOffset ahora)
    {
        if (Respuesta is not null)
        {
            return Result.Failure(PulsoErrors.YaRespondido);
        }

        if (new[] { satisfaccion, claridad, apoyo }.Any(v => v is < 1 or > 5) || recomendaria is < 0 or > 10)
        {
            return Result.Failure(PulsoErrors.Escala);
        }

        Respuesta = new RespuestaPulso
        {
            Satisfaccion = satisfaccion, Claridad = claridad, Apoyo = apoyo, Recomendaria = recomendaria, Comentario = (comentario ?? "").Trim(), En = ahora,
        };
        // Alerta temprana: promedio de 2,5 o menos, o no recomendaría la empresa (6 o menos)
        Alerta = (satisfaccion + claridad + apoyo) / 3m <= 2.5m || recomendaria <= 6;
        return Result.Success();
    }

    public decimal? Promedio => Respuesta is { } r ? Math.Round((r.Satisfaccion + r.Claridad + r.Apoyo) / 3m, 1) : null;

    public Result RegistrarSeguimiento(string nota, string por, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(nota))
        {
            return Result.Failure(PulsoErrors.SinNota);
        }

        Seguimiento = new SeguimientoPulso { Nota = nota.Trim(), Por = por, En = ahora };
        return Result.Success();
    }
}
