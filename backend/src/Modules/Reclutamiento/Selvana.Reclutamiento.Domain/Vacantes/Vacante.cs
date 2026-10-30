using Selvana.Reclutamiento.Domain.Pruebas;
using Selvana.Reclutamiento.Domain.Seleccion;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Vacantes;

public enum EstadoVacante
{
    /// <summary>El agente redactor está preparando el borrador.</summary>
    Redactando,
    Borrador,

    /// <summary>La requisición espera la aprobación de RRHH o de gerencia.</summary>
    EnAprobacion,
    Publicada,
    Cerrada,
}

/// <summary>Pregunta de la IA antes de redactar la vacante, con respuestas cortas para elegir.</summary>
public sealed class AclaracionVacante
{
    public string Pregunta { get; init; } = "";
    public List<string> Opciones { get; init; } = [];
}

/// <summary>Etapas de aprobación de la requisición: líder → RRHH → gerencia.</summary>
public enum EtapaAprobacion
{
    Rrhh,
    Gerencia,
}

/// <summary>Una decisión sobre la requisición: quién, con qué rol, en qué etapa y por qué (si la devolvió).</summary>
public sealed class AprobacionVacante
{
    public EtapaAprobacion Etapa { get; init; }
    public string Decision { get; init; } = "";
    public string Por { get; init; } = "";
    public string Rol { get; init; } = "";
    public string? Motivo { get; init; }

    /// <summary>Se aprobó sola porque quien pidió la vacante tiene autoridad en esa etapa.</summary>
    public bool Automatica { get; init; }
    public DateTimeOffset En { get; init; }
}

/// <summary>Quién aprueba cada etapa. Los nombres de rol son los de la matriz de permisos de Core.</summary>
public static class FlujoRequisicion
{
    public const string Aprobada = "Aprobada";
    public const string Devuelta = "Devuelta";

    /// <summary>Rol que, por ahora, aprueba todas las etapas de una vez.</summary>
    public const string Administrador = "Administrador";

    public static readonly IReadOnlyList<EtapaAprobacion> Etapas = [EtapaAprobacion.Rrhh, EtapaAprobacion.Gerencia];

    private static readonly Dictionary<EtapaAprobacion, string[]> Aprobadores = new()
    {
        [EtapaAprobacion.Rrhh] = ["GerenteRrhh", "Administrador"],
        [EtapaAprobacion.Gerencia] = ["Administrador"],
    };

    public static IReadOnlyList<string> QuienesAprueban(EtapaAprobacion etapa) => Aprobadores[etapa];

    public static bool PuedeAprobar(string rol, EtapaAprobacion etapa) => Aprobadores[etapa].Contains(rol, StringComparer.Ordinal);

    public static string Nombre(EtapaAprobacion etapa) => etapa == EtapaAprobacion.Rrhh ? "RRHH" : "Gerencia";
}

public sealed class RazonVacante
{
    public string Signo { get; init; } = "+";
    public string Texto { get; init; } = "";
    public string Fuente { get; init; } = "";
}

/// <summary>
/// Contenido de una vacante, tal como lo redacta la IA o lo edita una persona. Una vacante es de un solo cargo;
/// <see cref="Cupos"/> son las posiciones disponibles de ese cargo. <see cref="CodigoMunicipio"/> es el código DANE de la ciudad.
/// </summary>
public sealed record ContenidoVacante(
    string Titulo, string Cargo, string Ciudad, int Cupos, string Jornada, string Descripcion,
    decimal SalarioMinimo, decimal SalarioMaximo, IReadOnlyList<string> Requisitos, IReadOnlyList<string> PreguntasFiltro,
    string? CodigoMunicipio = null, bool EmpresaConfidencial = false, bool SalarioOculto = false);

public static class VacanteErrors
{
    public static readonly Error NoExiste = Error.NotFound("Vacante.NoExiste", "Esa vacante no existe.");
    public static readonly Error FraseVacia = Error.Validation("Vacante.FraseVacia", "Describa en una frase lo que necesita.");
    public static readonly Error Redactando = Error.Conflict("Vacante.Redactando", "La IA todavía está redactando la vacante. Espere unos segundos.");
    public static readonly Error Cerrada = Error.Conflict("Vacante.Cerrada", "La vacante está cerrada.");
    public static readonly Error Incompleta = Error.Validation("Vacante.Incompleta", "Para publicar, la vacante necesita título, ciudad, cupos, descripción y un rango salarial.");
    public static readonly Error SalarioInvalido = Error.Validation("Vacante.Salario", "El salario máximo no puede ser menor que el mínimo.");
    public static readonly Error SinAprobar = Error.Conflict("Vacante.SinAprobar", "La requisición debe aprobarse (RRHH y gerencia) antes de publicarse.");
    public static readonly Error Publicada = Error.Conflict("Vacante.Publicada", "La vacante ya está aprobada y publicada: no se puede modificar. Si necesita cambiarla, ciérrela y cree otra.");
    public static readonly Error EnAprobacion = Error.Conflict("Vacante.EnAprobacion", "La requisición está en aprobación: para editarla, quien aprueba debe devolverla.");
    public static readonly Error NoEstaEnAprobacion = Error.Conflict("Vacante.NoEstaEnAprobacion", "La requisición no está esperando aprobación.");
    public static readonly Error NoApruebaEstaEtapa = Error.Validation("Vacante.NoApruebaEstaEtapa", "Su rol no aprueba esta etapa de la requisición.");
    public static readonly Error YaAproboOtraEtapa = Error.Validation("Vacante.YaAproboOtraEtapa", "Ya aprobó otra etapa de esta requisición: la siguiente la aprueba otra persona.");
    public static readonly Error SinMotivo = Error.Validation("Vacante.SinMotivo", "Diga por qué devuelve la requisición, para que puedan ajustarla.");
    public static readonly Error NoPublicada = Error.Conflict("Vacante.NoPublicada", "Esta vacante ya no recibe postulaciones.");
    public static readonly Error NoSePuedeRedactar = Error.Conflict("Vacante.NoSePuedeRedactar", "Solo se le pide a la IA un borrador nuevo mientras la vacante es un borrador.");
}

public sealed class Vacante : AggregateRoot<Guid>
{
    private Vacante(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string EmpresaNombre { get; private init; } = "";
    public Guid? EmpresaUsuariaId { get; private init; }
    public string? EmpresaUsuariaNombre { get; private init; }

    /// <summary>Código público para el portal y para compartir: VAC-261022-3F2A.</summary>
    public string Codigo { get; private init; } = "";
    public string Frase { get; private set; } = "";

    /// <summary>Cargo del catálogo del que parte la vacante; null si se redactó desde cero.</summary>
    public Guid? CargoId { get; private init; }
    public string Titulo { get; private set; } = "";
    public string Cargo { get; private set; } = "";
    public string Ciudad { get; private set; } = "";

    /// <summary>Código DANE (DIVIPOLA) del municipio; null si la ciudad aún no se eligió del catálogo.</summary>
    public string? CodigoMunicipio { get; private set; }
    public int Cupos { get; private set; }
    public string Jornada { get; private set; } = "";
    public string Descripcion { get; private set; } = "";
    public decimal SalarioMinimo { get; private set; }
    public decimal SalarioMaximo { get; private set; }
    public string JustificacionSalario { get; private set; } = "";
    public List<string> Requisitos { get; private set; } = [];
    public List<string> Competencias { get; private set; } = [];
    public List<string> PreguntasFiltro { get; private set; } = [];
    public List<string> Supuestos { get; private set; } = [];
    public List<RazonVacante> Razones { get; private set; } = [];

    /// <summary>Pruebas psicotécnicas de la vacante: se heredan del cargo y se pueden ajustar.</summary>
    public List<PruebaEnBateria> Pruebas { get; private set; } = [];

    /// <summary>Decisiones de la ronda de aprobación actual (se reinician al volver a pedir aprobación).</summary>
    public List<AprobacionVacante> Aprobaciones { get; private set; } = [];

    /// <summary>Etapa que espera decisión; null si no está en aprobación.</summary>
    public EtapaAprobacion? EtapaPendiente { get; private set; }
    public string? AprobacionPedidaPor { get; private set; }

    /// <summary>Por qué la devolvieron la última vez (se muestra en el borrador).</summary>
    public string? MotivoDevolucion { get; private set; }
    public EstadoVacante Estado { get; private set; }
    public string CreadaPor { get; private init; } = "";
    public DateTimeOffset CreadaEn { get; private init; }
    public DateTimeOffset? PublicadaEn { get; private set; }
    public DateTimeOffset? CerradaEn { get; private set; }
    public string? ModeloIa { get; private set; }

    /// <summary>Lo que costó la IA en la vacante misma (redacción), en dólares. Lo de cada postulación se suma aparte.</summary>
    public decimal CostoIaUsd { get; private set; }

    public void SumarCostoIa(decimal costoUsd) => CostoIaUsd += Math.Max(0, costoUsd);
    public string? MensajeIa { get; private set; }

    /// <summary>
    /// Lo que la IA necesita saber antes de redactar (por ejemplo, si la frase pide dos cargos distintos).
    /// Mientras haya aclaraciones, la vacante no tiene contenido: la persona responde y vuelve a pedir el borrador.
    /// </summary>
    public List<AclaracionVacante> Aclaraciones { get; private set; } = [];

    /// <summary>Una persona cambió lo que redactó la IA (queda confirmado por ella).</summary>
    public bool EditadaPorPersona { get; private set; }

    /// <summary>Filtros ATS (excluyentes); null = aún no se configuran.</summary>
    public FiltrosVacante? Filtros { get; private set; }

    /// <summary>Criterios de paso entre etapas; null = los de base (sugerir, afinidad 70, tolerancia salarial 10 %).</summary>
    public ReglasAvance? ReglasAvance { get; private set; }

    public ReglasAvance Reglas => ReglasAvance ?? new ReglasAvance();

    /// <summary>Una persona ajusta cuándo pasa alguien de etapa y si la IA puede hacer sola los pasos de bajo riesgo.</summary>
    public Result ConfigurarReglasAvance(ModoAvance modo, int umbralAfinidad, int toleranciaSalarial)
    {
        if (Estado == EstadoVacante.Cerrada)
        {
            return Result.Failure(VacanteErrors.Cerrada);
        }

        if (umbralAfinidad is < 0 or > 100 || toleranciaSalarial is < 0 or > 100)
        {
            return Result.Failure(Error.Validation("Vacante.ReglasInvalidas", "El umbral y la tolerancia van de 0 a 100."));
        }

        ReglasAvance = new ReglasAvance { Modo = modo, UmbralAfinidad = umbralAfinidad, ToleranciaSalarial = toleranciaSalarial };
        return Result.Success();
    }

    /// <summary>Una persona define qué es excluyente. Solo puede marcar requisitos y preguntas que tiene la vacante.</summary>
    public Result ConfigurarFiltros(IEnumerable<string> requisitos, IEnumerable<string> preguntas, IEnumerable<string> ciudades, int? experienciaMinimaMeses,
        string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoVacante.Cerrada)
        {
            return Result.Failure(VacanteErrors.Cerrada);
        }

        var f = ReglasFiltros.Armar(Requisitos, PreguntasFiltro, requisitos, preguntas, ciudades, experienciaMinimaMeses, por, ahora);
        if (f.IsFailure)
        {
            return f;
        }

        Filtros = f.Value;
        return Result.Success();
    }

    /// <summary>Empresa que se muestra en el portal: la empresa usuaria si es una EST, si no la empresa.</summary>
    public string EmpresaVisible => EmpresaUsuariaNombre ?? EmpresaNombre;

    public const string NombreConfidencial = "Empresa confidencial";

    /// <summary>La oferta se publica sin decir qué empresa contrata.</summary>
    public bool EmpresaConfidencial { get; private set; }

    /// <summary>El portal no muestra el rango salarial («A convenir»); la vacante lo conserva para el ranking y la oferta.</summary>
    public bool SalarioOculto { get; private set; }

    /// <summary>Nombre que ve el candidato en el portal.</summary>
    public string EmpresaEnPortal => EmpresaConfidencial ? NombreConfidencial : EmpresaVisible;

    /// <summary>
    /// A quién autoriza el candidato el tratamiento de sus datos (Ley 1581: el responsable debe estar identificado).
    /// Si la oferta es confidencial, se nombra a la empresa que opera Selvana (la EST o la empresa misma), no a la empresa usuaria.
    /// </summary>
    public string ResponsableDatos => EmpresaConfidencial ? EmpresaNombre : EmpresaVisible;

    public bool RecibePostulaciones => Estado == EstadoVacante.Publicada;

    public static Result<Vacante> Solicitar(
        Guid empresaId, string empresaNombre, Guid? empresaUsuariaId, string? empresaUsuariaNombre, string frase, string usuario, DateTimeOffset ahora,
        Guid? cargoId = null, string? cargo = null)
    {
        if (string.IsNullOrWhiteSpace(frase))
        {
            return VacanteErrors.FraseVacia;
        }

        var id = Guid.CreateVersion7();
        return new Vacante(id)
        {
            EmpresaId = empresaId,
            EmpresaNombre = empresaNombre,
            EmpresaUsuariaId = empresaUsuariaId,
            EmpresaUsuariaNombre = empresaUsuariaNombre,
            Codigo = $"VAC-{ahora:yyMMdd}-{id.ToString("N")[^4..].ToUpperInvariant()}",
            Frase = frase.Trim(),
            CargoId = cargoId,
            Cargo = cargo?.Trim() ?? "",
            Estado = EstadoVacante.Redactando,
            CreadaPor = usuario,
            CreadaEn = ahora,
        };
    }

    /// <summary>Una persona escribe la vacante a mano, sin pedírsela a la IA: nace como borrador confirmado por ella.</summary>
    public static Result<Vacante> CrearManual(
        Guid empresaId, string empresaNombre, Guid? empresaUsuariaId, string? empresaUsuariaNombre, ContenidoVacante contenido, string usuario,
        DateTimeOffset ahora, Guid? cargoId = null)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        if (contenido.SalarioMaximo < contenido.SalarioMinimo)
        {
            return VacanteErrors.SalarioInvalido;
        }

        var id = Guid.CreateVersion7();
        var vacante = new Vacante(id)
        {
            EmpresaId = empresaId,
            EmpresaNombre = empresaNombre,
            EmpresaUsuariaId = empresaUsuariaId,
            EmpresaUsuariaNombre = empresaUsuariaNombre,
            Codigo = $"VAC-{ahora:yyMMdd}-{id.ToString("N")[^4..].ToUpperInvariant()}",
            CargoId = cargoId,
            Estado = EstadoVacante.Borrador,
            CreadaPor = usuario,
            CreadaEn = ahora,
            EditadaPorPersona = true,
        };
        vacante.Aplicar(contenido);
        return vacante;
    }

    /// <summary>La IA no redactó porque la frase pide más de un cargo o le falta algo que no puede suponer: pregunta antes.</summary>
    public void RegistrarAclaraciones(IEnumerable<AclaracionVacante> preguntas)
    {
        if (Estado == EstadoVacante.Redactando)
        {
            Aclaraciones = [.. preguntas.Where(p => !string.IsNullOrWhiteSpace(p.Pregunta))
                .Select(p => new AclaracionVacante { Pregunta = p.Pregunta.Trim(), Opciones = [.. p.Opciones.Select(o => o.Trim()).Where(o => o.Length > 0)] })];
            MensajeIa = null;
            Estado = EstadoVacante.Borrador;
        }
    }

    /// <summary>
    /// Pide a la IA un borrador nuevo con otra frase (por ejemplo, después de responder sus aclaraciones).
    /// Solo mientras es borrador: lo que ya se aprobó o publicó no lo reescribe la IA.
    /// </summary>
    public Result PedirRedaccion(string frase)
    {
        if (Estado != EstadoVacante.Borrador)
        {
            return Result.Failure(VacanteErrors.NoSePuedeRedactar);
        }

        if (string.IsNullOrWhiteSpace(frase))
        {
            return Result.Failure(VacanteErrors.FraseVacia);
        }

        Frase = frase.Trim();
        Aclaraciones = [];
        MensajeIa = null;
        Estado = EstadoVacante.Redactando;
        return Result.Success();
    }

    public void RegistrarBorradorIa(ContenidoVacante contenido, string justificacionSalario, IEnumerable<string> competencias,
        IEnumerable<string> supuestos, IEnumerable<RazonVacante> razones, string modelo)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        if (Estado != EstadoVacante.Redactando)
        {
            return;
        }

        // Si parte de un cargo del catálogo, el nombre del cargo es el del catálogo, diga lo que diga la IA
        // Lo que la persona decidió de la publicación (empresa confidencial, salario oculto) no lo cambia la IA
        contenido = contenido with { EmpresaConfidencial = EmpresaConfidencial, SalarioOculto = SalarioOculto };
        Aplicar(CargoId is null || string.IsNullOrWhiteSpace(Cargo) ? contenido : contenido with { Cargo = Cargo });
        JustificacionSalario = justificacionSalario;
        Competencias = [.. competencias];
        Supuestos = [.. supuestos];
        Razones = [.. razones];
        ModeloIa = modelo;
        MensajeIa = null;
        Estado = EstadoVacante.Borrador;
    }

    /// <summary>La IA no pudo redactar: queda un borrador vacío para que la persona lo llene, con el motivo.</summary>
    public void RegistrarFallaIa(string mensaje)
    {
        if (Estado == EstadoVacante.Redactando)
        {
            MensajeIa = mensaje;
            Titulo = Frase.Length <= 120 ? Frase : Frase[..120];
            Estado = EstadoVacante.Borrador;
        }
    }

    public Result Editar(ContenidoVacante contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        if (Estado == EstadoVacante.Redactando)
        {
            return Result.Failure(VacanteErrors.Redactando);
        }

        if (Estado == EstadoVacante.Cerrada)
        {
            return Result.Failure(VacanteErrors.Cerrada);
        }

        if (Estado == EstadoVacante.EnAprobacion)
        {
            return Result.Failure(VacanteErrors.EnAprobacion);
        }

        // Lo aprobado es lo que se publicó: después de publicarse no cambia
        if (Estado == EstadoVacante.Publicada)
        {
            return Result.Failure(VacanteErrors.Publicada);
        }

        if (contenido.SalarioMaximo < contenido.SalarioMinimo)
        {
            return Result.Failure(VacanteErrors.SalarioInvalido);
        }

        Aplicar(contenido);
        EditadaPorPersona = true;
        return Result.Success();
    }

    public bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(Titulo) && !string.IsNullOrWhiteSpace(Ciudad) && !string.IsNullOrWhiteSpace(Descripcion) && Cupos > 0 && SalarioMinimo > 0;

    /// <summary>
    /// Pide la aprobación de la requisición. Las etapas que puede aprobar quien la pide quedan aprobadas solas;
    /// cuando no queda ninguna etapa, la vacante se publica.
    /// </summary>
    public Result SolicitarAprobacion(string por, string rol, DateTimeOffset ahora)
    {
        switch (Estado)
        {
            case EstadoVacante.Redactando:
                return Result.Failure(VacanteErrors.Redactando);
            case EstadoVacante.Cerrada:
                return Result.Failure(VacanteErrors.Cerrada);
            case EstadoVacante.EnAprobacion or EstadoVacante.Publicada:
                return Result.Success();
        }

        if (!EstaCompleta)
        {
            return Result.Failure(VacanteErrors.Incompleta);
        }

        Aprobaciones = [];
        AprobacionPedidaPor = por;
        MotivoDevolucion = null;
        Estado = EstadoVacante.EnAprobacion;

        // Enviar no aprueba nada: aprobar es un paso aparte, aunque quien envía tenga autoridad para aprobar
        EtapaPendiente = FlujoRequisicion.Etapas[0];
        return Result.Success();
    }

    public Result Aprobar(string por, string rol, DateTimeOffset ahora)
    {
        var etapa = EtapaQueDecide(rol);
        if (etapa.IsFailure)
        {
            return Result.Failure(etapa.Error);
        }

        // Por ahora el administrador aprueba de una vez todas las etapas que falten, y con eso la vacante se publica
        if (rol == FlujoRequisicion.Administrador)
        {
            while (EtapaPendiente is { } pendiente)
            {
                Aprobaciones.Add(new AprobacionVacante { Etapa = pendiente, Decision = FlujoRequisicion.Aprobada, Por = por, Rol = rol, En = ahora });
                Siguiente(ahora);
            }

            return Result.Success();
        }

        // Cuatro ojos: quien ya aprobó a mano una etapa no aprueba la siguiente
        if (Aprobaciones.Any(a => !a.Automatica && a.Decision == FlujoRequisicion.Aprobada && a.Por == por))
        {
            return Result.Failure(VacanteErrors.YaAproboOtraEtapa);
        }

        Aprobaciones.Add(new AprobacionVacante { Etapa = etapa.Value, Decision = FlujoRequisicion.Aprobada, Por = por, Rol = rol, En = ahora });
        Siguiente(ahora);
        return Result.Success();
    }

    /// <summary>Devuelve la requisición al borrador con el motivo, para que quien la pidió la ajuste.</summary>
    public Result Devolver(string por, string rol, string motivo, DateTimeOffset ahora)
    {
        var etapa = EtapaQueDecide(rol);
        if (etapa.IsFailure)
        {
            return Result.Failure(etapa.Error);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result.Failure(VacanteErrors.SinMotivo);
        }

        Aprobaciones.Add(new AprobacionVacante { Etapa = etapa.Value, Decision = FlujoRequisicion.Devuelta, Por = por, Rol = rol, Motivo = motivo.Trim(), En = ahora });
        MotivoDevolucion = motivo.Trim();
        EtapaPendiente = null;
        Estado = EstadoVacante.Borrador;
        return Result.Success();
    }

    private Result<EtapaAprobacion> EtapaQueDecide(string rol)
    {
        if (Estado != EstadoVacante.EnAprobacion || EtapaPendiente is not { } etapa)
        {
            return VacanteErrors.NoEstaEnAprobacion;
        }

        return FlujoRequisicion.PuedeAprobar(rol, etapa) ? etapa : VacanteErrors.NoApruebaEstaEtapa;
    }

    private void Siguiente(DateTimeOffset ahora)
    {
        var indice = FlujoRequisicion.Etapas.ToList().IndexOf(EtapaPendiente!.Value) + 1;
        if (indice < FlujoRequisicion.Etapas.Count)
        {
            EtapaPendiente = FlujoRequisicion.Etapas[indice];
            return;
        }

        EtapaPendiente = null;
        Estado = EstadoVacante.Publicada;
        PublicadaEn = ahora;
    }

    public Result ConfigurarPruebas(IEnumerable<PruebaEnBateria> pruebas)
    {
        if (Estado == EstadoVacante.Cerrada)
        {
            return Result.Failure(VacanteErrors.Cerrada);
        }

        var validas = Baterias.Validar(pruebas);
        if (validas.IsFailure)
        {
            return Result.Failure(validas.Error);
        }

        Pruebas = validas.Value;
        return Result.Success();
    }

    public Result Cerrar(DateTimeOffset ahora)
    {
        if (Estado == EstadoVacante.Redactando)
        {
            return Result.Failure(VacanteErrors.Redactando);
        }

        Estado = EstadoVacante.Cerrada;
        CerradaEn = ahora;
        return Result.Success();
    }

    private void Aplicar(ContenidoVacante c)
    {
        Titulo = c.Titulo.Trim();
        Cargo = c.Cargo.Trim();
        Ciudad = c.Ciudad.Trim();
        CodigoMunicipio = string.IsNullOrWhiteSpace(c.CodigoMunicipio) ? null : c.CodigoMunicipio.Trim();
        EmpresaConfidencial = c.EmpresaConfidencial;
        SalarioOculto = c.SalarioOculto;
        Cupos = c.Cupos;
        Jornada = c.Jornada.Trim();
        Descripcion = c.Descripcion.Trim();
        SalarioMinimo = c.SalarioMinimo;
        SalarioMaximo = Math.Max(c.SalarioMaximo, c.SalarioMinimo);
        Requisitos = [.. c.Requisitos.Select(r => r.Trim()).Where(r => r.Length > 0)];
        PreguntasFiltro = [.. c.PreguntasFiltro.Select(p => p.Trim()).Where(p => p.Length > 0)];

        // Si se quitó un requisito o una pregunta, deja de ser excluyente
        if (Filtros is { } f)
        {
            f.RequisitosExcluyentes = [.. f.RequisitosExcluyentes.Where(r => Requisitos.Contains(r, StringComparer.OrdinalIgnoreCase))];
            f.PreguntasExcluyentes = [.. f.PreguntasExcluyentes.Where(p => PreguntasFiltro.Contains(p, StringComparer.OrdinalIgnoreCase))];
        }
    }
}
