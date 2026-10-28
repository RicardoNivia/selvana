using Selvana.Core.Contracts.Auditoria;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Domain.Empleados;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Application.Empleados;

/// <summary>Una fila del archivo de planta ya validada.</summary>
public sealed record FilaPlanta(
    int Fila, string Documento, string Nombre, string Cargo, string Area, string CentroCosto, string Sede, string TipoContrato, decimal SalarioCop,
    DateOnly FechaIngreso, DateOnly? FechaFinContrato, string Correo, string Celular, string Ciudad, Afiliaciones Afiliaciones);

/// <summary>Lee el archivo de planta (CSV) con el contrato canónico de integraciones.</summary>
public interface ILectorPlanta
{
    Task<(IReadOnlyList<FilaPlanta> Filas, IReadOnlyList<string> Errores)> LeerAsync(byte[] contenido, CancellationToken cancellationToken);
}

public sealed record FilaPlantaDto(int Fila, string Documento, string Nombre, string Cargo, string Sede, string TipoContrato, decimal SalarioCop, DateOnly FechaIngreso,
    string Resultado);

public sealed record PlantaPrevisualizadaDto(int Leidas, int PorCrear, int YaExisten, int Repetidas, IReadOnlyList<string> Errores, IReadOnlyList<FilaPlantaDto> Muestra);

public sealed record PlantaCargadaDto(int Creados, int Omitidos, int ConErrores);

/// <summary>Revisa el archivo de planta sin guardar nada: qué se crea, qué ya existe y qué filas tienen errores.</summary>
public sealed record PrevisualizarPlantaQuery(byte[] Contenido) : IQuery<PlantaPrevisualizadaDto>;

/// <summary>Decisión humana después de ver la previsualización: crea los empleados nuevos (los que ya existen no se tocan).</summary>
public sealed record CargarPlantaCommand(byte[] Contenido) : ICommand<PlantaCargadaDto>;

internal static class Planta
{
    public const int Muestra = 20;
    public const int TamanoMaximo = 5 * 1024 * 1024;

    public static readonly Error Vacio = Error.Validation("Expediente.PlantaVacia", "El archivo está vacío. Use la plantilla y vuelva a subirlo.");
    public static readonly Error Grande = Error.Validation("Expediente.PlantaGrande", "El archivo pesa más de 5 MB: divida la planta en varios archivos.");

    /// <summary>Clasifica las filas: nuevas, ya existentes (activo con ese documento) y repetidas dentro del mismo archivo.</summary>
    public static async Task<(List<FilaPlanta> Nuevas, int Existentes, int Repetidas, List<FilaPlantaDto> Muestra)> ClasificarAsync(
        IReadOnlyList<FilaPlanta> filas, Guid empresaId, IEmpleados empleados, CancellationToken ct)
    {
        var activos = await empleados.DocumentosActivosAsync(empresaId, [.. filas.Select(f => f.Documento).Distinct()], ct);
        var vistos = new HashSet<string>();
        var nuevas = new List<FilaPlanta>();
        var muestra = new List<FilaPlantaDto>();
        int existentes = 0, repetidas = 0;
        foreach (var f in filas)
        {
            string resultado;
            if (activos.Contains(f.Documento))
            {
                existentes++;
                resultado = "Ya existe";
            }
            else if (!vistos.Add(f.Documento))
            {
                repetidas++;
                resultado = "Repetida en el archivo";
            }
            else
            {
                nuevas.Add(f);
                resultado = "Se crea";
            }

            if (muestra.Count < Muestra)
            {
                muestra.Add(new FilaPlantaDto(f.Fila, f.Documento, f.Nombre, f.Cargo, f.Sede, f.TipoContrato, f.SalarioCop, f.FechaIngreso, resultado));
            }
        }

        return (nuevas, existentes, repetidas, muestra);
    }

    public static Error? Validar(byte[] contenido) => contenido.Length == 0 ? Vacio : contenido.Length > TamanoMaximo ? Grande : null;
}

internal sealed class PrevisualizarPlantaHandler(IUsuarioActual usuario, ILectorPlanta lector, IEmpleados empleados)
    : IQueryHandler<PrevisualizarPlantaQuery, PlantaPrevisualizadaDto>
{
    public async Task<Result<PlantaPrevisualizadaDto>> Handle(PrevisualizarPlantaQuery q, CancellationToken ct)
    {
        if (Planta.Validar(q.Contenido) is { } error)
        {
            return error;
        }

        var (filas, errores) = await lector.LeerAsync(q.Contenido, ct);
        var (nuevas, existentes, repetidas, muestra) = await Planta.ClasificarAsync(filas, usuario.EmpresaId, empleados, ct);
        return new PlantaPrevisualizadaDto(filas.Count, nuevas.Count, existentes, repetidas, errores, muestra);
    }
}

internal sealed class CargarPlantaHandler(IUsuarioActual usuario, ILectorPlanta lector, IEmpleados empleados, IBitacora bitacora, IUnidadExpediente unidad,
    TimeProvider reloj) : ICommandHandler<CargarPlantaCommand, PlantaCargadaDto>
{
    public async Task<Result<PlantaCargadaDto>> Handle(CargarPlantaCommand c, CancellationToken cancellationToken)
    {
        if (Planta.Validar(c.Contenido) is { } error)
        {
            return error;
        }

        var (filas, errores) = await lector.LeerAsync(c.Contenido, cancellationToken);
        var (nuevas, existentes, repetidas, _) = await Planta.ClasificarAsync(filas, usuario.EmpresaId, empleados, cancellationToken);
        var ahora = reloj.GetUtcNow();
        foreach (var f in nuevas)
        {
            empleados.Agregar(Empleado.Crear(usuario.EmpresaId, "Archivo de planta", f.Documento, f.Nombre, f.Correo, f.Celular, f.Ciudad,
                new SituacionLaboral(f.Cargo, f.Area, f.CentroCosto, f.Sede, null, null, f.TipoContrato, f.SalarioCop, f.FechaFinContrato), f.FechaIngreso,
                f.Afiliaciones, usuario.Nombre, ahora));
        }

        bitacora.Registrar(usuario.EmpresaId, TipoActor.Persona, usuario.Nombre, $"Cargó la planta: {nuevas.Count} empleados nuevos",
            $"{existentes} ya existían · {repetidas} repetidas · {errores.Count} filas con errores", null, "Empleado", null);
        await unidad.SaveChangesAsync(cancellationToken);
        return new PlantaCargadaDto(nuevas.Count, existentes + repetidas, errores.Count);
    }
}
