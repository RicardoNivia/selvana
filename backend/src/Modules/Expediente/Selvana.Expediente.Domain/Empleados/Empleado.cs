using System.Globalization;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Domain.Empleados;

public enum EstadoEmpleado
{
    Activo,
    Retirado,
}

public enum TipoNovedad
{
    Ingreso,
    CambioCargo,
    CambioSalario,
    Traslado,
    CambioContrato,
    ActualizacionDatos,
    Retiro,
}

/// <summary>Dónde y cómo trabaja hoy: cargo, área, centro de costo, sede, empresa usuaria, contrato y salario.</summary>
public sealed record SituacionLaboral(
    string Cargo, string Area, string CentroCosto, string Sede, Guid? EmpresaUsuariaId, string? EmpresaUsuaria, string TipoContrato, decimal SalarioCop,
    DateOnly? FechaFinContrato);

/// <summary>Seguridad social del empleado.</summary>
public sealed class Afiliaciones
{
    public string Eps { get; set; } = "";
    public string FondoPension { get; set; } = "";
    public string Arl { get; set; } = "";
    public int? ClaseRiesgoArl { get; set; }
    public string CajaCompensacion { get; set; } = "";
}

/// <summary>Una novedad del historial: qué cambió (antes → después), desde cuándo, por qué y quién la registró.</summary>
public sealed class Novedad
{
    public Guid Id { get; init; }
    public TipoNovedad Tipo { get; init; }
    public DateOnly FechaEfectiva { get; init; }
    public List<string> Cambios { get; init; } = [];
    public string Motivo { get; init; } = "";
    public string Por { get; init; } = "";
    public DateTimeOffset En { get; init; }
}

public sealed class VersionDocumento
{
    public Guid ArchivoId { get; init; }
    public string NombreArchivo { get; init; } = "";
    public DateOnly? VenceEn { get; init; }
    public string? Nota { get; init; }
    public string SubidoPor { get; init; } = "";
    public DateTimeOffset SubidoEn { get; init; }
}

/// <summary>Un documento de la carpeta (cédula, contrato, certificado…) con todas sus versiones; la vigente es la última.</summary>
public sealed class DocumentoEmpleado
{
    public Guid Id { get; init; }
    public string Tipo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public List<VersionDocumento> Versiones { get; init; } = [];

    public VersionDocumento? Vigente => Versiones.Count == 0 ? null : Versiones[^1];
}

public enum NivelAlertaEmpleado
{
    Vencido,
    PorVencer,
    Falta,
}

public sealed record AlertaEmpleado(NivelAlertaEmpleado Nivel, string Texto, string Fuente);

public static class EmpleadoErrors
{
    public static readonly Error NoExiste = Error.NotFound("Expediente.EmpleadoNoExiste", "Ese empleado no existe en su empresa.");
    public static readonly Error Retirado = Error.Conflict("Expediente.Retirado", "El empleado está retirado: su expediente ya no se modifica.");
    public static readonly Error SinCambios = Error.Validation("Expediente.SinCambios", "La novedad no cambia nada de la situación actual.");
    public static readonly Error FechaRetiro = Error.Validation("Expediente.FechaRetiro", "La fecha de retiro no puede ser anterior al ingreso.");
    public static readonly Error DocumentoNoExiste = Error.NotFound("Expediente.DocumentoNoExiste", "Ese documento no está en la carpeta del empleado.");
    public static readonly Error YaExiste = Error.Conflict("Expediente.YaExiste", "Ya hay un empleado activo con ese documento en su empresa.");
}

/// <summary>
/// Expediente vivo del empleado: datos, situación laboral actual, afiliaciones, historial de novedades y carpeta documental con versiones.
/// Nace al cerrar la vinculación, a mano o desde el archivo de planta; todo lo que cambia queda como novedad con su fecha efectiva.
/// </summary>
public sealed class Empleado : AggregateRoot<Guid>
{
    public const int DiasAvisoVencimiento = 30;
    private static readonly CultureInfo Colombia = new("es-CO");

    private Empleado(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string Codigo { get; private init; } = "";
    public string Documento { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public string Correo { get; private set; } = "";
    public string Celular { get; private set; } = "";
    public string Ciudad { get; private set; } = "";

    /// <summary>«Vinculación», «Manual» o «Archivo de planta».</summary>
    public string Origen { get; private init; } = "";
    public Guid? ProcesoVinculacionId { get; private init; }
    public Guid? CandidatoId { get; private init; }

    public string Cargo { get; private set; } = "";
    public string Area { get; private set; } = "";
    public string CentroCosto { get; private set; } = "";
    public string Sede { get; private set; } = "";
    public Guid? EmpresaUsuariaId { get; private set; }
    public string? EmpresaUsuaria { get; private set; }
    public string TipoContrato { get; private set; } = "";
    public decimal SalarioCop { get; private set; }
    public DateOnly FechaIngreso { get; private init; }
    public DateOnly? FechaFinContrato { get; private set; }
    public Afiliaciones Afiliaciones { get; private set; } = new();

    public EstadoEmpleado Estado { get; private set; }
    public DateOnly? FechaRetiro { get; private set; }
    public string? MotivoRetiro { get; private set; }

    /// <summary>Lo primero que vence (documento vigente o fin del contrato); sirve para filtrar alertas en la base.</summary>
    public DateOnly? ProximoVencimiento { get; private set; }
    public bool AfiliacionesCompletas { get; private set; }

    public List<Novedad> Novedades { get; private set; } = [];
    public List<DocumentoEmpleado> Documentos { get; private set; } = [];
    public DateTimeOffset CreadoEn { get; private init; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    public SituacionLaboral Situacion => new(Cargo, Area, CentroCosto, Sede, EmpresaUsuariaId, EmpresaUsuaria, TipoContrato, SalarioCop, FechaFinContrato);

    public static Empleado Crear(Guid empresaId, string origen, string documento, string nombre, string correo, string celular, string ciudad,
        SituacionLaboral situacion, DateOnly fechaIngreso, Afiliaciones afiliaciones, string por, DateTimeOffset ahora, Guid? procesoVinculacionId = null,
        Guid? candidatoId = null)
    {
        var id = Guid.CreateVersion7();
        var e = new Empleado(id)
        {
            EmpresaId = empresaId,
            Codigo = $"EMP-{id.ToString("N")[^6..].ToUpperInvariant()}",
            Origen = origen,
            ProcesoVinculacionId = procesoVinculacionId,
            CandidatoId = candidatoId,
            FechaIngreso = fechaIngreso,
            Estado = EstadoEmpleado.Activo,
            Afiliaciones = afiliaciones,
            CreadoEn = ahora,
        };
        e.PonerDatos(documento, nombre, correo, celular, ciudad);
        e.Poner(situacion);
        e.Novedades.Add(new Novedad
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoNovedad.Ingreso,
            FechaEfectiva = fechaIngreso,
            Cambios = [$"Ingresa como {situacion.Cargo}", $"{situacion.TipoContrato} · {Pesos(situacion.SalarioCop)}"],
            Motivo = origen == "Vinculación" ? "Viene del proceso de vinculación" : origen,
            Por = por,
            En = ahora,
        });
        e.Recalcular();
        e.ActualizadoEn = ahora;
        return e;
    }

    public static string Pesos(decimal valor) => valor.ToString("$#,##0", Colombia);

    private void PonerDatos(string documento, string nombre, string correo, string celular, string ciudad)
    {
        Documento = documento.Trim();
        Nombre = nombre.Trim();
        Correo = correo.Trim();
        Celular = celular.Trim();
        Ciudad = ciudad.Trim();
    }

    private void Poner(SituacionLaboral s)
    {
        Cargo = s.Cargo.Trim();
        Area = s.Area.Trim();
        CentroCosto = s.CentroCosto.Trim();
        Sede = s.Sede.Trim();
        EmpresaUsuariaId = s.EmpresaUsuariaId;
        EmpresaUsuaria = string.IsNullOrWhiteSpace(s.EmpresaUsuaria) ? null : s.EmpresaUsuaria.Trim();
        TipoContrato = s.TipoContrato.Trim();
        SalarioCop = s.SalarioCop;
        FechaFinContrato = s.FechaFinContrato;
    }

    /// <summary>Corrige datos personales y afiliaciones (no es una novedad laboral, pero queda en el historial).</summary>
    public Result ActualizarDatos(string nombre, string correo, string celular, string ciudad, Afiliaciones afiliaciones, string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoEmpleado.Retirado)
        {
            return Result.Failure(EmpleadoErrors.Retirado);
        }

        var cambios = new List<string>();
        void Comparar(string campo, string antes, string despues)
        {
            if (!string.Equals(antes.Trim(), despues.Trim(), StringComparison.Ordinal))
            {
                cambios.Add($"{campo}: {Vacio(antes)} → {Vacio(despues)}");
            }
        }

        Comparar("Nombre", Nombre, nombre);
        Comparar("Correo", Correo, correo);
        Comparar("Celular", Celular, celular);
        Comparar("Ciudad", Ciudad, ciudad);
        Comparar("EPS", Afiliaciones.Eps, afiliaciones.Eps);
        Comparar("Pensión", Afiliaciones.FondoPension, afiliaciones.FondoPension);
        Comparar("ARL", Afiliaciones.Arl, afiliaciones.Arl);
        Comparar("Clase de riesgo", Afiliaciones.ClaseRiesgoArl?.ToString(CultureInfo.InvariantCulture) ?? "", afiliaciones.ClaseRiesgoArl?.ToString(CultureInfo.InvariantCulture) ?? "");
        Comparar("Caja de compensación", Afiliaciones.CajaCompensacion, afiliaciones.CajaCompensacion);
        if (cambios.Count == 0)
        {
            return Result.Failure(EmpleadoErrors.SinCambios);
        }

        PonerDatos(Documento, nombre, correo, celular, ciudad);
        Afiliaciones = afiliaciones;
        Novedades.Add(new Novedad { Id = Guid.CreateVersion7(), Tipo = TipoNovedad.ActualizacionDatos, FechaEfectiva = DateOnly.FromDateTime(ahora.UtcDateTime), Cambios = cambios, Por = por, En = ahora });
        Recalcular();
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Cambio de cargo, salario, traslado o contrato: compara con la situación actual y guarda lo que cambió (antes → después).</summary>
    public Result RegistrarNovedad(TipoNovedad tipo, SituacionLaboral nueva, DateOnly fechaEfectiva, string motivo, string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoEmpleado.Retirado)
        {
            return Result.Failure(EmpleadoErrors.Retirado);
        }

        var a = Situacion;
        var cambios = new List<string>();
        void Comparar(string campo, string antes, string despues)
        {
            if (!string.Equals(antes.Trim(), despues.Trim(), StringComparison.Ordinal))
            {
                cambios.Add($"{campo}: {Vacio(antes)} → {Vacio(despues)}");
            }
        }

        Comparar("Cargo", a.Cargo, nueva.Cargo);
        Comparar("Área", a.Area, nueva.Area);
        Comparar("Centro de costo", a.CentroCosto, nueva.CentroCosto);
        Comparar("Sede", a.Sede, nueva.Sede);
        Comparar("Empresa usuaria", a.EmpresaUsuaria ?? "", nueva.EmpresaUsuaria ?? "");
        Comparar("Contrato", a.TipoContrato, nueva.TipoContrato);
        Comparar("Salario", Pesos(a.SalarioCop), Pesos(nueva.SalarioCop));
        Comparar("Fin del contrato", a.FechaFinContrato?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
            nueva.FechaFinContrato?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "");
        if (cambios.Count == 0)
        {
            return Result.Failure(EmpleadoErrors.SinCambios);
        }

        Poner(nueva);
        Novedades.Add(new Novedad { Id = Guid.CreateVersion7(), Tipo = tipo, FechaEfectiva = fechaEfectiva, Cambios = cambios, Motivo = motivo.Trim(), Por = por, En = ahora });
        Recalcular();
        ActualizadoEn = ahora;
        return Result.Success();
    }

    public Result Retirar(DateOnly fecha, string motivo, string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoEmpleado.Retirado)
        {
            return Result.Failure(EmpleadoErrors.Retirado);
        }

        if (fecha < FechaIngreso)
        {
            return Result.Failure(EmpleadoErrors.FechaRetiro);
        }

        Estado = EstadoEmpleado.Retirado;
        FechaRetiro = fecha;
        MotivoRetiro = motivo.Trim();
        Novedades.Add(new Novedad { Id = Guid.CreateVersion7(), Tipo = TipoNovedad.Retiro, FechaEfectiva = fecha, Cambios = [$"Retiro: {MotivoRetiro}"], Motivo = MotivoRetiro, Por = por, En = ahora });
        Recalcular();
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Agrega un documento nuevo a la carpeta, o una versión nueva si se indica el documento.</summary>
    public Result<DocumentoEmpleado> GuardarDocumento(Guid? documentoId, string tipo, string nombre, Guid archivoId, string nombreArchivo, DateOnly? venceEn, string? nota,
        string por, DateTimeOffset ahora)
    {
        DocumentoEmpleado? doc;
        if (documentoId is { } id)
        {
            doc = Documentos.FirstOrDefault(d => d.Id == id);
            if (doc is null)
            {
                return Result.Failure<DocumentoEmpleado>(EmpleadoErrors.DocumentoNoExiste);
            }
        }
        else
        {
            doc = new DocumentoEmpleado { Id = Guid.CreateVersion7(), Tipo = tipo.Trim(), Nombre = nombre.Trim() };
            Documentos.Add(doc);
        }

        doc.Versiones.Add(new VersionDocumento { ArchivoId = archivoId, NombreArchivo = nombreArchivo, VenceEn = venceEn, Nota = nota?.Trim(), SubidoPor = por, SubidoEn = ahora });
        Recalcular();
        ActualizadoEn = ahora;
        return doc;
    }

    /// <summary>Lo que hay que atender: documentos vencidos o por vencer, contrato que termina pronto y afiliaciones sin registrar.</summary>
    public IReadOnlyList<AlertaEmpleado> Alertas(DateOnly hoy)
    {
        var alertas = new List<AlertaEmpleado>();
        if (Estado == EstadoEmpleado.Retirado)
        {
            return alertas;
        }

        foreach (var d in Documentos.Where(d => d.Vigente?.VenceEn is not null))
        {
            var vence = d.Vigente!.VenceEn!.Value;
            if (vence < hoy)
            {
                alertas.Add(new(NivelAlertaEmpleado.Vencido, $"«{d.Nombre}» venció el {vence:dd/MM/yyyy}.", "Carpeta"));
            }
            else if (vence <= hoy.AddDays(DiasAvisoVencimiento))
            {
                alertas.Add(new(NivelAlertaEmpleado.PorVencer, $"«{d.Nombre}» vence el {vence:dd/MM/yyyy}.", "Carpeta"));
            }
        }

        if (FechaFinContrato is { } fin && fin >= hoy && fin <= hoy.AddDays(45))
        {
            alertas.Add(new(NivelAlertaEmpleado.PorVencer,
                $"El contrato termina el {fin:dd/MM/yyyy}: el preaviso de no renovación es de 30 días.", "Art. 46 CST"));
        }
        else if (FechaFinContrato is { } f && f < hoy)
        {
            alertas.Add(new(NivelAlertaEmpleado.Vencido, $"El contrato terminó el {f:dd/MM/yyyy}: registre la prórroga o el retiro.", "Contrato"));
        }

        var faltan = new[] { ("EPS", Afiliaciones.Eps), ("pensión", Afiliaciones.FondoPension), ("ARL", Afiliaciones.Arl), ("caja de compensación", Afiliaciones.CajaCompensacion) }
            .Where(x => string.IsNullOrWhiteSpace(x.Item2)).Select(x => x.Item1).ToList();
        if (faltan.Count > 0)
        {
            alertas.Add(new(NivelAlertaEmpleado.Falta, $"Falta registrar: {string.Join(", ", faltan)}.", "Afiliaciones"));
        }

        return alertas;
    }

    private void Recalcular()
    {
        var fechas = Documentos.Select(d => d.Vigente?.VenceEn).Append(FechaFinContrato).OfType<DateOnly>().ToList();
        ProximoVencimiento = Estado == EstadoEmpleado.Retirado || fechas.Count == 0 ? null : fechas.Min();
        AfiliacionesCompletas = new[] { Afiliaciones.Eps, Afiliaciones.FondoPension, Afiliaciones.Arl, Afiliaciones.CajaCompensacion }.All(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string Vacio(string t) => string.IsNullOrWhiteSpace(t) ? "—" : t.Trim();
}
