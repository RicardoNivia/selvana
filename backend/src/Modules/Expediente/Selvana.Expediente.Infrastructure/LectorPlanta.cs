using Selvana.Expediente.Application.Empleados;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Integrations.Nomina;

namespace Selvana.Expediente.Infrastructure;

/// <summary>Adaptador: traduce la planta del contrato canónico de integraciones (ADR 0004) a las filas del expediente.</summary>
internal sealed class LectorPlanta(ArchivoPlantaCsv archivo) : Application.Empleados.ILectorPlanta
{
    public async Task<(IReadOnlyList<FilaPlanta> Filas, IReadOnlyList<string> Errores)> LeerAsync(byte[] contenido, CancellationToken cancellationToken)
    {
        using var flujo = new MemoryStream(contenido);
        var lectura = await archivo.LeerAsync(flujo, cancellationToken);
        return ([.. lectura.Empleados.Select(e => new FilaPlanta(e.Fila, e.Documento, e.Nombre, e.Cargo, e.Area, e.CentroCosto, e.Sede, e.TipoContrato, e.SalarioCop,
            e.FechaIngreso, e.FechaFinContrato, e.Correo, e.Celular, e.Ciudad,
            new Afiliaciones { Eps = e.Eps, FondoPension = e.FondoPension, Arl = e.Arl, ClaseRiesgoArl = e.ClaseRiesgoArl, CajaCompensacion = e.CajaCompensacion }))],
            lectura.Errores);
    }
}
