using Selvana.SharedKernel.Domain;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Domain.Estructura;

public static class EstructuraErrors
{
    public static readonly Error SinEmpleados = Error.Validation("Nomina.SinEmpleados", "No se encontró ningún empleado válido en el archivo.");
    public static readonly Error ImportacionNoExiste = Error.NotFound("Nomina.ImportacionNoExiste", "Esa importación no existe.");
    public static readonly Error IaTrabajando = Error.Conflict("Nomina.IaTrabajando", "La IA todavía está organizando la nómina. Intente en unos segundos.");
    public static readonly Error ImportacionCerrada = Error.Conflict("Nomina.ImportacionCerrada", "Esta importación ya se aplicó o se descartó.");
    public static readonly Error PropuestaNoExiste = Error.NotFound("Nomina.PropuestaNoExiste", "Esa propuesta no existe.");
    public static readonly Error CargoNoExiste = Error.NotFound("Cargo.NoExiste", "Ese cargo no existe.");
    public static readonly Error PerfilSinBorrador = Error.Conflict("Cargo.SinBorrador", "El cargo no tiene un borrador de perfil para aprobar.");
    public static readonly Error PerfilYaAprobado = Error.Conflict("Cargo.PerfilAprobado", "El perfil ya está aprobado. Edítelo si necesita cambiarlo.");
    public static readonly Error CargoDuplicado = Error.Conflict("Cargo.Duplicado", "Ya existe un cargo con ese nombre.");
    public static readonly Error SedeNoExiste = Error.NotFound("Sede.NoExiste", "Esa sede no existe.");
    public static readonly Error AreaNoExiste = Error.NotFound("Area.NoExiste", "Esa área no existe.");
    public static readonly Error CentroCostoNoExiste = Error.NotFound("CentroCosto.NoExiste", "Ese centro de costo no existe.");
}

/// <summary>
/// Estructura de una empresa: sedes, áreas, centros de costo y cargos, más las importaciones de nómina que la alimentan.
/// Id = Id de la empresa. Nada se digita: todo llega de la nómina y la IA propone; una persona aprueba.
/// </summary>
public sealed class EstructuraEmpresa : AggregateRoot<Guid>
{
    private readonly List<Sede> _sedes = [];
    private readonly List<Area> _areas = [];
    private readonly List<CentroCosto> _centrosCosto = [];
    private readonly List<Cargo> _cargos = [];
    private readonly List<ImportacionNomina> _importaciones = [];

    private EstructuraEmpresa(Guid id) : base(id)
    {
    }

    public IReadOnlyList<Sede> Sedes => _sedes.AsReadOnly();
    public IReadOnlyList<Area> Areas => _areas.AsReadOnly();
    public IReadOnlyList<CentroCosto> CentrosCosto => _centrosCosto.AsReadOnly();
    public IReadOnlyList<Cargo> Cargos => _cargos.AsReadOnly();
    public IReadOnlyList<ImportacionNomina> Importaciones => _importaciones.AsReadOnly();

    public static EstructuraEmpresa Crear(Guid empresaId) => new(empresaId);

    /// <summary>Registra una lectura de nómina. Descarta cualquier importación anterior que no se haya aplicado.</summary>
    public Result<ImportacionNomina> RegistrarLectura(string fuente, IReadOnlyList<EmpleadoLeido> empleados, IEnumerable<string> erroresLectura, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(empleados);
        if (empleados.Count == 0)
        {
            var detalle = erroresLectura?.FirstOrDefault();
            return detalle is null ? EstructuraErrors.SinEmpleados : Error.Validation(EstructuraErrors.SinEmpleados.Code, detalle);
        }

        foreach (var abierta in _importaciones.Where(i => i.Abierta))
        {
            abierta.Descartar();
        }

        var importacion = ImportacionNomina.Leer(fuente, empleados, erroresLectura ?? [], ahora);
        _importaciones.Add(importacion);
        return importacion;
    }

    public Result AgregarUnionesIa(Guid importacionId, IEnumerable<(string NombreFinal, List<string> Codigos, string Porque)> uniones, string modelo)
    {
        var importacion = _importaciones.Find(i => i.Id == importacionId);
        if (importacion is null)
        {
            return Result.Failure(EstructuraErrors.ImportacionNoExiste);
        }

        if (importacion.Estado != EstadoImportacion.OrganizandoConIa)
        {
            return Result.Success();
        }

        importacion.AgregarUnionesIa(uniones, modelo);
        return Result.Success();
    }

    public Result RegistrarFallaIa(Guid importacionId, string mensaje)
    {
        var importacion = _importaciones.Find(i => i.Id == importacionId);
        if (importacion is null)
        {
            return Result.Failure(EstructuraErrors.ImportacionNoExiste);
        }

        if (importacion.Estado == EstadoImportacion.OrganizandoConIa)
        {
            importacion.RegistrarFallaIa(mensaje);
        }

        return Result.Success();
    }

    public Result CambiarInclusion(Guid importacionId, Guid propuestaId, bool incluida)
    {
        var importacion = _importaciones.Find(i => i.Id == importacionId);
        if (importacion is null)
        {
            return Result.Failure(EstructuraErrors.ImportacionNoExiste);
        }

        if (!importacion.Abierta)
        {
            return Result.Failure(EstructuraErrors.ImportacionCerrada);
        }

        var propuesta = importacion.BuscarPropuesta(propuestaId);
        if (propuesta is null)
        {
            return Result.Failure(EstructuraErrors.PropuestaNoExiste);
        }

        propuesta.CambiarInclusion(incluida);
        return Result.Success();
    }

    /// <summary>Aplica la importación con las propuestas incluidas. Devuelve los cargos que quedaron sin perfil.</summary>
    public Result<IReadOnlyList<Cargo>> AplicarImportacion(Guid importacionId, string usuario, DateTimeOffset ahora)
    {
        var importacion = _importaciones.Find(i => i.Id == importacionId);
        if (importacion is null)
        {
            return EstructuraErrors.ImportacionNoExiste;
        }

        if (importacion.Estado == EstadoImportacion.OrganizandoConIa)
        {
            return EstructuraErrors.IaTrabajando;
        }

        if (!importacion.Abierta)
        {
            return EstructuraErrors.ImportacionCerrada;
        }

        var comparar = StringComparer.OrdinalIgnoreCase;
        var incluidas = importacion.Propuestas.Where(p => p.Incluida).ToList();

        // Cargos: cada unión incluida forma un grupo; los demás cargos quedan solos.
        var grupos = incluidas.Where(p => p.Tipo == TipoPropuesta.UnirCargos)
            .Select(p => (Nombre: p.Valor, Cargos: importacion.Cargos.Where(c => p.Codigos.Contains(c.Codigo, comparar)).ToList()))
            .ToList();
        var unidos = grupos.SelectMany(g => g.Cargos.Select(c => c.Codigo)).ToHashSet(comparar);
        grupos.AddRange(importacion.Cargos.Where(c => !unidos.Contains(c.Codigo)).Select(c => (c.Nombre, new List<CargoEnLectura> { c })));

        var tocados = new HashSet<Guid>();
        foreach (var (nombre, cargos) in grupos)
        {
            var codigos = cargos.Select(c => c.Codigo).ToList();
            var cargo = _cargos.Find(c => c.TieneCodigo(codigos));
            if (cargo is null)
            {
                cargo = Cargo.Crear(nombre, cargos.OrderByDescending(c => c.Empleados).First().Area);
                _cargos.Add(cargo);
            }

            cargo.ActualizarDesdeNomina(nombre, cargos.OrderByDescending(c => c.Empleados).First().Area,
                cargos.Sum(c => c.Empleados), codigos, cargos.Select(c => c.Nombre));
            tocados.Add(cargo.Id);
        }

        foreach (var cargo in _cargos.Where(c => !tocados.Contains(c.Id)))
        {
            cargo.SinEmpleados();
        }

        // Sedes: lo leído más los empleados sin sede que la regla asignó.
        var sedes = importacion.Sedes.ToDictionary(s => s.Nombre, s => s.Empleados, comparar);
        foreach (var asignacion in incluidas.Where(p => p.Tipo == TipoPropuesta.AsignarSede))
        {
            sedes[asignacion.Valor] = sedes.GetValueOrDefault(asignacion.Valor) + asignacion.Cantidad;
        }

        Sincronizar(_sedes, sedes, Sede.Crear);
        Sincronizar(_areas, importacion.Areas.ToDictionary(a => a.Nombre, a => a.Empleados, comparar), Area.Crear);

        foreach (var leido in importacion.CentrosCosto)
        {
            var centro = _centrosCosto.Find(c => comparar.Equals(c.Codigo, leido.Codigo));
            if (centro is null)
            {
                centro = CentroCosto.Crear(leido.Codigo, leido.Nombre);
                _centrosCosto.Add(centro);
            }

            centro.ActualizarDesdeNomina(leido.Nombre, leido.Sede, leido.Empleados);
        }

        foreach (var centro in _centrosCosto.Where(c => importacion.CentrosCosto.All(l => !comparar.Equals(l.Codigo, c.Codigo))))
        {
            centro.ActualizarEmpleados(0);
        }

        importacion.MarcarAplicada(usuario, ahora);
        var sinPerfil = _cargos.Where(c => c.EstadoPerfil == EstadoPerfil.SinPerfil && c.Empleados > 0).ToList();
        sinPerfil.ForEach(c => c.MarcarPerfilSolicitado(ahora));
        return sinPerfil;
    }

    public Result RegistrarBorradorPerfil(Guid cargoId, PerfilCargo perfil, IEnumerable<RazonIa> razones, string modelo)
    {
        var cargo = _cargos.Find(c => c.Id == cargoId);
        if (cargo is null)
        {
            return Result.Failure(EstructuraErrors.CargoNoExiste);
        }

        // Un perfil ya aprobado por una persona no se reemplaza con un borrador de la IA.
        if (cargo.EstadoPerfil != EstadoPerfil.Aprobado)
        {
            cargo.RegistrarBorradorIa(perfil, razones, modelo);
        }

        return Result.Success();
    }

    public Result AprobarPerfil(Guid cargoId, string usuario, DateTimeOffset ahora)
    {
        var cargo = _cargos.Find(c => c.Id == cargoId);
        if (cargo is null)
        {
            return Result.Failure(EstructuraErrors.CargoNoExiste);
        }

        if (cargo.EstadoPerfil == EstadoPerfil.SinPerfil)
        {
            return Result.Failure(EstructuraErrors.PerfilSinBorrador);
        }

        cargo.AprobarPerfil(usuario, ahora);
        return Result.Success();
    }

    /// <summary>
    /// Crea a mano un cargo que todavía no está en la nómina (por ejemplo, para abrir una vacante nueva).
    /// Sin perfil, queda pendiente de que la IA lo redacte; con perfil escrito por una persona, queda aprobado por ella.
    /// </summary>
    public Result<Cargo> CrearCargo(string nombre, string area, PerfilCargo? perfil, string usuario, DateTimeOffset ahora)
    {
        if (ExisteCargo(nombre, excepto: null))
        {
            return EstructuraErrors.CargoDuplicado;
        }

        var cargo = Cargo.CrearManual(nombre, area);
        if (perfil is null)
        {
            cargo.MarcarPerfilSolicitado(ahora);
        }
        else
        {
            cargo.Editar(nombre, area, activo: true, perfil, usuario, ahora);
        }

        _cargos.Add(cargo);
        return cargo;
    }

    public Result EditarCargo(Guid cargoId, string nombre, string area, bool activo, PerfilCargo? perfil, string usuario, DateTimeOffset ahora)
    {
        var cargo = _cargos.Find(c => c.Id == cargoId);
        if (cargo is null)
        {
            return Result.Failure(EstructuraErrors.CargoNoExiste);
        }

        if (ExisteCargo(nombre, excepto: cargoId))
        {
            return Result.Failure(EstructuraErrors.CargoDuplicado);
        }

        cargo.Editar(nombre, area, activo, perfil, usuario, ahora);
        return Result.Success();
    }

    /// <summary>Pide otra vez el perfil a la IA (si falló o si se quiere un borrador nuevo). Un perfil aprobado no se reemplaza.</summary>
    public Result<Cargo> SolicitarPerfil(Guid cargoId, DateTimeOffset ahora)
    {
        var cargo = _cargos.Find(c => c.Id == cargoId);
        if (cargo is null)
        {
            return EstructuraErrors.CargoNoExiste;
        }

        if (cargo.EstadoPerfil == EstadoPerfil.Aprobado)
        {
            return EstructuraErrors.PerfilYaAprobado;
        }

        cargo.MarcarPerfilSolicitado(ahora);
        return cargo;
    }

    public Result EditarSede(Guid sedeId, string nombre, bool activo) =>
        EditarMaestro(_sedes, sedeId, EstructuraErrors.SedeNoExiste, m => m.Editar(nombre, activo));

    public Result EditarArea(Guid areaId, string nombre, bool activo) =>
        EditarMaestro(_areas, areaId, EstructuraErrors.AreaNoExiste, m => m.Editar(nombre, activo));

    public Result EditarCentroCosto(Guid centroId, string nombre, string sede, bool activo) =>
        EditarMaestro(_centrosCosto, centroId, EstructuraErrors.CentroCostoNoExiste, m => m.Editar(nombre, sede, activo));

    private bool ExisteCargo(string nombre, Guid? excepto) =>
        _cargos.Any(c => c.Id != excepto && string.Equals(c.Nombre.Trim(), nombre.Trim(), StringComparison.OrdinalIgnoreCase));

    private static Result EditarMaestro<T>(List<T> maestros, Guid id, Error noExiste, Action<T> editar)
        where T : Maestro
    {
        var maestro = maestros.Find(m => m.Id == id);
        if (maestro is null)
        {
            return Result.Failure(noExiste);
        }

        editar(maestro);
        return Result.Success();
    }

    private static void Sincronizar<T>(List<T> actuales, Dictionary<string, int> leidos, Func<string, T> crear)
        where T : Maestro
    {
        foreach (var (clave, empleados) in leidos)
        {
            var item = actuales.Find(a => string.Equals(a.NombreNomina, clave, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                item = crear(clave);
                actuales.Add(item);
            }

            item.ActualizarEmpleados(empleados);
        }

        foreach (var item in actuales.Where(a => !leidos.ContainsKey(a.NombreNomina)))
        {
            item.ActualizarEmpleados(0);
        }
    }
}
