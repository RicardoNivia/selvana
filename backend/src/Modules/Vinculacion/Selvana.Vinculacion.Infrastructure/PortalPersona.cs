using Microsoft.EntityFrameworkCore;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Contracts;
using Selvana.Vinculacion.Domain.Procesos;
using Selvana.Vinculacion.Infrastructure.Persistence;

namespace Selvana.Vinculacion.Infrastructure;

/// <summary>Lo que el portal de la persona ve de sus procesos de contratación (ADR 0013). Busca por cédula y correo a la vez.</summary>
internal sealed class ProcesosDeLaPersona(VinculacionDbContext db, ITokensEnlace tokens, TimeProvider reloj) : IProcesosDeLaPersona
{
    public async Task<IReadOnlyList<ProcesoDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken)
    {
        var procesos = await Buscar(documento, correo).AsNoTracking().ToListAsync(cancellationToken);
        return [.. procesos.OrderByDescending(p => p.ActualizadoEn).Select(p => new ProcesoDeLaPersona(
            p.Id, p.Vacante.PostulacionId, p.EmpresaUsuaria ?? p.Empresa, p.Vacante.Cargo is { Length: > 0 } cargo ? cargo : p.Vacante.Titulo, p.Etapa.ToString(),
            p.Etapa == EtapaVinculacion.Vinculado, p.Etapa == EtapaVinculacion.Cancelado, p.PendienteDelCandidato, p.ActualizadoEn))];
    }

    public async Task<string?> AbrirAsync(Guid procesoId, string documento, string correo, CancellationToken cancellationToken)
    {
        var proceso = await Buscar(documento, correo).FirstOrDefaultAsync(p => p.Id == procesoId, cancellationToken);
        if (proceso is null)
        {
            return null;
        }

        var (token, huella) = tokens.Generar();
        if (proceso.AbrirDesdePortal(huella, reloj.GetUtcNow()).IsFailure)
        {
            return null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    // El correo se compara sin mayúsculas (ILIKE) y escapado, para que % o _ no funcionen como comodines
    private IQueryable<ProcesoVinculacion> Buscar(string documento, string correo)
    {
        var patron = correo.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        return db.Set<ProcesoVinculacion>().Where(p => p.Persona.Documento == documento && EF.Functions.ILike(p.Persona.Correo, patron, "\\"));
    }
}
