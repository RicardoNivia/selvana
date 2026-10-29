using System.Globalization;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Domain.Empleados;

/// <summary>Datos que el empleado propuso cambiar desde su portal; RRHH los aplica o los descarta.</summary>
public sealed class CambioPropuestoEmpleado
{
    public string Celular { get; init; } = "";
    public string Ciudad { get; init; } = "";
    public string Eps { get; init; } = "";
    public string FondoPension { get; init; } = "";
    public string CajaCompensacion { get; init; } = "";
    public List<string> Cambios { get; init; } = [];
    public DateTimeOffset En { get; init; }
}

public static class AutoservicioErrors
{
    public static readonly Error SinCambios = Error.Validation("Expediente.SinCambiosPropuestos", "No cambiaste ningún dato. Si todo sigue igual, confírmalo.");
    public static readonly Error SinPropuesta = Error.NotFound("Expediente.SinPropuesta", "El empleado no tiene cambios propuestos por revisar.");
    public static readonly Error SinCampana = Error.Conflict("Expediente.SinCampana", "No hay una actualización de datos pendiente.");
}

/// <summary>P5 · Autoservicio del empleado desde su portal y campaña de actualización de datos.</summary>
public sealed partial class Empleado
{
    /// <summary>RRHH pidió que confirme o actualice sus datos (campaña); se limpia cuando responde.</summary>
    public DateTimeOffset? ActualizacionPedidaEn { get; private set; }
    public DateTimeOffset? DatosConfirmadosEn { get; private set; }
    public CambioPropuestoEmpleado? CambioPropuesto { get; private set; }

    /// <summary>Campaña de actualización: el empleado la verá como pendiente en su portal.</summary>
    public bool PedirActualizacion(DateTimeOffset ahora)
    {
        if (Estado == EstadoEmpleado.Retirado || ActualizacionPedidaEn is not null)
        {
            return false;
        }

        ActualizacionPedidaEn = ahora;
        ActualizadoEn = ahora;
        return true;
    }

    /// <summary>El empleado propone cambios: se comparan con lo actual y quedan esperando la decisión de RRHH.</summary>
    public Result<CambioPropuestoEmpleado> ProponerDatos(string celular, string ciudad, string eps, string fondoPension, string cajaCompensacion, DateTimeOffset ahora)
    {
        if (Estado == EstadoEmpleado.Retirado)
        {
            return EmpleadoErrors.Retirado;
        }

        var cambios = new List<string>();
        void Comparar(string campo, string antes, string despues)
        {
            if (!string.Equals((antes ?? "").Trim(), (despues ?? "").Trim(), StringComparison.Ordinal))
            {
                cambios.Add($"{campo}: {Vacio(antes ?? "")} → {Vacio(despues ?? "")}");
            }
        }

        Comparar("Celular", Celular, celular);
        Comparar("Ciudad", Ciudad, ciudad);
        Comparar("EPS", Afiliaciones.Eps, eps);
        Comparar("Pensión", Afiliaciones.FondoPension, fondoPension);
        Comparar("Caja de compensación", Afiliaciones.CajaCompensacion, cajaCompensacion);
        if (cambios.Count == 0)
        {
            return AutoservicioErrors.SinCambios;
        }

        CambioPropuesto = new CambioPropuestoEmpleado
        {
            Celular = (celular ?? "").Trim(), Ciudad = (ciudad ?? "").Trim(), Eps = (eps ?? "").Trim(), FondoPension = (fondoPension ?? "").Trim(),
            CajaCompensacion = (cajaCompensacion ?? "").Trim(), Cambios = cambios, En = ahora,
        };
        Responder(ahora);
        return CambioPropuesto;
    }

    /// <summary>Responde la campaña sin cambios: sus datos siguen iguales.</summary>
    public Result ConfirmarDatos(DateTimeOffset ahora)
    {
        if (ActualizacionPedidaEn is null)
        {
            return Result.Failure(AutoservicioErrors.SinCampana);
        }

        Responder(ahora);
        Novedades.Add(new Novedad
        {
            Id = Guid.CreateVersion7(), Tipo = TipoNovedad.ActualizacionDatos, FechaEfectiva = DateOnly.FromDateTime(ahora.UtcDateTime),
            Cambios = ["Confirmó que sus datos siguen iguales"], Motivo = "Campaña de actualización de datos", Por = Nombre, En = ahora,
        });
        return Result.Success();
    }

    /// <summary>Decisión de RRHH: aplica lo que propuso el empleado (queda como novedad con antes y después).</summary>
    public Result AplicarCambioPropuesto(string por, DateTimeOffset ahora)
    {
        if (CambioPropuesto is not { } p)
        {
            return Result.Failure(AutoservicioErrors.SinPropuesta);
        }

        var afiliaciones = new Afiliaciones
        {
            Eps = p.Eps, FondoPension = p.FondoPension, Arl = Afiliaciones.Arl, ClaseRiesgoArl = Afiliaciones.ClaseRiesgoArl, CajaCompensacion = p.CajaCompensacion,
        };
        var r = ActualizarDatos(Nombre, Correo, p.Celular, p.Ciudad, afiliaciones, por, ahora);
        if (r.IsFailure && r.Error != EmpleadoErrors.SinCambios)
        {
            return r;
        }

        if (r.IsSuccess)
        {
            Novedades[^1] = new Novedad
            {
                Id = Novedades[^1].Id, Tipo = TipoNovedad.ActualizacionDatos, FechaEfectiva = Novedades[^1].FechaEfectiva, Cambios = Novedades[^1].Cambios,
                Motivo = "Lo propuso el empleado desde su portal", Por = por, En = ahora,
            };
        }

        CambioPropuesto = null;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    public Result DescartarCambioPropuesto(DateTimeOffset ahora)
    {
        if (CambioPropuesto is null)
        {
            return Result.Failure(AutoservicioErrors.SinPropuesta);
        }

        CambioPropuesto = null;
        ActualizadoEn = ahora;
        return Result.Success();
    }

    /// <summary>Avisos para el propio empleado: documentos suyos vencidos o por vencer y el fin de su contrato.</summary>
    public IReadOnlyList<string> AvisosParaElEmpleado(DateOnly hoy)
    {
        var avisos = new List<string>();
        if (Estado == EstadoEmpleado.Retirado)
        {
            return avisos;
        }

        foreach (var d in Documentos.Where(d => d.Vigente?.VenceEn is not null))
        {
            var vence = d.Vigente!.VenceEn!.Value;
            if (vence < hoy)
            {
                avisos.Add($"«{d.Nombre}» venció el {vence.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}. Sube uno vigente.");
            }
            else if (vence <= hoy.AddDays(DiasAvisoVencimiento))
            {
                avisos.Add($"«{d.Nombre}» vence el {vence.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}. Ten listo el nuevo.");
            }
        }

        if (FechaFinContrato is { } fin && fin >= hoy && fin <= hoy.AddDays(45))
        {
            avisos.Add($"Tu contrato actual termina el {fin.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.");
        }

        return avisos;
    }

    private void Responder(DateTimeOffset ahora)
    {
        ActualizacionPedidaEn = null;
        DatosConfirmadosEn = ahora;
        ActualizadoEn = ahora;
    }
}
