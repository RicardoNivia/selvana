using Selvana.SharedKernel.Domain;

namespace Selvana.Core.Domain.Estructura;

/// <summary>
/// Dato maestro que llega de la nómina y se puede editar en Selvana.
/// <see cref="NombreNomina"/> es la llave con la que se reconoce en cada sincronización; <see cref="Nombre"/> es el que se muestra.
/// Si una persona lo editó, la sincronización solo actualiza el número de empleados.
/// </summary>
public abstract class Maestro : Entity<Guid>
{
    protected Maestro(Guid id, string nombre) : base(id)
    {
        Nombre = nombre;
        NombreNomina = nombre;
    }

    public string Nombre { get; protected set; }
    public string NombreNomina { get; protected set; }
    public int Empleados { get; protected set; }
    public bool Activo { get; protected set; } = true;
    public bool EditadoManualmente { get; protected set; }

    internal void ActualizarEmpleados(int empleados) => Empleados = empleados;

    internal void Editar(string nombre, bool activo)
    {
        Nombre = nombre.Trim();
        Activo = activo;
        EditadoManualmente = true;
    }
}

public sealed class Sede : Maestro
{
    private Sede(Guid id, string nombre) : base(id, nombre)
    {
    }

    internal static Sede Crear(string nombre) => new(Guid.CreateVersion7(), nombre);
}

public sealed class Area : Maestro
{
    private Area(Guid id, string nombre) : base(id, nombre)
    {
    }

    internal static Area Crear(string nombre) => new(Guid.CreateVersion7(), nombre);
}

public sealed class CentroCosto : Maestro
{
    private CentroCosto(Guid id, string codigo, string nombre) : base(id, nombre) => Codigo = codigo;

    public string Codigo { get; private set; }
    public string Sede { get; private set; } = "";

    internal static CentroCosto Crear(string codigo, string nombre) => new(Guid.CreateVersion7(), codigo, nombre);

    internal void ActualizarDesdeNomina(string nombre, string sede, int empleados)
    {
        Empleados = empleados;
        NombreNomina = nombre;
        if (!EditadoManualmente)
        {
            Nombre = nombre;
            Sede = sede;
        }
    }

    internal void Editar(string nombre, string sede, bool activo)
    {
        Editar(nombre, activo);
        Sede = sede.Trim();
    }
}
