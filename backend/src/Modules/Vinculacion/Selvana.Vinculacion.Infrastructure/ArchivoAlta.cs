using Selvana.Integrations.Nomina;
using Selvana.Vinculacion.Application.Alta;
using Selvana.Vinculacion.Application.Documentos;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Infrastructure;

/// <summary>Traduce el proceso al contrato canónico de nómina (ADR 0004) y lo entrega como archivo CSV para cualquier ERP.</summary>
internal sealed class ArchivoAltaCsv : IArchivoAlta
{
    public ArchivoDto Generar(ProcesoVinculacion p, bool auxilioTransporte)
    {
        ArgumentNullException.ThrowIfNull(p);
        string Entidad(TipoAfiliacion t) => p.Afiliaciones.FirstOrDefault(a => a.Tipo == t) is { Estado: EstadoAfiliacion.Confirmada } a ? a.Entidad : "";
        var alta = new AltaEmpleadoNomina(p.Persona.Documento, p.Persona.Nombre, p.Persona.Correo, p.Persona.Celular, p.Persona.Ciudad, p.Vacante.Cargo, p.Alta.Area,
            p.Alta.CentroCosto, p.Alta.Sede, ReglasContrato.Nombre(p.Condiciones.Tipo), p.Condiciones.SalarioCop, auxilioTransporte, p.Condiciones.FechaIngreso,
            p.Condiciones.FechaFin, p.Condiciones.PeriodoPruebaDias, Entidad(TipoAfiliacion.Eps), Entidad(TipoAfiliacion.Pension), Entidad(TipoAfiliacion.Arl),
            p.Afiliaciones.FirstOrDefault(a => a.Tipo == TipoAfiliacion.Arl)?.ClaseRiesgo, Entidad(TipoAfiliacion.CajaCompensacion), p.Alta.Banco, p.Alta.TipoCuenta,
            p.Alta.NumeroCuenta, p.EmpresaUsuaria);
        return new ArchivoDto($"alta-{p.Codigo}.csv", "text/csv; charset=utf-8", ArchivoCsvAltas.Generar([alta]));
    }
}
