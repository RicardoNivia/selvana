using System.Text.RegularExpressions;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Candidatos;

public sealed class ExperienciaLaboral
{
    public string Cargo { get; init; } = "";
    public string Empresa { get; init; } = "";
    public string Desde { get; init; } = "";
    public string Hasta { get; init; } = "";
}

public sealed class EstudioRealizado
{
    public string Titulo { get; init; } = "";
    public string Institucion { get; init; } = "";
    public string Anio { get; init; } = "";
}

/// <summary>Datos de la hoja de vida, leídos por la IA y confirmados por el candidato. Sin edad, género, estado civil ni foto.</summary>
public sealed class DatosCandidato
{
    public string Nombre { get; init; } = "";
    public string Documento { get; init; } = "";
    public string Celular { get; init; } = "";
    public string Correo { get; init; } = "";
    public string Ciudad { get; init; } = "";
    public List<ExperienciaLaboral> Experiencia { get; init; } = [];
    public List<EstudioRealizado> Formacion { get; init; } = [];
    public List<string> Cursos { get; init; } = [];
    public List<string> Habilidades { get; init; } = [];

    /// <summary>Perfil de LinkedIn (https://www.linkedin.com/in/...), si lo dio o venía en la hoja de vida.</summary>
    public string LinkedIn { get; init; } = "";
}

public enum CanalPostulacion
{
    Portal,
    LinkedIn,
    Computrabajo,
    Referido,
    Otro,
}

public sealed class RespuestaFiltro
{
    public string Pregunta { get; init; } = "";
    public bool Respuesta { get; init; }
}

/// <summary>Evidencia de la autorización de tratamiento de datos (Ley 1581 de 2012).</summary>
public sealed class AutorizacionDatos
{
    public string Version { get; init; } = "";
    public string Texto { get; init; } = "";
    public DateTimeOffset AceptadaEn { get; init; }
    public string Ip { get; init; } = "";
    public string Navegador { get; init; } = "";

    /// <summary>Evidencia: SHA-256 de versión, texto, fecha, IP, navegador y documento. Si alguien cambia algo, no coincide.</summary>
    public string Huella { get; init; } = "";

    public static string CalcularHuella(string version, string texto, DateTimeOffset aceptadaEn, string ip, string navegador, string documento) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join('\u001f', version, texto, aceptadaEn.UtcTicks - aceptadaEn.UtcTicks % 10, ip, navegador, documento))));
}

public sealed class Postulacion : Entity<Guid>
{
    private Postulacion(Guid id) : base(id)
    {
    }

    public Guid VacanteId { get; private init; }
    public string Numero { get; private init; } = "";
    public CanalPostulacion Canal { get; private init; }
    public List<RespuestaFiltro> Respuestas { get; private set; } = [];
    public AutorizacionDatos Autorizacion { get; private init; } = new();
    public Guid ArchivoHojaDeVidaId { get; private set; }

    /// <summary>Campos que la IA leyó y el candidato corrigió: sirven para medir la calidad del lector.</summary>
    public List<string> CamposCorregidos { get; private set; } = [];
    public DateTimeOffset RecibidaEn { get; private init; }

    /// <summary>Afinidad con la vacante según el agente de ranking (null en postulaciones anteriores al ranking).</summary>
    public AfinidadPostulacion? Afinidad { get; private set; }

    /// <summary>Columna del tablero de la vacante.</summary>
    public EtapaPostulacion Etapa { get; private set; } = EtapaPostulacion.Recibida;
    public string? MotivoDescarte { get; private set; }

    /// <summary>Cada movimiento del tablero, con quién y por qué.</summary>
    public List<CambioEtapa> Historial { get; private set; } = [];

    /// <summary>«Buen candidato» o «No encaja» del reclutador: calibra el ranking de la empresa.</summary>
    public RetroalimentacionPostulacion? Retroalimentacion { get; private set; }

    /// <summary>Una persona aprobó contratarlo: el proceso de vinculación lo lleva desde aquí.</summary>
    public Guid? ProcesoVinculacionId { get; private set; }
    public DateTimeOffset? ContratacionAprobadaEn { get; private set; }
    public string? ContratacionAprobadaPor { get; private set; }

    /// <summary>No pasó un filtro ATS pero una persona decidió seguir con él: ya no se propone descartarlo.</summary>
    public string? RescatadaPor { get; private set; }
    public DateTimeOffset? RescatadaEn { get; private set; }

    internal void Rescatar(string por, DateTimeOffset ahora)
    {
        RescatadaPor = por;
        RescatadaEn = ahora;
    }

    internal void MarcarContratacion(Guid? procesoId, string? por, DateTimeOffset? en)
    {
        ProcesoVinculacionId = procesoId;
        ContratacionAprobadaPor = por;
        ContratacionAprobadaEn = en;
    }

    internal Result Retroalimentar(ValorRetroalimentacion valor, string? nota, string por, DateTimeOffset ahora)
    {
        if (VariablesProtegidas.Menciona(nota))
        {
            return Result.Failure(RetroalimentacionErrors.MotivoDiscriminatorio);
        }

        Retroalimentacion = new RetroalimentacionPostulacion { Valor = valor, Nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(), Por = por, En = ahora };
        return Result.Success();
    }

    internal Result Mover(EtapaPostulacion etapa, string? motivo, string por, DateTimeOffset ahora)
    {
        if (etapa == Etapa)
        {
            return Result.Success();
        }

        if (etapa == EtapaPostulacion.Descartada && string.IsNullOrWhiteSpace(motivo))
        {
            return Result.Failure(EtapaErrors.SinMotivo);
        }

        if (VariablesProtegidas.Menciona(motivo))
        {
            return Result.Failure(RetroalimentacionErrors.MotivoDiscriminatorio);
        }

        Historial.Add(new CambioEtapa { De = Etapa, A = etapa, Por = por, Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(), En = ahora });
        Etapa = etapa;
        MotivoDescarte = etapa == EtapaPostulacion.Descartada ? motivo!.Trim() : null;
        return Result.Success();
    }

    internal void PedirAfinidad() => Afinidad = new AfinidadPostulacion { Estado = EstadoAfinidad.Calculando };

    internal void RegistrarAfinidad(int puntaje, IEnumerable<RazonPuntaje> razones, IEnumerable<string> cumplidos, IEnumerable<string> faltantes,
        string modelo, DateTimeOffset ahora, int razonesRetiradas = 0) =>
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

    internal void RegistrarFallaAfinidad(string mensaje) =>
        Afinidad = new AfinidadPostulacion { Estado = EstadoAfinidad.Fallo, Mensaje = mensaje.Length <= 300 ? mensaje : mensaje[..300] };

    internal Result AjustarAfinidad(int puntaje, string motivo, string por, DateTimeOffset ahora)
    {
        if (Afinidad is not { Estado: EstadoAfinidad.Lista } a)
        {
            return Result.Failure(AfinidadErrors.SinCalcular);
        }

        if (puntaje is < 0 or > 100)
        {
            return Result.Failure(AfinidadErrors.PuntajeInvalido);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result.Failure(AfinidadErrors.SinMotivo);
        }

        if (VariablesProtegidas.Menciona(motivo))
        {
            return Result.Failure(RetroalimentacionErrors.MotivoDiscriminatorio);
        }

        a.PuntajeAjustado = puntaje;
        a.MotivoAjuste = motivo.Trim();
        a.AjustadoPor = por;
        a.AjustadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Al suprimir: se borran las respuestas y el archivo; quedan número, canal, fecha y la autorización como constancia.</summary>
    internal void QuitarDatos()
    {
        Respuestas = [];
        CamposCorregidos = [];
        Afinidad = null;
        Retroalimentacion = null;
        ArchivoHojaDeVidaId = Guid.Empty;
    }

    internal static Postulacion Crear(Guid vacanteId, CanalPostulacion canal, IEnumerable<RespuestaFiltro> respuestas,
        AutorizacionDatos autorizacion, Guid archivoId, IEnumerable<string> camposCorregidos, DateTimeOffset ahora)
    {
        var id = Guid.CreateVersion7();
        return new Postulacion(id)
        {
            VacanteId = vacanteId,
            Numero = $"P-{ahora:yyyy}-{Math.Abs(BitConverter.ToInt32(id.ToByteArray(), 12)) % 1_000_000:D6}",
            Canal = canal,
            Respuestas = [.. respuestas],
            Autorizacion = autorizacion,
            ArchivoHojaDeVidaId = archivoId,
            CamposCorregidos = [.. camposCorregidos],
            RecibidaEn = ahora,
        };
    }
}

public static class CandidatoErrors
{
    public static readonly Error YaPostulado = Error.Conflict("Postulacion.Repetida", "Ya te postulaste a esta vacante. Te avisaremos cómo va.");
    public static readonly Error SinAutorizacion = Error.Validation("Postulacion.SinAutorizacion", "Para enviar tu postulación debes aceptar la autorización de datos.");
}

/// <summary>Una persona que se postula a vacantes de una empresa. Se reconoce por su documento: no se duplica.</summary>
public sealed partial class Candidato : AggregateRoot<Guid>
{
    private readonly List<Postulacion> _postulaciones = [];

    private Candidato(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string Documento { get; private init; } = "";
    public DatosCandidato Datos { get; private set; } = new();

    /// <summary>Cargos, empresas, habilidades, cursos, estudios y ciudad, simplificados: aquí busca el banco de talento.</summary>
    public string TextoBusqueda { get; private set; } = "";
    public DateTimeOffset CreadoEn { get; private init; }
    public DateTimeOffset ActualizadoEn { get; private set; }
    public IReadOnlyList<Postulacion> Postulaciones => _postulaciones.AsReadOnly();

    /// <summary>El titular revocó la autorización o pidió suprimir sus datos y la empresa lo aprobó.</summary>
    public DateTimeOffset? AutorizacionRevocadaEn { get; private set; }

    public bool Suprimido => AutorizacionRevocadaEn is not null;

    public static string NormalizarDocumento(string documento) => new([.. (documento ?? "").Where(char.IsDigit)]);

    /// <summary>«linkedin.com/in/ana-perez/» → «https://www.linkedin.com/in/ana-perez»; vacío si no es un perfil de LinkedIn.</summary>
    public static string NormalizarLinkedIn(string? perfil)
    {
        var m = FormatoLinkedIn().Match((perfil ?? "").Trim());
        return m.Success ? $"https://www.linkedin.com/in/{m.Groups[1].Value}" : "";
    }

    public static bool EsLinkedInValido(string? perfil) => string.IsNullOrWhiteSpace(perfil) || NormalizarLinkedIn(perfil).Length > 0;

    [GeneratedRegex(@"^(?:https?://)?(?:[a-z]{2,3}\.)?linkedin\.com/in/([A-Za-z0-9\-_%]{3,100})/?(?:\?.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex FormatoLinkedIn();

    public static Candidato Registrar(Guid empresaId, DatosCandidato datos, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return new Candidato(Guid.CreateVersion7())
        {
            EmpresaId = empresaId,
            Documento = NormalizarDocumento(datos.Documento),
            Datos = datos,
            TextoBusqueda = Normalizacion.TextoBusqueda(datos),
            CreadoEn = ahora,
            ActualizadoEn = ahora,
        };
    }

    /// <summary>La hoja de vida más reciente manda: se actualizan sus datos en cada postulación.</summary>
    public void ActualizarDatos(DatosCandidato datos, DateTimeOffset ahora)
    {
        Datos = datos;
        TextoBusqueda = Normalizacion.TextoBusqueda(datos);
        ActualizadoEn = ahora;
    }

    /// <summary>Recalcula el texto de búsqueda (candidatos anteriores al banco de talento o un catálogo de sinónimos nuevo).</summary>
    public void RecalcularBusqueda() => TextoBusqueda = Suprimido ? "" : Normalizacion.TextoBusqueda(Datos);

    /// <summary>
    /// Elimina los datos personales y deja solo la constancia: el documento (para no volver a contactarlo sin una nueva
    /// autorización), las fechas y la versión que había aceptado. Devuelve las hojas de vida que hay que borrar.
    /// </summary>
    public IReadOnlyList<Guid> Suprimir(DateTimeOffset ahora)
    {
        var archivos = _postulaciones.Select(p => p.ArchivoHojaDeVidaId).Where(id => id != Guid.Empty).Distinct().ToList();
        Datos = new DatosCandidato { Nombre = "Titular con datos suprimidos", Documento = Documento };
        TextoBusqueda = "";
        AutorizacionRevocadaEn ??= ahora;
        ActualizadoEn = ahora;
        foreach (var p in _postulaciones)
        {
            p.QuitarDatos();
        }

        return archivos;
    }

    /// <summary>La postulación queda esperando al agente de ranking.</summary>
    public Result<Postulacion> PedirAfinidad(Guid postulacionId)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null)
        {
            return AfinidadErrors.NoExiste;
        }

        p.PedirAfinidad();
        return p;
    }

    public Result<Postulacion> RegistrarAfinidad(Guid postulacionId, int puntaje, IEnumerable<RazonPuntaje> razones, IEnumerable<string> cumplidos,
        IEnumerable<string> faltantes, string modelo, DateTimeOffset ahora, int razonesRetiradas = 0)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null)
        {
            return AfinidadErrors.NoExiste;
        }

        p.RegistrarAfinidad(puntaje, razones, cumplidos, faltantes, modelo, ahora, razonesRetiradas);
        return p;
    }

    public Result<Postulacion> RegistrarFallaAfinidad(Guid postulacionId, string mensaje)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null)
        {
            return AfinidadErrors.NoExiste;
        }

        p.RegistrarFallaAfinidad(mensaje);
        return p;
    }

    /// <summary>El reclutador dice si el candidato le sirve para la vacante; la nota no puede basarse en variables protegidas.</summary>
    public Result<Postulacion> Retroalimentar(Guid postulacionId, ValorRetroalimentacion valor, string? nota, string por, DateTimeOffset ahora)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null)
        {
            return AfinidadErrors.NoExiste;
        }

        if (Suprimido)
        {
            return EtapaErrors.Suprimido;
        }

        var r = p.Retroalimentar(valor, nota, por, ahora);
        return r.IsSuccess ? p : r.Error;
    }

    /// <summary>Una persona rescata la postulación de un descarte sugerido por los filtros ATS.</summary>
    public bool RescatarDeFiltros(Guid postulacionId, string por, DateTimeOffset ahora)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null || Suprimido || p.Etapa == EtapaPostulacion.Descartada)
        {
            return false;
        }

        p.Rescatar(por, ahora);
        return true;
    }

    /// <summary>Vinculación abrió (o canceló, con null) el proceso de contratación de la postulación.</summary>
    public bool MarcarContratacion(Guid postulacionId, Guid? procesoId, string? por, DateTimeOffset? en)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null || (procesoId is null && p.ProcesoVinculacionId is null))
        {
            return false;
        }

        p.MarcarContratacion(procesoId, por, en);
        return true;
    }

    /// <summary>Una persona mueve la postulación en el tablero; descartar exige motivo (afecta a la persona).</summary>
    public Result<Postulacion> MoverPostulacion(Guid postulacionId, EtapaPostulacion etapa, string? motivo, string por, DateTimeOffset ahora)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null)
        {
            return AfinidadErrors.NoExiste;
        }

        if (Suprimido)
        {
            return EtapaErrors.Suprimido;
        }

        var r = p.Mover(etapa, motivo, por, ahora);
        return r.IsSuccess ? p : r.Error;
    }

    /// <summary>Una persona corrige el puntaje de la IA, con su motivo: su puntaje manda y la sugerencia original se conserva.</summary>
    public Result<Postulacion> AjustarAfinidad(Guid postulacionId, int puntaje, string motivo, string por, DateTimeOffset ahora)
    {
        var p = _postulaciones.FirstOrDefault(x => x.Id == postulacionId);
        if (p is null)
        {
            return AfinidadErrors.NoExiste;
        }

        var r = p.AjustarAfinidad(puntaje, motivo, por, ahora);
        return r.IsSuccess ? p : r.Error;
    }

    public Result<Postulacion> Postularse(Guid vacanteId, CanalPostulacion canal, IEnumerable<RespuestaFiltro> respuestas,
        AutorizacionDatos? autorizacion, Guid archivoId, IEnumerable<string> camposCorregidos, DateTimeOffset ahora)
    {
        if (autorizacion is null || string.IsNullOrWhiteSpace(autorizacion.Version))
        {
            return CandidatoErrors.SinAutorizacion;
        }

        if (Suprimido)
        {
            // Volver a postularse es una nueva autorización: el titular decide de nuevo
            AutorizacionRevocadaEn = null;
        }

        if (_postulaciones.Any(p => p.VacanteId == vacanteId))
        {
            return CandidatoErrors.YaPostulado;
        }

        var postulacion = Postulacion.Crear(vacanteId, canal, respuestas, autorizacion, archivoId, camposCorregidos, ahora);
        _postulaciones.Add(postulacion);
        return postulacion;
    }
}
