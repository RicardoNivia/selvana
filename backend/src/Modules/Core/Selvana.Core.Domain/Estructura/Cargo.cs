using Selvana.SharedKernel.Domain;

namespace Selvana.Core.Domain.Estructura;

public enum EstadoPerfil
{
    SinPerfil,
    BorradorIa,
    Aprobado,
}

/// <summary>Perfil del cargo. Si lo redactó la IA, queda en violeta hasta que RRHH lo apruebe.</summary>
public sealed class PerfilCargo
{
    public string Mision { get; init; } = "";
    public List<string> Funciones { get; init; } = [];
    public List<string> Requisitos { get; init; } = [];
    public string RiesgoArl { get; init; } = "";
}

/// <summary>Una razón de por qué la IA redactó el perfil así, con su fuente.</summary>
public sealed class RazonIa
{
    public string Signo { get; init; } = "+";
    public string Texto { get; init; } = "";
    public string Fuente { get; init; } = "";
}

public sealed class Cargo : Entity<Guid>
{
    private Cargo(Guid id, string nombre, string area) : base(id)
    {
        Nombre = nombre;
        Area = area;
    }

    public string Nombre { get; private set; }
    public string Area { get; private set; }
    public int Empleados { get; private set; }

    /// <summary>Códigos con los que aparece en la nómina; más de uno si se unieron nombres repetidos.</summary>
    public List<string> CodigosNomina { get; private set; } = [];

    /// <summary>Nombres con los que aparece en la nómina, para mostrar la evidencia de una unión.</summary>
    public List<string> NombresNomina { get; private set; } = [];

    public EstadoPerfil EstadoPerfil { get; private set; } = EstadoPerfil.SinPerfil;
    public PerfilCargo? Perfil { get; private set; }
    public List<RazonIa> Razones { get; private set; } = [];
    public string? ModeloIa { get; private set; }
    public string? AprobadoPor { get; private set; }
    public DateTimeOffset? AprobadoEn { get; private set; }
    public bool Activo { get; private set; } = true;

    /// <summary>Si una persona editó el nombre o el área, la sincronización con la nómina ya no los cambia.</summary>
    public bool EditadoManualmente { get; private set; }

    /// <summary>Cuándo se pidió el perfil a la IA; sirve para saber si sigue en redacción o hay que pedirlo otra vez.</summary>
    public DateTimeOffset? PerfilSolicitadoEn { get; private set; }

    internal static Cargo Crear(string nombre, string area) => new(Guid.CreateVersion7(), nombre, area);

    internal static Cargo CrearManual(string nombre, string area) => new(Guid.CreateVersion7(), nombre.Trim(), area.Trim()) { EditadoManualmente = true };

    internal bool TieneCodigo(IEnumerable<string> codigos) => CodigosNomina.Intersect(codigos, StringComparer.OrdinalIgnoreCase).Any();

    internal void ActualizarDesdeNomina(string nombre, string area, int empleados, IEnumerable<string> codigos, IEnumerable<string> nombres)
    {
        if (!EditadoManualmente)
        {
            Nombre = nombre;
            Area = area;
        }

        Empleados = empleados;
        CodigosNomina = [.. CodigosNomina.Union(codigos, StringComparer.OrdinalIgnoreCase)];
        NombresNomina = [.. NombresNomina.Union(nombres, StringComparer.OrdinalIgnoreCase)];
    }

    internal void SinEmpleados() => Empleados = 0;

    internal void RegistrarBorradorIa(PerfilCargo perfil, IEnumerable<RazonIa> razones, string modelo)
    {
        Perfil = perfil;
        Razones = [.. razones];
        ModeloIa = modelo;
        EstadoPerfil = EstadoPerfil.BorradorIa;
        AprobadoPor = null;
        AprobadoEn = null;
    }

    internal void MarcarPerfilSolicitado(DateTimeOffset ahora) => PerfilSolicitadoEn = ahora;

    /// <summary>Edición de una persona. Si cambia el perfil, queda aprobado por ella: editar lo que propuso la IA lo confirma.</summary>
    internal void Editar(string nombre, string area, bool activo, PerfilCargo? perfil, string usuario, DateTimeOffset ahora)
    {
        Nombre = nombre.Trim();
        Area = area.Trim();
        Activo = activo;
        EditadoManualmente = true;
        if (perfil is not null)
        {
            Perfil = perfil;
            AprobarPerfil(usuario, ahora);
        }
    }

    internal void AprobarPerfil(string aprobadoPor, DateTimeOffset ahora)
    {
        EstadoPerfil = EstadoPerfil.Aprobado;
        AprobadoPor = aprobadoPor;
        AprobadoEn = ahora;
    }
}
