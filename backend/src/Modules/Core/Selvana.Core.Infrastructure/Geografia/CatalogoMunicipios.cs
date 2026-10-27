using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Core.Application.Geografia;
using Selvana.Core.Infrastructure.Persistence;

namespace Selvana.Core.Infrastructure.Geografia;

/// <summary>Fila de la tabla core.municipios (DIVIPOLA del DANE). Es referencia, no un agregado de negocio.</summary>
internal sealed class Municipio
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string CodigoDepartamento { get; set; } = "";
    public string Departamento { get; set; } = "";

    /// <summary>Subregión o provincia del departamento (Sabana Occidente, Urabá…); null si no se conoce.</summary>
    public string? Subregion { get; set; }

    /// <summary>Municipio y departamento en minúsculas y sin tildes, para buscar como escribe la gente.</summary>
    public string Busqueda { get; set; } = "";
}

internal sealed class MunicipioConfiguration : IEntityTypeConfiguration<Municipio>
{
    public void Configure(EntityTypeBuilder<Municipio> builder)
    {
        builder.ToTable("municipios");
        builder.HasKey(m => m.Codigo);
        builder.Property(m => m.Codigo).HasMaxLength(5);
        builder.Property(m => m.Nombre).HasMaxLength(120);
        builder.Property(m => m.CodigoDepartamento).HasMaxLength(2);
        builder.Property(m => m.Departamento).HasMaxLength(120);
        builder.Property(m => m.Subregion).HasMaxLength(120);
        builder.Property(m => m.Busqueda).HasMaxLength(400);
        builder.HasIndex(m => m.Busqueda);
    }
}

internal sealed class CatalogoMunicipios(CoreDbContext db) : ICatalogoMunicipios
{
    public async Task<IReadOnlyList<MunicipioDto>> BuscarAsync(string? texto, int tamano, CancellationToken cancellationToken)
    {
        var municipios = db.Set<Municipio>().AsNoTracking();
        var t = SemillaMunicipios.Normalizar(texto ?? "");

        // Sin texto, las capitales grandes primero; con texto, primero los que empiezan por lo escrito («fun» → Funza antes que Guadalupe de Funes)
        var consulta = t.Length == 0
            ? municipios.OrderBy(m => m.Codigo.EndsWith("001") ? 0 : 1).ThenBy(m => m.Nombre)
            : t.All(char.IsDigit)
                ? municipios.Where(m => m.Codigo.StartsWith(t)).OrderBy(m => m.Codigo)
                : municipios.Where(m => EF.Functions.Like(m.Busqueda, $"%{t}%")).OrderBy(m => m.Busqueda.StartsWith(t) ? 0 : 1).ThenBy(m => m.Nombre);

        return await consulta.Take(tamano)
            .Select(m => new MunicipioDto(m.Codigo, m.Nombre, m.CodigoDepartamento, m.Departamento, m.Subregion))
            .ToListAsync(cancellationToken);
    }

    public Task<MunicipioDto?> ObtenerAsync(string codigo, CancellationToken cancellationToken) =>
        db.Set<Municipio>().AsNoTracking().Where(m => m.Codigo == codigo)
            .Select(m => new MunicipioDto(m.Codigo, m.Nombre, m.CodigoDepartamento, m.Departamento, m.Subregion))
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Carga la DIVIPOLA (datos.gov.co, conjunto gdxc-w37w, 1.122 municipios) con la subregión (provincia) de cada municipio,
/// tomada del documento de subregiones de RRHH; los 5 municipios que ese documento no trae quedan sin subregión.
/// Es idempotente: inserta lo que falte y corrige lo que cambió, así que corre en cada arranque.
/// </summary>
internal static class SemillaMunicipios
{
    private sealed record Fila(string Codigo, string Municipio, string CodigoDepartamento, string Departamento, string? Subregion);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task SembrarAsync(CoreDbContext db, CancellationToken cancellationToken = default)
    {
        await using var recurso = typeof(SemillaMunicipios).Assembly.GetManifestResourceStream("Selvana.Core.Infrastructure.Geografia.municipios.json")
            ?? throw new InvalidOperationException("Falta el recurso municipios.json (DIVIPOLA).");
        var filas = await JsonSerializer.DeserializeAsync<List<Fila>>(recurso, Json, cancellationToken) ?? [];

        var existentes = await db.Set<Municipio>().ToDictionaryAsync(m => m.Codigo, cancellationToken);
        foreach (var f in filas)
        {
            if (!existentes.TryGetValue(f.Codigo, out var m))
            {
                m = new Municipio { Codigo = f.Codigo };
                db.Add(m);
            }

            m.Nombre = f.Municipio;
            m.CodigoDepartamento = f.CodigoDepartamento;
            m.Departamento = f.Departamento;
            m.Subregion = f.Subregion;
            m.Busqueda = Normalizar($"{f.Municipio} {f.Departamento} {f.Subregion}");
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static string Normalizar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
