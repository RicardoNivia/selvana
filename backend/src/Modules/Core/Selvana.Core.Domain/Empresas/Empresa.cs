using System.Text.RegularExpressions;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Domain.Empresas;

public static class EmpresaErrors
{
    public static readonly Error NitInvalido = Error.Validation("Empresa.NitInvalido", "Escriba el NIT con 9 dígitos y su dígito de verificación, por ejemplo 900.412.887-9.");
    public static readonly Error NombreVacio = Error.Validation("Empresa.NombreVacio", "Escriba la razón social.");
    public static readonly Error NoEsTemporal = Error.Conflict("Empresa.NoEsTemporal", "Solo las empresas de servicios temporales manejan empresas usuarias.");
    public static readonly Error NitRepetido = Error.Conflict("Empresa.NitRepetido", "Ya hay una empresa usuaria con ese NIT.");
    public static readonly Error EmpresaUsuariaNoExiste = Error.NotFound("Empresa.UsuariaNoExiste", "Esa empresa usuaria no existe.");
    public static readonly Error NoExiste = Error.NotFound("Empresa.NoExiste", "La empresa no existe.");
    public static readonly Error YaRegistrada = Error.Conflict("Empresa.YaRegistrada",
        "Ya hay una empresa con ese NIT en Selvana. Pídale acceso a su administrador o escríbanos si cree que es un error.");
}

/// <summary>Cliente de una empresa de servicios temporales (EST): sus datos nunca se mezclan con los de otra.</summary>
public sealed class EmpresaUsuaria : Entity<Guid>
{
    private EmpresaUsuaria(Guid id) : base(id)
    {
    }

    public string Nombre { get; private set; } = "";
    public string Nit { get; private init; } = "";
    public string Sector { get; private set; } = "";
    public string Ciudad { get; private set; } = "";
    public bool Activa { get; private set; } = true;
    public DateTimeOffset CreadaEn { get; private init; }

    internal static EmpresaUsuaria Crear(string nombre, string nit, string sector, string ciudad, DateTimeOffset ahora) => new(Guid.CreateVersion7())
    {
        Nombre = nombre.Trim(),
        Nit = nit,
        Sector = sector.Trim(),
        Ciudad = ciudad.Trim(),
        CreadaEn = ahora,
    };

    internal void Editar(string nombre, string sector, string ciudad, bool activa)
    {
        Nombre = nombre.Trim();
        Sector = sector.Trim();
        Ciudad = ciudad.Trim();
        Activa = activa;
    }
}

/// <summary>El cliente de Selvana (tenant). Si es una EST, administra sus empresas usuarias.</summary>
public sealed partial class Empresa : AggregateRoot<Guid>
{
    private readonly List<EmpresaUsuaria> _empresasUsuarias = [];

    private Empresa(Guid id) : base(id)
    {
    }

    public string Nombre { get; private set; } = "";
    public string Nit { get; private set; } = "";
    public bool EsTemporal { get; private set; }
    public IReadOnlyList<EmpresaUsuaria> EmpresasUsuarias => _empresasUsuarias.AsReadOnly();

    /// <summary>Sector y tamaño que dio al registrarse (para el recorrido guiado y la analítica de uso).</summary>
    public string Sector { get; private set; } = "";
    public string Tamano { get; private set; } = "";

    /// <summary>Hasta cuándo va la prueba gratuita; null = no está en prueba (empresas de ejemplo o con plan).</summary>
    public DateTimeOffset? PruebaHasta { get; private set; }
    public DateTimeOffset? RegistradaEn { get; private set; }

    public const int DiasPrueba = 30;

    /// <summary>Autoservicio (sprint 10): la empresa se registra sola y arranca una prueba gratuita de 30 días.</summary>
    public static Result<Empresa> Registrar(Guid id, string nombre, string nit, bool esTemporal, string sector, string tamano, DateTimeOffset ahora)
    {
        var r = Crear(id, nombre, nit, esTemporal);
        if (r.IsFailure)
        {
            return r;
        }

        var e = r.Value;
        e.Sector = (sector ?? "").Trim();
        e.Tamano = (tamano ?? "").Trim();
        e.RegistradaEn = ahora;
        e.PruebaHasta = ahora.AddDays(DiasPrueba);
        return e;
    }

    /// <summary>Días que le quedan a la prueba (0 si terminó); null si no está en prueba.</summary>
    public int? DiasDePrueba(DateTimeOffset ahora) => PruebaHasta is { } fin ? Math.Max(0, (int)Math.Ceiling((fin - ahora).TotalDays)) : null;

    public static Result<Empresa> Crear(Guid id, string nombre, string nit, bool esTemporal)
    {
        var normalizado = NormalizarNit(nit);
        if (normalizado is null)
        {
            return EmpresaErrors.NitInvalido;
        }

        return string.IsNullOrWhiteSpace(nombre)
            ? EmpresaErrors.NombreVacio
            : new Empresa(id) { Nombre = nombre.Trim(), Nit = normalizado, EsTemporal = esTemporal };
    }

    public Result<EmpresaUsuaria> AgregarEmpresaUsuaria(string nombre, string nit, string sector, string ciudad, DateTimeOffset ahora)
    {
        if (!EsTemporal)
        {
            return EmpresaErrors.NoEsTemporal;
        }

        var normalizado = NormalizarNit(nit);
        if (normalizado is null)
        {
            return EmpresaErrors.NitInvalido;
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return EmpresaErrors.NombreVacio;
        }

        if (_empresasUsuarias.Any(e => e.Nit == normalizado))
        {
            return EmpresaErrors.NitRepetido;
        }

        var empresaUsuaria = EmpresaUsuaria.Crear(nombre, normalizado, sector, ciudad, ahora);
        _empresasUsuarias.Add(empresaUsuaria);
        return empresaUsuaria;
    }

    public Result EditarEmpresaUsuaria(Guid id, string nombre, string sector, string ciudad, bool activa)
    {
        var empresaUsuaria = _empresasUsuarias.Find(e => e.Id == id);
        if (empresaUsuaria is null)
        {
            return Result.Failure(EmpresaErrors.EmpresaUsuariaNoExiste);
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Result.Failure(EmpresaErrors.NombreVacio);
        }

        empresaUsuaria.Editar(nombre, sector, ciudad, activa);
        return Result.Success();
    }

    /// <summary>NIT colombiano: 9 dígitos y el dígito de verificación. Devuelve "900412887-9" o null si no es válido.</summary>
    public static string? NormalizarNit(string nit)
    {
        var limpio = (nit ?? "").Replace(".", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        var m = FormatoNit().Match(limpio);
        if (!m.Success)
        {
            return null;
        }

        var cuerpo = m.Groups[1].Value;
        var dv = m.Groups[2].Success ? m.Groups[2].Value : null;
        var esperado = DigitoVerificacion(cuerpo);
        return dv is null || dv == esperado.ToString(System.Globalization.CultureInfo.InvariantCulture) ? $"{cuerpo}-{esperado}" : null;
    }

    /// <summary>Algoritmo de la DIAN (módulo 11 con los pesos oficiales).</summary>
    internal static int DigitoVerificacion(string cuerpo)
    {
        int[] pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];
        var suma = 0;
        for (var i = 0; i < cuerpo.Length; i++)
        {
            suma += (cuerpo[cuerpo.Length - 1 - i] - '0') * pesos[i];
        }

        var residuo = suma % 11;
        return residuo > 1 ? 11 - residuo : residuo;
    }

    [GeneratedRegex(@"^(\d{9})(?:-(\d))?$")]
    private static partial Regex FormatoNit();
}
