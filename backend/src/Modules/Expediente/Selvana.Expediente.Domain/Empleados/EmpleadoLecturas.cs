using System.Globalization;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Domain.Empleados;

/// <summary>Actualización inteligente (E2): la IA lee un documento de la carpeta, las reglas proponen cambios y RRHH decide.</summary>
public sealed partial class Empleado
{
    private VersionDocumento? Version(Guid documentoId, Guid? archivoId = null)
    {
        var d = Documentos.FirstOrDefault(x => x.Id == documentoId);
        return archivoId is { } a ? d?.Versiones.FirstOrDefault(v => v.ArchivoId == a) : d?.Vigente;
    }

    /// <summary>Pide a la IA que lea la versión vigente del documento.</summary>
    public Result<VersionDocumento> PedirLectura(Guid documentoId, DateTimeOffset ahora)
    {
        if (Estado == EstadoEmpleado.Retirado)
        {
            return Result.Failure<VersionDocumento>(EmpleadoErrors.Retirado);
        }

        var v = Version(documentoId);
        if (v is null)
        {
            return Result.Failure<VersionDocumento>(EmpleadoErrors.DocumentoNoExiste);
        }

        if (v.Lectura?.Estado == EstadoLectura.Leyendo)
        {
            return Result.Failure<VersionDocumento>(ActualizacionErrors.Leyendo);
        }

        v.Lectura = new LecturaDocumento { Estado = EstadoLectura.Leyendo };
        ActualizadoEn = ahora;
        return v;
    }

    /// <summary>Llegó la lectura: las reglas proponen los cambios. Devuelve true si queda por revisar (cambios u observaciones).</summary>
    public bool RegistrarLectura(Guid documentoId, Guid archivoId, string tipoDetectado, bool legible, bool tipoCorrecto, IReadOnlyList<CampoLeido> campos,
        IEnumerable<string> observaciones, IEnumerable<RazonLectura> razones, string modelo, decimal costoUsd, DateTimeOffset ahora)
    {
        var d = Documentos.FirstOrDefault(x => x.Id == documentoId);
        var v = Version(documentoId, archivoId);
        if (d is null || v?.Lectura is not { Estado: EstadoLectura.Leyendo })
        {
            return false;
        }

        var (cambios, deReglas) = ReglasActualizacion.Proponer(this, d, tipoDetectado, campos);
        var todas = observaciones.Concat(deReglas).ToList();
        if (!legible)
        {
            todas.Insert(0, "La IA no pudo leer bien el documento: revíselo a mano.");
        }

        if (!tipoCorrecto)
        {
            todas.Insert(0, $"Parece ser «{tipoDetectado}», no «{d.Tipo}».");
        }

        v.Lectura = new LecturaDocumento
        {
            Estado = cambios.Count > 0 || todas.Count > 0 ? EstadoLectura.PorRevisar : EstadoLectura.SinCambios,
            TipoDetectado = tipoDetectado, Legible = legible, TipoCorrecto = tipoCorrecto, Campos = [.. campos], Observaciones = todas,
            Razones = [.. razones], Cambios = cambios, Modelo = modelo, CostoUsd = costoUsd,
        };
        ActualizadoEn = ahora;
        return v.Lectura.Estado == EstadoLectura.PorRevisar;
    }

    public bool FallaLectura(Guid documentoId, Guid archivoId, string error, DateTimeOffset ahora)
    {
        var v = Version(documentoId, archivoId);
        if (v?.Lectura is not { Estado: EstadoLectura.Leyendo })
        {
            return false;
        }

        v.Lectura.Estado = EstadoLectura.Fallida;
        v.Lectura.Error = error;
        ActualizadoEn = ahora;
        return true;
    }

    /// <summary>Decisión humana: aplica los cambios elegidos (con el valor que RRHH confirmó o corrigió) y deja la novedad.</summary>
    public Result AplicarLectura(Guid documentoId, IReadOnlyList<CambioAplicado> cambios, string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoEmpleado.Retirado)
        {
            return Result.Failure(EmpleadoErrors.Retirado);
        }

        var d = Documentos.FirstOrDefault(x => x.Id == documentoId);
        var v = d?.Vigente;
        if (d is null || v?.Lectura is not { Estado: EstadoLectura.PorRevisar })
        {
            return Result.Failure(ActualizacionErrors.SinLectura);
        }

        if (cambios.Any(c => !ReglasActualizacion.Claves.Contains(c.Clave)))
        {
            return Result.Failure(ActualizacionErrors.ClaveInvalida);
        }

        var hechos = new List<string>();
        var a = Afiliaciones;
        foreach (var c in cambios)
        {
            var valor = c.Valor.Trim();
            switch (c.Clave)
            {
                case "VenceEn":
                    DateOnly? vence = valor.Length == 0 ? null : ReglasActualizacion.Fecha(valor);
                    if (valor.Length > 0 && vence is null)
                    {
                        return Result.Failure(ActualizacionErrors.FechaInvalida);
                    }

                    hechos.Add($"Vencimiento de «{d.Nombre}»: {v.VenceEn?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—"} → {vence?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—"}");
                    v.VenceEn = vence;
                    break;
                case "Eps":
                    hechos.Add($"EPS: {Vacio(a.Eps)} → {Vacio(valor)}");
                    a.Eps = valor;
                    break;
                case "FondoPension":
                    hechos.Add($"Pensión: {Vacio(a.FondoPension)} → {Vacio(valor)}");
                    a.FondoPension = valor;
                    break;
                case "Arl":
                    hechos.Add($"ARL: {Vacio(a.Arl)} → {Vacio(valor)}");
                    a.Arl = valor;
                    break;
                case "CajaCompensacion":
                    hechos.Add($"Caja de compensación: {Vacio(a.CajaCompensacion)} → {Vacio(valor)}");
                    a.CajaCompensacion = valor;
                    break;
            }
        }

        // Reasignar para que EF detecte el cambio del JSON de afiliaciones
        Afiliaciones = new Afiliaciones { Eps = a.Eps, FondoPension = a.FondoPension, Arl = a.Arl, ClaseRiesgoArl = a.ClaseRiesgoArl, CajaCompensacion = a.CajaCompensacion };
        v.Lectura.Estado = EstadoLectura.Aplicada;
        v.Lectura.ResueltaPor = por;
        v.Lectura.ResueltaEn = ahora;
        if (hechos.Count > 0)
        {
            Novedades.Add(new Novedad
            {
                Id = Guid.CreateVersion7(), Tipo = TipoNovedad.ActualizacionDatos, FechaEfectiva = DateOnly.FromDateTime(ahora.UtcDateTime), Cambios = hechos,
                Motivo = $"Desde el documento «{d.Nombre}» (lectura de la IA revisada)", Por = por, En = ahora,
            });
        }

        Recalcular();
        ActualizadoEn = ahora;
        return Result.Success();
    }

    public Result DescartarLectura(Guid documentoId, string por, DateTimeOffset ahora)
    {
        var v = Version(documentoId);
        if (v?.Lectura is not { Estado: EstadoLectura.PorRevisar or EstadoLectura.Fallida })
        {
            return Result.Failure(ActualizacionErrors.SinLectura);
        }

        v.Lectura.Estado = EstadoLectura.Descartada;
        v.Lectura.ResueltaPor = por;
        v.Lectura.ResueltaEn = ahora;
        ActualizadoEn = ahora;
        return Result.Success();
    }
}
