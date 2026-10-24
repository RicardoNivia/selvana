using System.Globalization;
using System.Text;
using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Domain.Pruebas;

/// <summary>Una prueba dentro de una batería: cuánto pesa en el total y el puntaje mínimo para pasar (opcional).</summary>
public sealed class PruebaEnBateria
{
    public string Codigo { get; init; } = "";
    public int Peso { get; init; } = 1;
    public int? Corte { get; init; }
}

public static class BateriaErrors
{
    public static readonly Error PruebaNoExiste = Error.Validation("Bateria.PruebaNoExiste", "Una de las pruebas elegidas no existe en la suite.");
    public static readonly Error PesoInvalido = Error.Validation("Bateria.Peso", "El peso de cada prueba va de 1 a 10.");
    public static readonly Error CorteInvalido = Error.Validation("Bateria.Corte", "El puntaje de corte va de 0 a 100.");
    public static readonly Error CorteEnPersonalidad = Error.Validation("Bateria.CortePersonalidad", "La personalidad no tiene corte: no hay rasgos buenos o malos.");
    public static readonly Error CargoVacio = Error.Validation("Bateria.Cargo", "Falta el nombre del cargo.");
}

public sealed record SugerenciaBateria(IReadOnlyList<PruebaEnBateria> Pruebas, string Razon);

public static class Baterias
{
    public static Result<List<PruebaEnBateria>> Validar(IEnumerable<PruebaEnBateria> pruebas)
    {
        var lista = pruebas.GroupBy(p => p.Codigo).Select(g => g.First()).ToList();
        foreach (var p in lista)
        {
            var definicion = SuitePsicotecnica.Buscar(p.Codigo);
            if (definicion is null)
            {
                return BateriaErrors.PruebaNoExiste;
            }

            if (p.Peso is < 1 or > 10)
            {
                return BateriaErrors.PesoInvalido;
            }

            if (p.Corte is < 0 or > 100)
            {
                return BateriaErrors.CorteInvalido;
            }

            if (p.Corte is not null && definicion.Tipo == TipoPrueba.Personalidad)
            {
                return BateriaErrors.CorteEnPersonalidad;
            }
        }

        return lista;
    }

    /// <summary>Clave para encontrar la batería de un cargo por su nombre: sin tildes, minúsculas y espacios simples.</summary>
    public static string ClaveCargo(string cargo)
    {
        var sb = new StringBuilder();
        foreach (var c in (cargo ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Batería sugerida para un cargo que aún no tiene una configurada. Son reglas sobre el nombre del cargo; el psicólogo y RRHH
    /// la ajustan y la guardan para el cargo.
    /// </summary>
    public static SugerenciaBateria Sugerir(string cargo)
    {
        var c = ClaveCargo(cargo);
        string[] servicio = ["servicio", "asesor", "agente", "cajer", "vendedor", "comercial", "call", "contact", "recepcion", "atencion"];
        string[] operativo = ["bodega", "operari", "auxiliar", "montacarg", "conductor", "produccion", "logistic", "empaque", "alistador", "mensajer"];

        if (servicio.Any(c.Contains))
        {
            return new([new() { Codigo = "SJT-SERVICIO", Peso = 3, Corte = 60 }, new() { Codigo = "ATENCION", Peso = 1, Corte = 60 }, new() { Codigo = "PERSONALIDAD", Peso = 1 }],
                "Es un cargo de atención a clientes: pesa más cómo resuelve situaciones de servicio.");
        }

        if (operativo.Any(c.Contains))
        {
            return new([new() { Codigo = "ATENCION", Peso = 2, Corte = 70 }, new() { Codigo = "NUMERICO", Peso = 2, Corte = 60 }, new() { Codigo = "PERSONALIDAD", Peso = 1 }],
                "Es un cargo operativo: pesan la precisión con los datos y los cálculos del día a día.");
        }

        return new([new() { Codigo = "ATENCION", Peso = 1, Corte = 60 }, new() { Codigo = "NUMERICO", Peso = 1, Corte = 60 }, new() { Codigo = "PERSONALIDAD", Peso = 1 }],
            "Batería general mientras se define una para este cargo.");
    }
}

/// <summary>La batería de pruebas de un cargo. Cada vacante de ese cargo la hereda.</summary>
public sealed class BateriaCargo : AggregateRoot<Guid>
{
    private BateriaCargo(Guid id) : base(id)
    {
    }

    public Guid EmpresaId { get; private init; }
    public string ClaveCargo { get; private init; } = "";
    public string CargoNombre { get; private set; } = "";
    public List<PruebaEnBateria> Pruebas { get; private set; } = [];
    public string ActualizadaPor { get; private set; } = "";
    public DateTimeOffset ActualizadaEn { get; private set; }

    public static Result<BateriaCargo> Crear(Guid empresaId, string cargo, IEnumerable<PruebaEnBateria> pruebas, string usuario, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(cargo))
        {
            return BateriaErrors.CargoVacio;
        }

        var b = new BateriaCargo(Guid.CreateVersion7()) { EmpresaId = empresaId, ClaveCargo = Baterias.ClaveCargo(cargo) };
        var cambio = b.Cambiar(cargo, pruebas, usuario, ahora);
        return cambio.IsFailure ? cambio.Error : b;
    }

    public Result Cambiar(string cargo, IEnumerable<PruebaEnBateria> pruebas, string usuario, DateTimeOffset ahora)
    {
        var validas = Baterias.Validar(pruebas);
        if (validas.IsFailure)
        {
            return Result.Failure(validas.Error);
        }

        CargoNombre = cargo.Trim();
        Pruebas = validas.Value;
        ActualizadaPor = usuario;
        ActualizadaEn = ahora;
        return Result.Success();
    }
}
