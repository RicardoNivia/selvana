using System.Security.Cryptography;
using System.Text;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Vinculacion.Domain.Procesos;

/// <summary>Pasos del proceso: oferta → documentos → afiliaciones → onboarding → vinculado.</summary>
public enum EtapaVinculacion
{
    Oferta,
    Documentos,
    Afiliaciones,
    Onboarding,
    Vinculado,
    Cancelado,
}

public enum EstadoOferta
{
    /// <summary>La IA está redactando la carta oferta y el contrato.</summary>
    Redactando,
    PorAprobar,
    FallaIa,

    /// <summary>RRHH la aprobó y el candidato tiene su enlace.</summary>
    Enviada,
    Aceptada,
    Rechazada,
}

/// <summary>Quién se vincula: copia de lo que dio en su postulación.</summary>
public sealed class PersonaVinculada
{
    public string Nombre { get; init; } = "";
    public string Documento { get; init; } = "";
    public string Correo { get; init; } = "";
    public string Celular { get; init; } = "";
    public string Ciudad { get; init; } = "";
}

/// <summary>La vacante de la que viene, tal como estaba al aprobar la contratación.</summary>
public sealed class VacanteOrigen
{
    public Guid VacanteId { get; init; }
    public Guid PostulacionId { get; init; }
    public Guid CandidatoId { get; init; }
    public string Codigo { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Cargo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public List<string> Requisitos { get; init; } = [];
    public decimal SalarioMinimo { get; init; }
    public decimal SalarioMaximo { get; init; }
}

public sealed class CondicionesContratacion
{
    public TipoContrato Tipo { get; init; }
    public decimal SalarioCop { get; init; }
    public DateOnly FechaIngreso { get; init; }
    public DateOnly? FechaFin { get; init; }
    public int PeriodoPruebaDias { get; init; }

    /// <summary>Obra o labor contratada (obligatoria en obra o labor y en misión temporal).</summary>
    public string? Labor { get; init; }
    public string Jornada { get; init; } = "";
    public string Ciudad { get; init; } = "";
}

public sealed class Clausula
{
    public string Titulo { get; init; } = "";
    public string Texto { get; init; } = "";
}

public sealed class RazonOferta
{
    public string Signo { get; init; } = "+";
    public string Texto { get; init; } = "";
    public string Fuente { get; init; } = "";
}

/// <summary>Evidencia de la respuesta del candidato: quién, cuándo, desde dónde y la huella del texto que vio.</summary>
public sealed class RespuestaOferta
{
    public bool Acepta { get; init; }
    public string NombreEscrito { get; init; } = "";
    public string? Motivo { get; init; }
    public DateTimeOffset En { get; init; }
    public string Ip { get; init; } = "";
    public string Navegador { get; init; } = "";

    /// <summary>SHA-256 del texto de la oferta, la decisión, la fecha, la IP, el navegador y el documento.</summary>
    public string Huella { get; init; } = "";
}

public sealed class OfertaLaboral
{
    public EstadoOferta Estado { get; set; }
    public string Carta { get; set; } = "";
    public List<Clausula> Clausulas { get; set; } = [];
    public List<RazonOferta> Razones { get; set; } = [];

    /// <summary>Lo que la IA pide revisar (se suma a las alertas por reglas).</summary>
    public List<string> AlertasIa { get; set; } = [];
    public string? Modelo { get; set; }
    public decimal CostoUsd { get; set; }
    public string? ErrorIa { get; set; }
    public DateTimeOffset? RedactadaEn { get; set; }

    /// <summary>Una persona editó el texto: deja de ser sugerencia de la IA.</summary>
    public string? EditadaPor { get; set; }
    public DateTimeOffset? EditadaEn { get; set; }
    public string? AprobadaPor { get; set; }
    public DateTimeOffset? AprobadaEn { get; set; }

    /// <summary>SHA-256 del enlace del candidato: el enlace en claro solo lo tiene él.</summary>
    public string? TokenHash { get; set; }
    public DateTimeOffset? EnviadaEn { get; set; }
    public RespuestaOferta? Respuesta { get; set; }
}

public static class ProcesoErrors
{
    public static readonly Error NoExiste = Error.NotFound("Vinculacion.NoExiste", "Ese proceso de vinculación no existe.");
    public static readonly Error EnlaceNoExiste = Error.NotFound("Vinculacion.EnlaceNoExiste", "Este enlace no existe o ya no está activo. Pídele uno nuevo a la empresa.");
    public static readonly Error NoEstaEnTerna = Error.Conflict("Vinculacion.NoEstaEnTerna", "Solo se puede contratar a un candidato de la terna aprobada.");
    public static readonly Error YaEnProceso = Error.Conflict("Vinculacion.YaEnProceso", "Este candidato ya tiene un proceso de vinculación abierto.");
    public static readonly Error Terminado = Error.Conflict("Vinculacion.Terminado", "El proceso ya terminó o se canceló.");
    public static readonly Error NoEditable = Error.Conflict("Vinculacion.NoEditable", "La oferta ya no se puede cambiar: el candidato ya respondió.");
    public static readonly Error NoPorAprobar = Error.Conflict("Vinculacion.NoPorAprobar", "La oferta no está lista para aprobar: espere el borrador de la IA.");
    public static readonly Error Redactando = Error.Conflict("Vinculacion.Redactando", "La IA está redactando la oferta: espere un momento.");
    public static readonly Error CondicionesBloquean = Error.Validation("Vinculacion.CondicionesBloquean", "Corrija las alertas en rojo de las condiciones antes de aprobar la oferta.");
    public static readonly Error NoEnviada = Error.Conflict("Vinculacion.NoEnviada", "La oferta no está esperando respuesta.");
    public static readonly Error DocumentoNoCoincide = Error.Validation("Vinculacion.DocumentoNoCoincide", "El número de documento no coincide con el de tu postulación. Revísalo.");
    public static readonly Error SinNombre = Error.Validation("Vinculacion.SinNombre", "Escribe tu nombre completo para aceptar.");
    public static readonly Error OfertaVacia = Error.Validation("Vinculacion.OfertaVacia", "La carta y el contrato no pueden quedar vacíos.");
}

/// <summary>
/// Proceso de vinculación de una persona: nace cuando alguien aprueba contratar al candidato de la terna y termina con el empleado creado.
/// La IA redacta la oferta; RRHH la aprueba; el candidato la acepta por su enlace con evidencia.
/// </summary>
public sealed class ProcesoVinculacion : AggregateRoot<Guid>
{
    public const string MarcaTrabajador = "[TRABAJADOR]";
    public const string MarcaDocumento = "[DOCUMENTO]";

    private ProcesoVinculacion(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string Empresa { get; private init; } = "";
    public Guid? EmpresaUsuariaId { get; private init; }
    public string? EmpresaUsuaria { get; private init; }
    public string Codigo { get; private init; } = "";
    public PersonaVinculada Persona { get; private init; } = new();
    public VacanteOrigen Vacante { get; private init; } = new();
    public CondicionesContratacion Condiciones { get; private set; } = new();
    public OfertaLaboral Oferta { get; private set; } = new();

    /// <summary>Documentos de ingreso: se piden al aceptar la oferta, según el tipo de contrato.</summary>
    public List<DocumentoIngreso> Documentos { get; private set; } = [];

    /// <summary>EPS, pensión, ARL y caja: se preparan al aprobar los documentos, prellenadas con lo que leyó la IA.</summary>
    public List<Afiliacion> Afiliaciones { get; private set; } = [];
    public DatosAlta Alta { get; private set; } = new();
    public string? EmpleadoCreadoPor { get; private set; }
    public DateTimeOffset? EmpleadoCreadoEn { get; private set; }
    public EtapaVinculacion Etapa { get; private set; }
    public string? MotivoCancelacion { get; private set; }
    public string AprobadaPor { get; private init; } = "";
    public DateTimeOffset CreadoEn { get; private init; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    public bool Abierto => Etapa is not (EtapaVinculacion.Vinculado or EtapaVinculacion.Cancelado);

    /// <summary>Una persona aprobó la contratación: el proceso arranca con la IA redactando la oferta.</summary>
    public static ProcesoVinculacion Abrir(Guid empresaId, string empresa, Guid? empresaUsuariaId, string? empresaUsuaria, PersonaVinculada persona,
        VacanteOrigen vacante, CondicionesContratacion condiciones, string por, DateTimeOffset ahora)
    {
        var id = Guid.CreateVersion7();
        return new ProcesoVinculacion(id)
        {
            EmpresaId = empresaId,
            Empresa = empresa,
            EmpresaUsuariaId = empresaUsuariaId,
            EmpresaUsuaria = empresaUsuaria,
            Codigo = $"VIN-{ahora:yyMMdd}-{id.ToString("N")[^4..].ToUpperInvariant()}",
            Persona = persona,
            Vacante = vacante,
            Condiciones = condiciones,
            Oferta = new OfertaLaboral { Estado = EstadoOferta.Redactando },
            Etapa = EtapaVinculacion.Oferta,
            AprobadaPor = por,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
        };
    }

    /// <summary>RRHH cambia las condiciones: la oferta se vuelve a redactar y el enlace anterior deja de servir.</summary>
    public Result CambiarCondiciones(CondicionesContratacion condiciones, DateTimeOffset ahora)
    {
        if (!Abierto || Etapa != EtapaVinculacion.Oferta)
        {
            return Result.Failure(ProcesoErrors.Terminado);
        }

        if (Oferta.Estado == EstadoOferta.Aceptada)
        {
            return Result.Failure(ProcesoErrors.NoEditable);
        }

        Condiciones = condiciones;
        Oferta = new OfertaLaboral { Estado = EstadoOferta.Redactando };
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Pedir de nuevo el borrador a la IA (porque falló o porque RRHH quiere otra versión).</summary>
    public Result PedirRedaccion(DateTimeOffset ahora)
    {
        if (!Abierto || Etapa != EtapaVinculacion.Oferta)
        {
            return Result.Failure(ProcesoErrors.Terminado);
        }

        if (Oferta.Estado is not (EstadoOferta.PorAprobar or EstadoOferta.FallaIa))
        {
            return Result.Failure(Oferta.Estado == EstadoOferta.Redactando ? ProcesoErrors.Redactando : ProcesoErrors.NoEditable);
        }

        Oferta = new OfertaLaboral { Estado = EstadoOferta.Redactando };
        ActualizadoEn = ahora;
        return Result.Success();
    }

    public bool RegistrarBorrador(string carta, IEnumerable<Clausula> clausulas, IEnumerable<RazonOferta> razones, IEnumerable<string> alertas, string modelo,
        decimal costoUsd, DateTimeOffset ahora)
    {
        if (Oferta.Estado != EstadoOferta.Redactando)
        {
            return false;
        }

        Oferta = new OfertaLaboral
        {
            Estado = EstadoOferta.PorAprobar,
            Carta = carta.Trim(),
            Clausulas = [.. clausulas],
            Razones = [.. razones.Take(5)],
            AlertasIa = [.. alertas],
            Modelo = modelo,
            CostoUsd = costoUsd,
            RedactadaEn = ahora,
        };
        ActualizadoEn = ahora;
        return true;
    }

    public bool FallaRedaccion(string error, DateTimeOffset ahora)
    {
        if (Oferta.Estado != EstadoOferta.Redactando)
        {
            return false;
        }

        Oferta.Estado = EstadoOferta.FallaIa;
        Oferta.ErrorIa = error;
        ActualizadoEn = ahora;
        return true;
    }

    /// <summary>Una persona corrige el texto: queda como suyo (pierde el violeta de la IA).</summary>
    public Result EditarOferta(string carta, IReadOnlyList<Clausula> clausulas, string por, DateTimeOffset ahora)
    {
        if (Oferta.Estado != EstadoOferta.PorAprobar)
        {
            return Result.Failure(Oferta.Estado == EstadoOferta.Redactando ? ProcesoErrors.Redactando : ProcesoErrors.NoEditable);
        }

        if (string.IsNullOrWhiteSpace(carta) || clausulas.Count == 0 || clausulas.Any(c => string.IsNullOrWhiteSpace(c.Texto)))
        {
            return Result.Failure(ProcesoErrors.OfertaVacia);
        }

        Oferta.Carta = carta.Trim();
        Oferta.Clausulas = [.. clausulas.Select(c => new Clausula { Titulo = c.Titulo.Trim(), Texto = c.Texto.Trim() })];
        Oferta.EditadaPor = por;
        Oferta.EditadaEn = ahora;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>RRHH aprueba la oferta (decisión que afecta a una persona) y el candidato recibe su enlace.</summary>
    public Result AprobarOferta(IReadOnlyList<AlertaContrato> alertas, string tokenHash, string por, DateTimeOffset ahora)
    {
        if (!Abierto || Etapa != EtapaVinculacion.Oferta)
        {
            return Result.Failure(ProcesoErrors.Terminado);
        }

        if (Oferta.Estado != EstadoOferta.PorAprobar)
        {
            return Result.Failure(ProcesoErrors.NoPorAprobar);
        }

        if (ReglasContrato.Bloquea(alertas))
        {
            return Result.Failure(ProcesoErrors.CondicionesBloquean);
        }

        Oferta.Estado = EstadoOferta.Enviada;
        Oferta.AprobadaPor = por;
        Oferta.AprobadaEn = ahora;
        Oferta.TokenHash = tokenHash;
        Oferta.EnviadaEn = ahora;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Un enlace nuevo invalida el anterior (por ejemplo, si el candidato lo perdió).</summary>
    public Result RenovarEnlace(string tokenHash, DateTimeOffset ahora)
    {
        if (Oferta.Estado != EstadoOferta.Enviada)
        {
            return Result.Failure(ProcesoErrors.NoEnviada);
        }

        Oferta.TokenHash = tokenHash;
        Oferta.EnviadaEn = ahora;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Qué le toca hacer al candidato: aceptar y firmar la oferta, o subir sus documentos de ingreso; null si nada.</summary>
    public string? PendienteDelCandidato => !Abierto ? null
        : Oferta.Estado == EstadoOferta.Enviada ? "Revisar y aceptar la oferta, y firmar el contrato"
        : Etapa == EtapaVinculacion.Documentos && Oferta.Estado == EstadoOferta.Aceptada && !CandidatoCompleto ? "Subir tus documentos de ingreso"
        : null;

    /// <summary>
    /// La persona abre su pendiente desde el portal (ADR 0013): un enlace nuevo que invalida el anterior,
    /// solo si tiene algo por hacer.
    /// </summary>
    public Result AbrirDesdePortal(string tokenHash, DateTimeOffset ahora)
    {
        if (PendienteDelCandidato is null)
        {
            return Result.Failure(ProcesoErrors.NoEnviada);
        }

        Oferta.TokenHash = tokenHash;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Carta y contrato con el nombre y el documento del trabajador en lugar de las marcas.</summary>
    public string Personalizar(string texto) =>
        (texto ?? "").Replace(MarcaTrabajador, Persona.Nombre, StringComparison.Ordinal).Replace(MarcaDocumento, Persona.Documento, StringComparison.Ordinal);

    /// <summary>El texto exacto que ve el candidato: sobre él se calcula la huella de la aceptación.</summary>
    public string TextoOferta() =>
        string.Join("\n\n", [Personalizar(Oferta.Carta), .. Oferta.Clausulas.Select(c => $"{c.Titulo}\n{Personalizar(c.Texto)}")]);

    public static string SoloDigitos(string? documento) => new([.. (documento ?? "").Where(char.IsAsciiDigit)]);

    /// <summary>
    /// El candidato responde por su enlace. Para aceptar escribe su nombre y su documento (debe coincidir con el de la postulación);
    /// queda la huella SHA-256 del texto que vio, la fecha, la IP y el navegador.
    /// </summary>
    public Result ResponderOferta(bool acepta, string? nombreEscrito, string? documentoEscrito, string? motivo, string ip, string navegador, DateTimeOffset ahora)
    {
        if (Oferta.Estado != EstadoOferta.Enviada || !Abierto)
        {
            return Result.Failure(ProcesoErrors.NoEnviada);
        }

        if (acepta && string.IsNullOrWhiteSpace(nombreEscrito))
        {
            return Result.Failure(ProcesoErrors.SinNombre);
        }

        if (acepta && SoloDigitos(documentoEscrito) != SoloDigitos(Persona.Documento))
        {
            return Result.Failure(ProcesoErrors.DocumentoNoCoincide);
        }

        Oferta.Respuesta = new RespuestaOferta
        {
            Acepta = acepta,
            NombreEscrito = (nombreEscrito ?? "").Trim(),
            Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            En = ahora,
            Ip = ip,
            Navegador = navegador.Length > 300 ? navegador[..300] : navegador,
            Huella = CalcularHuella(TextoOferta(), acepta, ahora, ip, navegador, Persona.Documento),
        };
        Oferta.Estado = acepta ? EstadoOferta.Aceptada : EstadoOferta.Rechazada;
        if (acepta)
        {
            Etapa = EtapaVinculacion.Documentos;
            Documentos = RequisitosDocumentales.Para(Condiciones.Tipo);
        }

        ActualizadoEn = ahora;
        return Result.Success();
    }

    public static string CalcularHuella(string texto, bool acepta, DateTimeOffset en, string ip, string navegador, string documento) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\u001f', texto, acepta ? "acepta" : "rechaza", en.UtcTicks - en.UtcTicks % 10, ip, navegador.Length > 300 ? navegador[..300] : navegador, SoloDigitos(documento)))));

    /// <summary>¿La evidencia guardada coincide con el texto actual? Si alguien cambió algo, no.</summary>
    public bool EvidenciaIntegra() => Oferta.Respuesta is not { } r ||
        r.Huella == CalcularHuella(TextoOferta(), r.Acepta, r.En, r.Ip, r.Navegador, Persona.Documento);

    /// <summary>Procesos que aceptaron antes de existir la lista de documentos: se arma una vez.</summary>
    public bool AsegurarDocumentos()
    {
        if (Etapa == EtapaVinculacion.Cancelado || Oferta.Estado != EstadoOferta.Aceptada || Documentos.Count > 0)
        {
            return false;
        }

        Documentos = RequisitosDocumentales.Para(Condiciones.Tipo);
        return true;
    }

    public DocumentoIngreso? Documento(TipoDocumentoIngreso tipo) => Documentos.FirstOrDefault(d => d.Tipo == tipo);

    /// <summary>
    /// Llega un archivo. Si es del candidato (o RRHH lo sube por él), la IA lo lee; los de la empresa se registran con su resultado.
    /// Un archivo nuevo reemplaza al anterior y vuelve a revisión.
    /// </summary>
    public Result<DocumentoIngreso> SubirDocumento(TipoDocumentoIngreso tipo, Guid archivoId, string nombreArchivo, bool desdeEnlace, string por, DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Documentos)
        {
            return DocumentosErrors.NoEnDocumentos;
        }

        if (Documento(tipo) is not { } d)
        {
            return DocumentosErrors.NoExiste;
        }

        if (d.LoCargaEmpresa)
        {
            return DocumentosErrors.LoCargaEmpresa;
        }

        if (d.Estado == EstadoDocumento.Aprobado)
        {
            return DocumentosErrors.YaAprobado;
        }

        d.ArchivoId = archivoId;
        d.NombreArchivo = nombreArchivo;
        d.SubidoPor = desdeEnlace ? Persona.Nombre : por;
        d.SubidoEn = ahora;
        d.Versiones++;
        d.Estado = EstadoDocumento.Leyendo;
        d.Lectura = null;
        d.MotivoRechazo = null;
        ActualizadoEn = ahora;
        return d;
    }

    /// <summary>RRHH carga antecedentes o el examen médico con su resultado: queda aprobado por quien lo cargó.</summary>
    public Result RegistrarSoporteEmpresa(TipoDocumentoIngreso tipo, Guid archivoId, string nombreArchivo, ResultadoAntecedentes? antecedentes, string? conceptoCifrado,
        bool? conceptoRequiereRevision, string por, DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Documentos)
        {
            return Result.Failure(DocumentosErrors.NoEnDocumentos);
        }

        if (Documento(tipo) is not { } d)
        {
            return Result.Failure(DocumentosErrors.NoExiste);
        }

        if (!d.LoCargaEmpresa)
        {
            return Result.Failure(DocumentosErrors.LoCargaCandidato);
        }

        if ((tipo == TipoDocumentoIngreso.Antecedentes && antecedentes is null) || (tipo == TipoDocumentoIngreso.ExamenMedico && conceptoCifrado is null))
        {
            return Result.Failure(DocumentosErrors.SinResultado);
        }

        d.ArchivoId = archivoId;
        d.NombreArchivo = nombreArchivo;
        d.SubidoPor = por;
        d.SubidoEn = ahora;
        d.Versiones++;
        d.Antecedentes = antecedentes;
        d.ConceptoCifrado = conceptoCifrado;
        d.ConceptoRequiereRevision = conceptoRequiereRevision;
        d.Estado = EstadoDocumento.Aprobado;
        d.RevisadoPor = por;
        d.RevisadoEn = ahora;
        ActualizadoEn = ahora;
        RevisarAvance();
        return Result.Success();
    }

    /// <summary>Llega la lectura de la IA; si mientras tanto subieron otro archivo, se ignora.</summary>
    public bool RegistrarLectura(TipoDocumentoIngreso tipo, Guid archivoId, LecturaDocumento lectura, DateTimeOffset ahora)
    {
        if (Documento(tipo) is not { Estado: EstadoDocumento.Leyendo } d || d.ArchivoId != archivoId)
        {
            return false;
        }

        d.Lectura = lectura;
        d.Estado = EstadoDocumento.PorRevisar;
        ActualizadoEn = ahora;
        return true;
    }

    /// <summary>Se puede aprobar en bloque desde la bandeja: legible, del tipo pedido y del titular.</summary>
    public IReadOnlyList<DocumentoIngreso> SinObservaciones =>
        [.. Documentos.Where(d => d.Estado == EstadoDocumento.PorRevisar && d.Lectura is { Legible: true, TipoCorrecto: true, CoincideTitular: true })];

    /// <summary>Todos los del candidato que son obligatorios ya tienen archivo leído o aprobado.</summary>
    public bool CandidatoCompleto => Documentos.Where(d => d.Obligatorio && !d.LoCargaEmpresa)
        .All(d => d.Estado is EstadoDocumento.PorRevisar or EstadoDocumento.Aprobado);

    public Result AprobarDocumento(TipoDocumentoIngreso tipo, string por, DateTimeOffset ahora)
    {
        var r = Revisable(tipo);
        if (r.IsFailure)
        {
            return r;
        }

        var d = r.Value;
        d.Estado = EstadoDocumento.Aprobado;
        d.RevisadoPor = por;
        d.RevisadoEn = ahora;
        d.MotivoRechazo = null;
        ActualizadoEn = ahora;
        RevisarAvance();
        return Result.Success();
    }

    /// <summary>El candidato ve el motivo en su enlace y puede subir otro archivo.</summary>
    public Result RechazarDocumento(TipoDocumentoIngreso tipo, string motivo, string por, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result.Failure(DocumentosErrors.SinMotivo);
        }

        var r = Revisable(tipo);
        if (r.IsFailure)
        {
            return r;
        }

        var d = r.Value;
        d.Estado = EstadoDocumento.Rechazado;
        d.RevisadoPor = por;
        d.RevisadoEn = ahora;
        d.MotivoRechazo = motivo.Trim();
        ActualizadoEn = ahora;
        return Result.Success();
    }

    private Result<DocumentoIngreso> Revisable(TipoDocumentoIngreso tipo)
    {
        if (Etapa != EtapaVinculacion.Documentos)
        {
            return DocumentosErrors.NoEnDocumentos;
        }

        return Documento(tipo) switch
        {
            null => DocumentosErrors.NoExiste,
            { LoCargaEmpresa: true } => DocumentosErrors.LoCargaCandidato,
            { Estado: EstadoDocumento.Leyendo } => DocumentosErrors.Leyendo,
            { Estado: EstadoDocumento.Aprobado } => DocumentosErrors.YaAprobado,
            { ArchivoId: null } => DocumentosErrors.SinArchivo,
            var d => d,
        };
    }

    /// <summary>Con todos los obligatorios aprobados, el proceso pasa a afiliaciones.</summary>
    private void RevisarAvance()
    {
        if (Etapa == EtapaVinculacion.Documentos && Documentos.Count > 0 && Documentos.Where(d => d.Obligatorio).All(d => d.Estado == EstadoDocumento.Aprobado))
        {
            Etapa = EtapaVinculacion.Afiliaciones;
            AsegurarAfiliaciones();
        }
    }

    /// <summary>Un dato que leyó la IA de un documento aprobado, por palabras del nombre del campo («número de cuenta»).</summary>
    private string? Leido(TipoDocumentoIngreso tipo, params string[] palabras) =>
        Documento(tipo)?.Lectura?.Campos.FirstOrDefault(c => palabras.All(p => c.Campo.Contains(p, StringComparison.OrdinalIgnoreCase)))?.Valor?.Trim() is { Length: > 0 } v ? v : null;

    /// <summary>Arma las afiliaciones y los datos del alta una sola vez, con lo que leyó la IA (queda marcado como sugerido).</summary>
    public bool AsegurarAfiliaciones()
    {
        if (Etapa != EtapaVinculacion.Afiliaciones || Afiliaciones.Count > 0)
        {
            return false;
        }

        Afiliacion A(TipoAfiliacion t, TipoDocumentoIngreso? fuente)
        {
            var entidad = fuente is { } f ? Leido(f, "entidad") ?? Leido(f, "eps") ?? Leido(f, "fondo") : null;
            return new Afiliacion { Tipo = t, Entidad = entidad ?? "", SugeridaDe = entidad is null ? null : RequisitosDocumentales.Nombre(fuente!.Value) };
        }

        var (clase, _) = ReglasAlta.ClaseSugerida(Vacante.Cargo);
        Afiliaciones =
        [
            A(TipoAfiliacion.Eps, TipoDocumentoIngreso.CertificadoEps),
            A(TipoAfiliacion.Pension, TipoDocumentoIngreso.CertificadoPension),
            new Afiliacion { Tipo = TipoAfiliacion.Arl, ClaseRiesgo = clase, SugeridaDe = "Reglas por cargo" },
            new Afiliacion { Tipo = TipoAfiliacion.CajaCompensacion },
        ];

        var sugeridos = new List<string>();
        string Tomar(string nombre, TipoDocumentoIngreso tipo, params string[] palabras)
        {
            var v = Leido(tipo, palabras);
            if (v is not null)
            {
                sugeridos.Add(nombre);
            }

            return v ?? "";
        }

        Alta = new DatosAlta
        {
            Sede = Condiciones.Ciudad,
            Banco = Tomar("banco", TipoDocumentoIngreso.CertificadoBancario, "banco"),
            TipoCuenta = Tomar("tipo de cuenta", TipoDocumentoIngreso.CertificadoBancario, "tipo"),
            NumeroCuenta = Tomar("número de cuenta", TipoDocumentoIngreso.CertificadoBancario, "cuenta", "mero"),
            SugeridosPorIa = sugeridos,
        };
        return true;
    }

    public Result ActualizarAfiliacion(TipoAfiliacion tipo, EstadoAfiliacion estado, string? entidad, string? radicado, DateOnly? fechaEfectiva, int? claseRiesgo,
        string por, DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Afiliaciones)
        {
            return Result.Failure(AltaErrors.NoEnAfiliaciones);
        }

        if (Afiliaciones.FirstOrDefault(a => a.Tipo == tipo) is not { } a)
        {
            return Result.Failure(AltaErrors.NoEnAfiliaciones);
        }

        if (estado == EstadoAfiliacion.NoAplica && (tipo != TipoAfiliacion.Pension || Condiciones.Tipo != TipoContrato.Aprendizaje))
        {
            return Result.Failure(AltaErrors.NoAplicaSoloPension);
        }

        if (estado is EstadoAfiliacion.Solicitada or EstadoAfiliacion.Confirmada && string.IsNullOrWhiteSpace(entidad))
        {
            return Result.Failure(AltaErrors.SinEntidad);
        }

        if (tipo == TipoAfiliacion.Arl && claseRiesgo is not (null or >= 1 and <= 5))
        {
            return Result.Failure(AltaErrors.ClaseRiesgo);
        }

        a.Estado = estado;
        a.Entidad = (entidad ?? "").Trim();
        a.SugeridaDe = null;
        a.Radicado = string.IsNullOrWhiteSpace(radicado) ? null : radicado.Trim();
        a.FechaEfectiva = fechaEfectiva;
        a.ClaseRiesgo = tipo == TipoAfiliacion.Arl ? claseRiesgo ?? a.ClaseRiesgo : null;
        a.Por = por;
        a.En = ahora;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Una persona revisa los datos del alta: lo que sugirió la IA queda confirmado.</summary>
    public Result EditarAlta(string area, string centroCosto, string sede, string banco, string tipoCuenta, string numeroCuenta, string por, DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Afiliaciones)
        {
            return Result.Failure(AltaErrors.NoEnAfiliaciones);
        }

        Alta = new DatosAlta
        {
            Area = area.Trim(), CentroCosto = centroCosto.Trim(), Sede = sede.Trim(), Banco = banco.Trim(), TipoCuenta = tipoCuenta.Trim(),
            NumeroCuenta = numeroCuenta.Trim(), EditadoPor = por,
        };
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Todo confirmado y sin alertas en rojo: lo único que queda es la decisión de crear el empleado.</summary>
    public bool ListoParaCrearEmpleado(DateOnly hoy) => Etapa == EtapaVinculacion.Afiliaciones && Afiliaciones.Count > 0 && !ReglasAlta.Revisar(this, hoy).Any(a => a.Nivel == NivelAlerta.Bloquea);

    /// <summary>Decisión humana: crear el empleado. El proceso pasa a onboarding y el empleado nace en nómina y en Empleados.</summary>
    public Result CrearEmpleado(bool confirmaAlertas, string por, DateTimeOffset ahora)
    {
        if (Etapa != EtapaVinculacion.Afiliaciones)
        {
            return Result.Failure(AltaErrors.NoEnAfiliaciones);
        }

        if (!ListoParaCrearEmpleado(DateOnly.FromDateTime(ahora.UtcDateTime)))
        {
            return Result.Failure(AltaErrors.Incompleto);
        }

        if (ReglasAlta.RequiereConfirmarAlertas(this) && !confirmaAlertas)
        {
            return Result.Failure(AltaErrors.SinConfirmarAlertas);
        }

        Etapa = EtapaVinculacion.Onboarding;
        EmpleadoCreadoPor = por;
        EmpleadoCreadoEn = ahora;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    public Result Cancelar(string motivo, DateTimeOffset ahora)
    {
        if (!Abierto)
        {
            return Result.Failure(ProcesoErrors.Terminado);
        }

        Etapa = EtapaVinculacion.Cancelado;
        MotivoCancelacion = motivo.Trim();
        Oferta.TokenHash = null;
        ActualizadoEn = ahora;
        return Result.Success();
    }
}
