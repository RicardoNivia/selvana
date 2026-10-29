using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Portal;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Portal;

namespace Selvana.Reclutamiento.Infrastructure.Portal;

/// <summary>
/// La hoja de vida de un empleado para su expediente: primero la de su postulación en esa misma empresa (ya autorizada para ella);
/// si no hay, el perfil del portal, solo si la persona lo compartió con esa empresa.
/// </summary>
internal sealed class HojasDeVidaParaEmpresa(ICandidatos candidatos, IVacantes vacantes, ICuentasCandidato cuentas, IAlmacenHojasDeVida almacen)
    : IHojasDeVidaParaEmpresa
{
    public async Task<HojaDeVidaParaEmpresa?> ObtenerAsync(Guid empresaId, string documento, string correo, CancellationToken cancellationToken)
    {
        var candidato = await candidatos.ObtenerPorDocumentoAsync(empresaId, Candidato.NormalizarDocumento(documento), cancellationToken);
        if (candidato is { Suprimido: false } && candidato.Postulaciones.OrderByDescending(p => p.RecibidaEn).FirstOrDefault() is { } ultima)
        {
            var vacante = await vacantes.ObtenerAsync(ultima.VacanteId, cancellationToken);
            return Hoja("Postulación", candidato.Datos, ultima.Id, vacante?.Titulo, null, ultima.RecibidaEn);
        }

        var cuenta = await CompartidaAsync(empresaId, documento, correo, cancellationToken);
        return cuenta is null ? null
            : Hoja("Portal", cuenta.Datos, null, null, cuenta.ArchivoHojaDeVidaId is null ? null : cuenta.NombreArchivoHojaDeVida, cuenta.HojaDeVidaActualizadaEn);
    }

    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> AbrirCompartidaAsync(Guid empresaId, string documento, string correo,
        CancellationToken cancellationToken) =>
        await CompartidaAsync(empresaId, documento, correo, cancellationToken) is { ArchivoHojaDeVidaId: { } archivo }
            ? await almacen.LeerAsync(archivo, cancellationToken)
            : null;

    /// <summary>La cuenta del portal de esa persona (correo y cédula a la vez), solo si compartió su hoja de vida con la empresa.</summary>
    private async Task<CuentaCandidato?> CompartidaAsync(Guid empresaId, string documento, string correo, CancellationToken cancellationToken)
    {
        var cuenta = await cuentas.ObtenerPorCorreoAsync(CuentaCandidato.NormalizarCorreo(correo), cancellationToken);
        return cuenta is not null && cuenta.Documento == Candidato.NormalizarDocumento(documento) && cuenta.HojaDeVidaCompartidaCon.Contains(empresaId) ? cuenta : null;
    }

    private static HojaDeVidaParaEmpresa Hoja(string fuente, DatosCandidato d, Guid? postulacionId, string? vacante, string? archivo, DateTimeOffset? fecha) =>
        new(fuente, d.Nombre, d.Ciudad, string.IsNullOrWhiteSpace(d.LinkedIn) ? null : d.LinkedIn,
            [.. d.Experiencia.Select(x => new ExperienciaHv(x.Cargo, x.Empresa, x.Desde, x.Hasta))],
            [.. d.Formacion.Select(x => new EstudioHv(x.Titulo, x.Institucion, x.Anio))], d.Cursos, d.Habilidades, postulacionId, vacante, archivo, fecha);
}
