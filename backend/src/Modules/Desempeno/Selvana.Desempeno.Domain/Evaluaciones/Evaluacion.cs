using Selvana.Desempeno.Domain.Ciclos;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Desempeno.Domain.Evaluaciones;

public enum EstadoEvaluacion
{
    Pendiente,
    EnCurso,
    Calificada,
}

public enum Potencial
{
    Bajo,
    Medio,
    Alto,
}

public sealed class Evidencia
{
    public string Texto { get; init; } = "";
    public DateOnly Fecha { get; init; }
    public string Por { get; init; } = "";
}

/// <summary>Un objetivo del periodo (D2): meta, peso, avance y evidencias.</summary>
public sealed class Objetivo
{
    public Guid Id { get; init; }
    public string Descripcion { get; set; } = "";
    public string Meta { get; set; } = "";
    public int Peso { get; set; }

    /// <summary>De 0 a 100 %.</summary>
    public int Avance { get; set; }
    public List<Evidencia> Evidencias { get; set; } = [];
}

/// <summary>La calificación de una competencia de 1 a 5: la del líder cuenta para el puntaje; la del empleado y las de pares son referencia.</summary>
public sealed class CalificacionCompetencia
{
    public string Nombre { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public int Peso { get; init; }
    public int? Lider { get; set; }
    public int? Auto { get; set; }
    public List<int> Pares { get; set; } = [];
}

/// <summary>D3 · Lo que redactó el copiloto: fortalezas, oportunidades y un comentario. Nunca la calificación: esa la pone el líder.</summary>
public sealed class BorradorCopiloto
{
    /// <summary>Preparando, Listo, Fallido, Usado o Descartado.</summary>
    public string Estado { get; set; } = "Preparando";
    public List<string> Fortalezas { get; set; } = [];
    public List<string> Oportunidades { get; set; } = [];
    public string Comentario { get; set; } = "";
    public List<RazonPropuesta> Razones { get; set; } = [];
    public string? Modelo { get; set; }
    public decimal CostoUsd { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset PedidoEn { get; set; }
}

/// <summary>Una acción del plan de desarrollo individual (D4).</summary>
public sealed class AccionDesarrollo
{
    public Guid Id { get; init; }
    public string Competencia { get; set; } = "";
    public string Accion { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public bool Cumplida { get; set; }

    /// <summary>«Reglas» si salió de la brecha; «Manual» si la escribió una persona.</summary>
    public string Origen { get; set; } = "";
}

public sealed class AjusteCalibracion
{
    public int Caja { get; init; }
    public string Motivo { get; init; } = "";
    public string Por { get; init; } = "";
    public DateTimeOffset En { get; init; }
}

public static class EvaluacionErrors
{
    public static readonly Error NoExiste = Error.NotFound("Desempeno.EvaluacionNoExiste", "Esa evaluación no existe en su empresa.");
    public static readonly Error Escala = Error.Validation("Desempeno.Escala", "Las calificaciones van de 1 a 5.");
    public static readonly Error Pesos = Error.Validation("Desempeno.PesosObjetivos", "Los pesos de los objetivos deben sumar 100.");
    public static readonly Error SinCalificar = Error.Validation("Desempeno.SinCalificar", "Califique todas las competencias antes de cerrar la evaluación.");
    public static readonly Error Cerrada = Error.Conflict("Desempeno.Cerrada", "La evaluación ya está calificada: para cambiarla, reábrala.");
    public static readonly Error SinObjetivo = Error.NotFound("Desempeno.SinObjetivo", "Ese objetivo no está en la evaluación.");
    public static readonly Error SinAutoevaluacion = Error.Conflict("Desempeno.SinAutoevaluacion", "Este ciclo no lleva autoevaluación.");
    public static readonly Error SinMotivo = Error.Validation("Desempeno.SinMotivo", "Diga por qué el comité mueve a la persona en la matriz.");
    public static readonly Error SinBorrador = Error.Conflict("Desempeno.SinBorrador", "No hay un borrador del copiloto para usar.");
}

/// <summary>
/// La evaluación de un empleado en un ciclo: objetivos con avance y evidencias, competencias calificadas por el líder (y por el empleado o
/// sus pares según el alcance), comentarios, el borrador del copiloto, el potencial, la posición en el 9-box y el plan de desarrollo.
/// Calificar es una decisión humana: la IA solo redacta.
/// </summary>
public sealed class Evaluacion : AggregateRoot<Guid>
{
    private Evaluacion(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public Guid CicloId { get; private init; }
    public AlcanceEvaluacion Alcance { get; private init; }
    public int PesoObjetivos { get; private init; }
    public Guid EmpleadoId { get; private init; }
    public string Codigo { get; private init; } = "";
    public string Nombre { get; private init; } = "";
    public string Documento { get; private init; } = "";
    public string Correo { get; private init; } = "";
    public string Cargo { get; private init; } = "";
    public string Area { get; private init; } = "";
    public string Sede { get; private init; } = "";
    public Guid? EmpresaUsuariaId { get; private init; }
    public EstadoEvaluacion Estado { get; private set; }
    public List<Objetivo> Objetivos { get; private set; } = [];
    public List<CalificacionCompetencia> Competencias { get; private set; } = [];
    public string ComentarioLider { get; private set; } = "";
    public string ComentarioAuto { get; private set; } = "";
    public DateTimeOffset? AutoevaluadaEn { get; private set; }
    public string? CalificadaPor { get; private set; }
    public DateTimeOffset? CalificadaEn { get; private set; }
    public BorradorCopiloto? Borrador { get; private set; }
    public Potencial? Potencial { get; private set; }

    /// <summary>De 1 a 5 cuando está calificada; sirve para la matriz y para filtrar en la base.</summary>
    public decimal? Puntaje { get; private set; }
    public AjusteCalibracion? Ajuste { get; private set; }
    public List<AccionDesarrollo> Plan { get; private set; } = [];
    public DateTimeOffset CreadaEn { get; private init; }
    public DateTimeOffset ActualizadaEn { get; private set; }

    public bool LlevaAutoevaluacion => Alcance != AlcanceEvaluacion.Grados90;

    public static Evaluacion Crear(Ciclo ciclo, FormularioCargo formulario, Guid empleadoId, string codigo, string nombre, string documento, string correo, string area,
        string sede, Guid? empresaUsuariaId, DateTimeOffset ahora) => new(Guid.CreateVersion7())
        {
            EmpresaId = ciclo.EmpresaId,
            CicloId = ciclo.Id,
            Alcance = ciclo.Alcance,
            PesoObjetivos = ciclo.PesoObjetivos,
            EmpleadoId = empleadoId,
            Codigo = codigo,
            Nombre = nombre,
            Documento = documento,
            Correo = correo,
            Cargo = formulario.Cargo,
            Area = area,
            Sede = sede,
            EmpresaUsuariaId = empresaUsuariaId,
            Estado = EstadoEvaluacion.Pendiente,
            Objetivos = [.. formulario.Objetivos.Select(o => new Objetivo { Id = Guid.CreateVersion7(), Descripcion = o.Descripcion, Meta = o.Meta, Peso = o.Peso })],
            Competencias = [.. formulario.Competencias.Select(c => new CalificacionCompetencia { Nombre = c.Nombre, Descripcion = c.Descripcion, Peso = c.Peso })],
            CreadaEn = ahora,
            ActualizadaEn = ahora,
        };

    /// <summary>Objetivos del periodo (reemplaza la lista). Conserva avance y evidencias de los que ya existían.</summary>
    public Result DefinirObjetivos(IReadOnlyList<(Guid? Id, string Descripcion, string Meta, int Peso)> objetivos, DateTimeOffset ahora)
    {
        if (Estado == EstadoEvaluacion.Calificada)
        {
            return Result.Failure(EvaluacionErrors.Cerrada);
        }

        if (objetivos.Count > 0 && objetivos.Sum(o => o.Peso) != 100)
        {
            return Result.Failure(EvaluacionErrors.Pesos);
        }

        Objetivos = [.. objetivos.Select(o => Objetivos.FirstOrDefault(x => x.Id == o.Id) is { } previo
            ? new Objetivo { Id = previo.Id, Descripcion = o.Descripcion.Trim(), Meta = o.Meta.Trim(), Peso = o.Peso, Avance = previo.Avance, Evidencias = previo.Evidencias }
            : new Objetivo { Id = Guid.CreateVersion7(), Descripcion = o.Descripcion.Trim(), Meta = o.Meta.Trim(), Peso = o.Peso })];
        EnCurso(ahora);
        return Result.Success();
    }

    public Result RegistrarAvance(Guid objetivoId, int avance, string? evidencia, string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoEvaluacion.Calificada)
        {
            return Result.Failure(EvaluacionErrors.Cerrada);
        }

        if (Objetivos.FirstOrDefault(o => o.Id == objetivoId) is not { } o)
        {
            return Result.Failure(EvaluacionErrors.SinObjetivo);
        }

        o.Avance = Math.Clamp(avance, 0, 100);
        if (!string.IsNullOrWhiteSpace(evidencia))
        {
            o.Evidencias = [.. o.Evidencias, new Evidencia { Texto = evidencia.Trim(), Fecha = DateOnly.FromDateTime(ahora.UtcDateTime), Por = por }];
        }

        Objetivos = [.. Objetivos];
        EnCurso(ahora);
        return Result.Success();
    }

    /// <summary>180° y 360°: el empleado se califica desde su portal y comenta. No mueve el puntaje.</summary>
    public Result Autoevaluar(IReadOnlyDictionary<string, int> calificaciones, string comentario, DateTimeOffset ahora)
    {
        if (!LlevaAutoevaluacion)
        {
            return Result.Failure(EvaluacionErrors.SinAutoevaluacion);
        }

        if (Estado == EstadoEvaluacion.Calificada)
        {
            return Result.Failure(EvaluacionErrors.Cerrada);
        }

        if (calificaciones.Values.Any(v => v is < 1 or > 5))
        {
            return Result.Failure(EvaluacionErrors.Escala);
        }

        foreach (var c in Competencias)
        {
            c.Auto = calificaciones.TryGetValue(c.Nombre, out var v) ? v : c.Auto;
        }

        Competencias = [.. Competencias];
        ComentarioAuto = (comentario ?? "").Trim();
        AutoevaluadaEn = ahora;
        EnCurso(ahora);
        return Result.Success();
    }

    /// <summary>360°: la calificación de un par (anónima: solo se guarda el número).</summary>
    public Result RegistrarPar(IReadOnlyDictionary<string, int> calificaciones, DateTimeOffset ahora)
    {
        if (Alcance != AlcanceEvaluacion.Grados360)
        {
            return Result.Failure(EvaluacionErrors.SinAutoevaluacion);
        }

        if (calificaciones.Values.Any(v => v is < 1 or > 5))
        {
            return Result.Failure(EvaluacionErrors.Escala);
        }

        foreach (var c in Competencias.Where(c => calificaciones.ContainsKey(c.Nombre)))
        {
            c.Pares = [.. c.Pares, calificaciones[c.Nombre]];
        }

        Competencias = [.. Competencias];
        EnCurso(ahora);
        return Result.Success();
    }

    public void PedirBorrador(DateTimeOffset ahora)
    {
        Borrador = new BorradorCopiloto { Estado = "Preparando", PedidoEn = ahora };
        ActualizadaEn = ahora;
    }

    public void RegistrarBorrador(IReadOnlyList<string> fortalezas, IReadOnlyList<string> oportunidades, string comentario, IReadOnlyList<RazonPropuesta> razones,
        string modelo, decimal costo, string? error, DateTimeOffset ahora)
    {
        Borrador = new BorradorCopiloto
        {
            Estado = error is null ? "Listo" : "Fallido", Fortalezas = [.. fortalezas], Oportunidades = [.. oportunidades], Comentario = comentario,
            Razones = [.. razones], Modelo = modelo, CostoUsd = costo, Error = error, PedidoEn = Borrador?.PedidoEn ?? ahora,
        };
        ActualizadaEn = ahora;
    }

    /// <summary>El líder usa el borrador como punto de partida: el comentario pasa a ser suyo (lo edita antes de calificar).</summary>
    public Result UsarBorrador(DateTimeOffset ahora)
    {
        if (Borrador is not { Estado: "Listo" } b)
        {
            return Result.Failure(EvaluacionErrors.SinBorrador);
        }

        ComentarioLider = b.Comentario;
        Borrador = new BorradorCopiloto
        {
            Estado = "Usado", Fortalezas = b.Fortalezas, Oportunidades = b.Oportunidades, Comentario = b.Comentario, Razones = b.Razones, Modelo = b.Modelo,
            CostoUsd = b.CostoUsd, PedidoEn = b.PedidoEn,
        };
        ActualizadaEn = ahora;
        return Result.Success();
    }

    /// <summary>
    /// Decisión humana: el líder califica cada competencia, deja su comentario y el potencial. Queda el puntaje (objetivos y competencias
    /// según los pesos del ciclo) y, si hay brechas, el plan de desarrollo propuesto por reglas.
    /// </summary>
    public Result Calificar(IReadOnlyDictionary<string, int> calificaciones, string comentario, Potencial potencial, string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoEvaluacion.Calificada)
        {
            return Result.Failure(EvaluacionErrors.Cerrada);
        }

        if (calificaciones.Values.Any(v => v is < 1 or > 5))
        {
            return Result.Failure(EvaluacionErrors.Escala);
        }

        if (Competencias.Any(c => !calificaciones.ContainsKey(c.Nombre)))
        {
            return Result.Failure(EvaluacionErrors.SinCalificar);
        }

        foreach (var c in Competencias)
        {
            c.Lider = calificaciones[c.Nombre];
        }

        Competencias = [.. Competencias];
        ComentarioLider = (comentario ?? "").Trim();
        Potencial = potencial;
        Puntaje = CalcularPuntaje();
        Estado = EstadoEvaluacion.Calificada;
        CalificadaPor = por;
        CalificadaEn = ahora;
        if (Plan.Count == 0)
        {
            Plan = ProponerPlan(ahora);
        }

        ActualizadaEn = ahora;
        return Result.Success();
    }

    /// <summary>Para corregir: vuelve a «en curso» y conserva lo calificado como punto de partida.</summary>
    public void Reabrir(DateTimeOffset ahora)
    {
        Estado = EstadoEvaluacion.EnCurso;
        Puntaje = null;
        Ajuste = null;
        ActualizadaEn = ahora;
    }

    /// <summary>Puntaje de 1 a 5: el avance de los objetivos (0 a 100 %) se lleva a la escala y se pondera con las competencias.</summary>
    public decimal CalcularPuntaje()
    {
        var competencias = Competencias.Where(c => c.Lider is not null).ToList();
        decimal? notaCompetencias = competencias.Count == 0 ? null
            : competencias.Sum(c => c.Lider!.Value * (decimal)c.Peso) / Math.Max(1, competencias.Sum(c => c.Peso));
        decimal? notaObjetivos = Objetivos.Count == 0 || PesoObjetivos == 0 ? null
            : 1 + Objetivos.Sum(o => o.Avance * (decimal)o.Peso) / Math.Max(1, Objetivos.Sum(o => o.Peso)) / 100m * 4;

        // Mientras el líder no califica, el puntaje en curso es solo el de los objetivos (y al revés)
        return Math.Round((notaObjetivos, notaCompetencias) switch
        {
            ({ } o, { } c) => (o * PesoObjetivos + c * (100 - PesoObjetivos)) / 100m,
            ({ } o, null) => o,
            (null, { } c) => c,
            _ => 0m,
        }, 2);
    }

    /// <summary>Desempeño en la matriz: bajo (&lt; 2,6), medio (&lt; 3,6) o alto.</summary>
    public static int NivelDesempeno(decimal puntaje) => puntaje < 2.6m ? 0 : puntaje < 3.6m ? 1 : 2;

    /// <summary>Caja del 9-box (1 a 9): filas por potencial (bajo arriba → 1-3), columnas por desempeño. La del comité manda si la movió.</summary>
    public int? Caja => Ajuste?.Caja ?? (Puntaje is { } p && Potencial is { } pot ? (int)pot * 3 + NivelDesempeno(p) + 1 : null);

    /// <summary>D4 · El comité de calibración mueve a la persona en la matriz, siempre con su motivo.</summary>
    public Result Calibrar(int caja, string motivo, string por, DateTimeOffset ahora)
    {
        if (Estado != EstadoEvaluacion.Calificada)
        {
            return Result.Failure(EvaluacionErrors.SinCalificar);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result.Failure(EvaluacionErrors.SinMotivo);
        }

        Ajuste = new AjusteCalibracion { Caja = Math.Clamp(caja, 1, 9), Motivo = motivo.Trim(), Por = por, En = ahora };
        ActualizadaEn = ahora;
        return Result.Success();
    }

    public void DefinirPlan(IReadOnlyList<(Guid? Id, string Competencia, string Accion, DateOnly Fecha, bool Cumplida)> acciones, DateTimeOffset ahora)
    {
        Plan = [.. acciones.Select(a => new AccionDesarrollo
        {
            Id = a.Id ?? Guid.CreateVersion7(), Competencia = a.Competencia.Trim(), Accion = a.Accion.Trim(), Fecha = a.Fecha, Cumplida = a.Cumplida,
            Origen = Plan.FirstOrDefault(p => p.Id == a.Id)?.Origen ?? "Manual",
        })];
        ActualizadaEn = ahora;
    }

    /// <summary>Plan base por reglas: una acción por cada competencia calificada en 1 o 2 (brecha), a 90 días.</summary>
    private List<AccionDesarrollo> ProponerPlan(DateTimeOffset ahora)
    {
        var fecha = DateOnly.FromDateTime(ahora.UtcDateTime).AddDays(90);
        return [.. Competencias.Where(c => c.Lider is <= 2).Select(c => new AccionDesarrollo
        {
            Id = Guid.CreateVersion7(), Competencia = c.Nombre, Fecha = fecha, Origen = "Reglas",
            Accion = $"Acompañamiento del líder y una meta práctica en «{c.Nombre.ToLowerInvariant()}», con seguimiento mensual",
        })];
    }

    private void EnCurso(DateTimeOffset ahora)
    {
        if (Estado == EstadoEvaluacion.Pendiente)
        {
            Estado = EstadoEvaluacion.EnCurso;
        }

        ActualizadaEn = ahora;
    }
}
