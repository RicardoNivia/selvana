using Selvana.SharedKernel.Domain;

namespace Selvana.Core.Domain.Estructura;

public enum EstadoImportacion
{
    /// <summary>Se leyó la nómina y la IA está buscando cargos repetidos.</summary>
    OrganizandoConIa,

    /// <summary>Las propuestas están listas para que una persona las revise.</summary>
    ListaParaRevisar,
    Aplicada,
    Descartada,
}

public enum TipoPropuesta
{
    UnirCargos,
    AsignarSede,
}

/// <summary>Quién hizo la propuesta: la IA (violeta en la interfaz) o una regla fija del sistema.</summary>
public enum OrigenPropuesta
{
    Ia,
    Regla,
}

/// <summary>Un empleado como lo leyó el conector, sin nombres: la estructura no guarda datos personales.</summary>
public sealed record EmpleadoLeido(
    string Documento, string CargoCodigo, string CargoNombre, string Area, string CentroCostoCodigo, string CentroCostoNombre, string Sede);

public sealed class CargoEnLectura
{
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string Area { get; init; } = "";
    public int Empleados { get; init; }
}

public sealed class CentroCostoEnLectura
{
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string Sede { get; init; } = "";
    public int Empleados { get; init; }
}

public sealed class ConteoEnLectura
{
    public string Nombre { get; init; } = "";
    public int Empleados { get; init; }
}

public sealed class Propuesta : Entity<Guid>
{
    private Propuesta(Guid id) : base(id)
    {
    }

    public TipoPropuesta Tipo { get; private init; }
    public OrigenPropuesta Origen { get; private init; }
    public string Titulo { get; private init; } = "";
    public string Porque { get; private init; } = "";
    public string Evidencia { get; private init; } = "";
    public bool Incluida { get; private set; } = true;

    /// <summary>UnirCargos: códigos a unir. AsignarSede: centro de costo afectado.</summary>
    public List<string> Codigos { get; private init; } = [];

    /// <summary>UnirCargos: nombre final. AsignarSede: sede asignada.</summary>
    public string Valor { get; private init; } = "";

    /// <summary>Empleados afectados.</summary>
    public int Cantidad { get; private init; }

    internal static Propuesta UnirCargos(string nombreFinal, List<string> codigos, string porque, string evidencia, int empleados) => new(Guid.CreateVersion7())
    {
        Tipo = TipoPropuesta.UnirCargos,
        Origen = OrigenPropuesta.Ia,
        Titulo = $"Unir {codigos.Count} nombres de cargo en «{nombreFinal}»",
        Porque = porque,
        Evidencia = evidencia,
        Codigos = codigos,
        Valor = nombreFinal,
        Cantidad = empleados,
    };

    internal static Propuesta AsignarSede(string centroCosto, string nombreCentro, string sede, int empleados) => new(Guid.CreateVersion7())
    {
        Tipo = TipoPropuesta.AsignarSede,
        Origen = OrigenPropuesta.Regla,
        Titulo = $"Asignar sede {sede} a {empleados} empleados sin sede",
        Porque = $"Están en el centro de costo «{nombreCentro}», cuyos demás empleados son de {sede}.",
        Evidencia = $"Nómina · {centroCosto}",
        Codigos = [centroCosto],
        Valor = sede,
        Cantidad = empleados,
    };

    internal void CambiarInclusion(bool incluida) => Incluida = incluida;
}

public sealed class ImportacionNomina : Entity<Guid>
{
    private readonly List<Propuesta> _propuestas = [];

    private ImportacionNomina(Guid id) : base(id)
    {
    }

    public string Fuente { get; private init; } = "";
    public DateTimeOffset LeidaEn { get; private init; }
    public EstadoImportacion Estado { get; private set; }
    public int TotalEmpleados { get; private init; }
    public List<string> Errores { get; private init; } = [];
    public List<ConteoEnLectura> Sedes { get; private init; } = [];
    public List<ConteoEnLectura> Areas { get; private init; } = [];
    public List<CentroCostoEnLectura> CentrosCosto { get; private init; } = [];
    public List<CargoEnLectura> Cargos { get; private init; } = [];
    public string? MensajeIa { get; private set; }
    public string? ModeloIa { get; private set; }
    public DateTimeOffset? AplicadaEn { get; private set; }
    public string? AplicadaPor { get; private set; }
    public IReadOnlyList<Propuesta> Propuestas => _propuestas.AsReadOnly();

    internal static ImportacionNomina Leer(string fuente, IReadOnlyList<EmpleadoLeido> empleados, IEnumerable<string> erroresLectura, DateTimeOffset ahora)
    {
        var errores = erroresLectura.ToList();
        var repetidos = empleados.GroupBy(e => e.Documento).Where(g => g.Count() > 1).ToList();
        foreach (var g in repetidos)
        {
            errores.Add($"La cédula {g.Key} aparece en {g.Count()} empleados. Corríjala en la nómina o márquelos como la misma persona.");
        }

        var validos = empleados.Where(e => repetidos.All(r => r.Key != e.Documento)).ToList();
        var comparar = StringComparer.OrdinalIgnoreCase;

        var importacion = new ImportacionNomina(Guid.CreateVersion7())
        {
            Fuente = fuente,
            LeidaEn = ahora,
            Estado = EstadoImportacion.OrganizandoConIa,
            TotalEmpleados = validos.Count,
            Errores = errores,
            Sedes = [.. validos.Where(e => e.Sede.Length > 0).GroupBy(e => e.Sede, comparar)
                .Select(g => new ConteoEnLectura { Nombre = g.Key, Empleados = g.Count() }).OrderByDescending(c => c.Empleados)],
            Areas = [.. validos.Where(e => e.Area.Length > 0).GroupBy(e => e.Area, comparar)
                .Select(g => new ConteoEnLectura { Nombre = g.Key, Empleados = g.Count() }).OrderByDescending(c => c.Empleados)],
            CentrosCosto = [.. validos.GroupBy(e => e.CentroCostoCodigo, comparar).Select(g => new CentroCostoEnLectura
            {
                Codigo = g.Key,
                Nombre = g.First().CentroCostoNombre,
                Sede = g.Where(e => e.Sede.Length > 0).GroupBy(e => e.Sede, comparar).OrderByDescending(s => s.Count()).Select(s => s.Key).FirstOrDefault() ?? "",
                Empleados = g.Count(),
            }).OrderBy(c => c.Codigo)],
            Cargos = [.. validos.GroupBy(e => CodigoCargo(e), comparar).Select(g => new CargoEnLectura
            {
                Codigo = g.Key,
                Nombre = g.First().CargoNombre,
                Area = g.GroupBy(e => e.Area, comparar).OrderByDescending(a => a.Count()).First().Key,
                Empleados = g.Count(),
            }).OrderByDescending(c => c.Empleados)],
        };

        // Regla fija (no IA): empleados sin sede toman la sede de su centro de costo.
        foreach (var cc in importacion.CentrosCosto.Where(c => c.Sede.Length > 0))
        {
            var sinSede = validos.Count(e => e.Sede.Length == 0 && comparar.Equals(e.CentroCostoCodigo, cc.Codigo));
            if (sinSede > 0)
            {
                importacion._propuestas.Add(Propuesta.AsignarSede(cc.Codigo, cc.Nombre, cc.Sede, sinSede));
            }
        }

        return importacion;
    }

    internal static string CodigoCargo(EmpleadoLeido e) => e.CargoCodigo.Length > 0 ? e.CargoCodigo : e.CargoNombre;

    internal bool Abierta => Estado is EstadoImportacion.OrganizandoConIa or EstadoImportacion.ListaParaRevisar;

    internal void AgregarUnionesIa(IEnumerable<(string NombreFinal, List<string> Codigos, string Porque)> uniones, string modelo)
    {
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nombreFinal, codigos, porque) in uniones)
        {
            var validos = codigos.Where(c => Cargos.Any(x => string.Equals(x.Codigo, c, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // La IA puede equivocarse: solo se aceptan uniones de 2 o más cargos que existan y no se repitan.
            if (validos.Count < 2 || validos.Any(usados.Contains) || string.IsNullOrWhiteSpace(nombreFinal))
            {
                continue;
            }

            usados.UnionWith(validos);
            var cargos = Cargos.Where(c => validos.Contains(c.Codigo, StringComparer.OrdinalIgnoreCase)).ToList();
            var evidencia = "Nómina · " + string.Join(" + ", cargos.Select(c => c.Empleados)) + " empleados";
            var porqueCompleto = $"{string.Join(", ", cargos.Select(c => $"«{c.Nombre}»"))}. {porque}";
            _propuestas.Add(Propuesta.UnirCargos(nombreFinal.Trim(), validos, porqueCompleto, evidencia, cargos.Sum(c => c.Empleados)));
        }

        ModeloIa = modelo;
        MensajeIa = null;
        Estado = EstadoImportacion.ListaParaRevisar;
    }

    internal void RegistrarFallaIa(string mensaje)
    {
        MensajeIa = mensaje;
        Estado = EstadoImportacion.ListaParaRevisar;
    }

    internal Propuesta? BuscarPropuesta(Guid id) => _propuestas.Find(p => p.Id == id);

    internal void MarcarAplicada(string usuario, DateTimeOffset ahora)
    {
        Estado = EstadoImportacion.Aplicada;
        AplicadaPor = usuario;
        AplicadaEn = ahora;
    }

    internal void Descartar() => Estado = EstadoImportacion.Descartada;
}
