using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Filtros;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Recontacto;

public enum EstadoSugerenciaBanco
{
    /// <summary>Espera la decisión de una persona.</summary>
    Sugerida,

    /// <summary>Una persona decidió invitarlo a postularse: el candidato se postula él mismo, con su autorización.</summary>
    Invitada,

    /// <summary>Una persona decidió que no encaja, con su motivo.</summary>
    NoEncaja,
}

/// <summary>Una regla que coincidió entre la vacante y la hoja de vida, con su evidencia («Cargo», «Auxiliar de bodega», «HV · experiencia 1»).</summary>
public sealed class CoincidenciaBanco
{
    public string Tipo { get; init; } = "";
    public string Texto { get; init; } = "";
    public string Evidencia { get; init; } = "";
}

/// <summary>Lo que encontraron las reglas: puntaje para ordenar y elegir a quién califica la IA, y las coincidencias con evidencia.</summary>
public sealed record EvaluacionBanco(int Puntaje, IReadOnlyList<CoincidenciaBanco> Coincidencias)
{
    /// <summary>La ciudad sola no basta: tiene que coincidir el cargo o algún requisito.</summary>
    public bool Encaja => Puntaje >= RecontactoBanco.PuntajeMinimo && Coincidencias.Any(c => c.Tipo != RecontactoBanco.TipoCiudad);
}

/// <summary>
/// Recontacto del banco de talento, por reglas (sin IA): a quién se puede volver a contactar y qué tanto encaja con una vacante.
/// Nunca mira edad, género, estado civil, foto ni dirección.
/// </summary>
public static class RecontactoBanco
{
    public const string TipoCargo = "Cargo";
    public const string TipoCiudad = "Ciudad";
    public const string TipoRequisito = "Requisito";
    public const int PuntajeMinimo = 30;

    /// <summary>Frase de la autorización (v3 en adelante) que permite ofrecerle otras vacantes.</summary>
    private const string FraseAutorizacion = "vacantes similares";

    /// <summary>
    /// Ley 1581: solo si no está suprimido, no se ha postulado ya a esta vacante y aceptó que le ofrezcan vacantes similares
    /// (se mira el texto que aceptó, no solo la versión).
    /// </summary>
    public static bool PuedeRecontactarse(Candidato candidato, Guid vacanteId)
    {
        ArgumentNullException.ThrowIfNull(candidato);
        return !candidato.Suprimido
            && candidato.Postulaciones.All(p => p.VacanteId != vacanteId)
            && candidato.Postulaciones.Any(p => Normalizacion.Simplificar(p.Autorizacion.Texto).Contains(FraseAutorizacion, StringComparison.Ordinal));
    }

    public static EvaluacionBanco Evaluar(Vacante vacante, DatosCandidato datos, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(vacante);
        ArgumentNullException.ThrowIfNull(datos);
        var coincidencias = new List<CoincidenciaBanco>();
        var puntaje = 0;

        // Cargo: el mismo cargo normalizado («bodeguero» = Auxiliar de bodega) en alguna experiencia
        var cargo = Normalizacion.Cargo(string.IsNullOrWhiteSpace(vacante.Cargo) ? vacante.Titulo : vacante.Cargo);
        var cargoSimple = Normalizacion.Simplificar(cargo);
        for (var i = 0; i < datos.Experiencia.Count && cargoSimple.Length > 0; i++)
        {
            var e = datos.Experiencia[i];
            if (Normalizacion.Simplificar(Normalizacion.Cargo(e.Cargo)) == cargoSimple)
            {
                coincidencias.Add(new CoincidenciaBanco { Tipo = TipoCargo, Texto = $"Trabajó como {e.Cargo.Trim()}", Evidencia = $"HV · experiencia {i + 1}" });
                puntaje += 50;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(datos.Ciudad) && Normalizacion.Simplificar(datos.Ciudad) == Normalizacion.Simplificar(vacante.Ciudad))
        {
            coincidencias.Add(new CoincidenciaBanco { Tipo = TipoCiudad, Texto = $"Vive en {datos.Ciudad.Trim()}", Evidencia = "HV · ciudad" });
            puntaje += 20;
        }

        // Requisitos que la hoja de vida demuestra (hasta tres cuentan para el puntaje)
        var meses = RevisionHojaDeVida.MesesDeExperiencia(datos, hoy);
        var requisitos = 0;
        foreach (var r in vacante.Requisitos.Where(r => !VariablesProtegidas.Menciona(r)))
        {
            var demostrado = EvaluadorFiltros.AniosPedidos(r) is { } anios
                ? meses >= anios * 12 ? ($"Tiene {meses / 12} {(meses / 12 == 1 ? "año" : "años")} de experiencia con fechas", "HV · experiencia") : ((string, string)?)null
                : EvaluadorFiltros.Demuestra(r, datos);
            if (demostrado is { } d)
            {
                var detalle = d.Item1.TrimEnd('.');
                coincidencias.Add(new CoincidenciaBanco { Tipo = TipoRequisito, Texto = $"{r.Trim()}: {char.ToLowerInvariant(detalle[0])}{detalle[1..]}", Evidencia = d.Item2 });
                if (++requisitos <= 3)
                {
                    puntaje += 10;
                }
            }
        }

        return new EvaluacionBanco(Math.Min(puntaje, 100), coincidencias);
    }
}

public static class SugerenciaBancoErrors
{
    public static readonly Error NoExiste = Error.NotFound("SugerenciaBanco.NoExiste", "Esa sugerencia del banco no existe.");
    public static readonly Error YaDecidida = Error.Conflict("SugerenciaBanco.YaDecidida", "Ya se decidió sobre esta persona.");
    public static readonly Error SinMotivo = Error.Validation("SugerenciaBanco.SinMotivo", "Diga por qué no encaja: queda en la auditoría.");
    public static readonly Error NoRecontactable = Error.Conflict("SugerenciaBanco.NoRecontactable",
        "Esta persona ya no autoriza que le ofrezcan vacantes (suprimió sus datos o revocó la autorización).");
}

/// <summary>
/// Una persona del banco de talento que encaja con una vacante publicada. Las reglas la encuentran, el agente de ranking la califica
/// sobre su perfil anónimo y una persona decide si la invita a postularse. Nadie queda postulado solo.
/// </summary>
public sealed class SugerenciaBanco : AggregateRoot<Guid>
{
    private SugerenciaBanco(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public Guid VacanteId { get; private init; }
    public Guid CandidatoId { get; private init; }
    public int PuntajeReglas { get; private init; }
    public List<CoincidenciaBanco> Coincidencias { get; private init; } = [];

    /// <summary>Lo que dijo el agente de ranking (violeta hasta que una persona decide).</summary>
    public AfinidadPostulacion? Afinidad { get; private set; }
    public EstadoSugerenciaBanco Estado { get; private set; } = EstadoSugerenciaBanco.Sugerida;
    public string? DecididaPor { get; private set; }
    public DateTimeOffset? DecididaEn { get; private set; }
    public string? Motivo { get; private set; }
    public DateTimeOffset CreadaEn { get; private init; }

    public bool Calculando => Afinidad?.Estado == EstadoAfinidad.Calculando;

    /// <summary>Puntaje para ordenar: el de la IA si ya calificó; si no, el de las reglas.</summary>
    public int PuntajeOrden => Afinidad?.PuntajeIa ?? PuntajeReglas;

    public static SugerenciaBanco Crear(Guid empresaId, Guid vacanteId, Guid candidatoId, EvaluacionBanco evaluacion, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(evaluacion);
        return new SugerenciaBanco(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            VacanteId = vacanteId,
            CandidatoId = candidatoId,
            PuntajeReglas = evaluacion.Puntaje,
            Coincidencias = [.. evaluacion.Coincidencias],
            CreadaEn = ahora,
        };
    }

    public void PedirAfinidad() => Afinidad = new AfinidadPostulacion { Estado = EstadoAfinidad.Calculando };

    public void RegistrarAfinidad(int puntaje, IEnumerable<RazonPuntaje> razones, IEnumerable<string> cumplidos, IEnumerable<string> faltantes,
        string modelo, DateTimeOffset ahora, int razonesRetiradas) =>
        Afinidad = new AfinidadPostulacion
        {
            Estado = EstadoAfinidad.Lista,
            PuntajeIa = Math.Clamp(puntaje, 0, 100),
            Razones = [.. razones.Take(5)],
            Cumplidos = [.. cumplidos],
            Faltantes = [.. faltantes],
            Modelo = modelo,
            CalculadaEn = ahora,
            RazonesRetiradas = razonesRetiradas,
        };

    public void RegistrarFallaAfinidad(string mensaje) =>
        Afinidad = new AfinidadPostulacion { Estado = EstadoAfinidad.Fallo, Mensaje = mensaje.Length <= 300 ? mensaje : mensaje[..300] };

    /// <summary>Una persona decide invitarlo: se le comparte el enlace de la vacante y él decide si se postula.</summary>
    public Result Invitar(bool recontactable, string por, DateTimeOffset ahora)
    {
        if (Estado != EstadoSugerenciaBanco.Sugerida)
        {
            return Result.Failure(SugerenciaBancoErrors.YaDecidida);
        }

        if (!recontactable)
        {
            return Result.Failure(SugerenciaBancoErrors.NoRecontactable);
        }

        Decidir(EstadoSugerenciaBanco.Invitada, null, por, ahora);
        return Result.Success();
    }

    /// <summary>Una persona decide que no encaja; el motivo no puede basarse en variables protegidas.</summary>
    public Result DescartarSugerencia(string? motivo, string por, DateTimeOffset ahora)
    {
        if (Estado != EstadoSugerenciaBanco.Sugerida)
        {
            return Result.Failure(SugerenciaBancoErrors.YaDecidida);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result.Failure(SugerenciaBancoErrors.SinMotivo);
        }

        if (VariablesProtegidas.Menciona(motivo))
        {
            return Result.Failure(RetroalimentacionErrors.MotivoDiscriminatorio);
        }

        Decidir(EstadoSugerenciaBanco.NoEncaja, motivo.Trim(), por, ahora);
        return Result.Success();
    }

    private void Decidir(EstadoSugerenciaBanco estado, string? motivo, string por, DateTimeOffset ahora)
    {
        Estado = estado;
        Motivo = motivo;
        DecididaPor = por;
        DecididaEn = ahora;
    }
}
