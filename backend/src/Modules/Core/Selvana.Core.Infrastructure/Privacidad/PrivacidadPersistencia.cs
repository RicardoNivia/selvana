using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Privacidad;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Domain.Auditoria;
using Selvana.Core.Domain.Privacidad;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.SharedKernel.Results;

namespace Selvana.Core.Infrastructure.Privacidad;

internal sealed class EntradaBitacoraConfiguration : IEntityTypeConfiguration<EntradaBitacora>
{
    public void Configure(EntityTypeBuilder<EntradaBitacora> builder)
    {
        builder.ToTable("bitacora");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.TipoActor).HasMaxLength(20);
        builder.Property(e => e.Actor).HasMaxLength(200);
        builder.Property(e => e.Accion).HasMaxLength(300);
        builder.Property(e => e.Detalle).HasMaxLength(1000);
        builder.Property(e => e.Nota).HasMaxLength(500);
        builder.Property(e => e.Modulo).HasMaxLength(40);
        builder.Property(e => e.Entidad).HasMaxLength(60);
        builder.Property(e => e.HuellaAnterior).HasMaxLength(64);
        builder.Property(e => e.Huella).HasMaxLength(64);
        builder.HasIndex(e => new { e.EmpresaId, e.Fecha });
        // Cada huella solo puede tener una sucesora: si dos escrituras compiten, una falla y se reintenta (la cadena no se bifurca)
        builder.HasIndex(e => new { e.EmpresaId, e.HuellaAnterior }).IsUnique();
        builder.HasIndex(e => new { e.EmpresaId, e.Huella }).IsUnique();
    }
}

internal sealed class SolicitudTitularConfiguration : IEntityTypeConfiguration<SolicitudTitular>
{
    public void Configure(EntityTypeBuilder<SolicitudTitular> builder)
    {
        builder.ToTable("solicitudes_titular");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.EsSupresion);
        builder.Property(s => s.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Titular).HasMaxLength(200);
        builder.Property(s => s.Documento).HasMaxLength(20);
        builder.Property(s => s.Contacto).HasMaxLength(200);
        builder.Property(s => s.Canal).HasMaxLength(60);
        builder.Property(s => s.Descripcion).HasMaxLength(2000);
        builder.Property(s => s.RegistradaPor).HasMaxLength(200);
        builder.Property(s => s.BorradorRespuesta).HasMaxLength(4000);
        builder.Property(s => s.Respuesta).HasMaxLength(4000);
        builder.Property(s => s.RespondidaPor).HasMaxLength(200);
        builder.HasIndex(s => new { s.EmpresaId, s.Estado, s.Vence });
    }
}

/// <summary>Encadena cada línea con la última de la empresa. Recuerda la última del alcance para varias líneas en un mismo guardado.</summary>
internal sealed class BitacoraCore(CoreDbContext db, TimeProvider reloj) : IBitacora
{
    private readonly Dictionary<Guid, string> _ultima = [];

    public async Task RegistrarAsync(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false, string modulo = "Core", DateTimeOffset? fecha = null, Guid? id = null, CancellationToken cancellationToken = default)
    {
        if (!_ultima.TryGetValue(empresaId, out var anterior))
        {
            var bitacora = db.Set<EntradaBitacora>();
            // La última es la única cuya huella nadie toma como anterior
            anterior = await bitacora.AsNoTracking()
                .Where(e => e.EmpresaId == empresaId && !bitacora.Any(o => o.EmpresaId == empresaId && o.HuellaAnterior == e.Huella))
                .Select(e => e.Huella).FirstOrDefaultAsync(cancellationToken) ?? "";
        }

        var entrada = EntradaBitacora.Crear(id ?? Guid.CreateVersion7(), empresaId, fecha ?? reloj.GetUtcNow(), tipoActor, actor, accion, detalle, nota,
            modulo, entidad, entidadId, datoSensible, anterior);
        db.Add(entrada);
        _ultima[empresaId] = entrada.Huella;
    }
}

internal sealed class SolicitudesTitularRepositorio(CoreDbContext db) : ISolicitudesTitular
{
    public Task<SolicitudTitular?> ObtenerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<SolicitudTitular>().FirstOrDefaultAsync(s => s.EmpresaId == empresaId && s.Id == id, cancellationToken);

    public void Agregar(SolicitudTitular solicitud) => db.Add(solicitud);
}

internal sealed class LecturaPrivacidad(CoreDbContext db) : ILecturaPrivacidad
{
    public async Task<Pagina<EntradaBitacoraDto>> BitacoraAsync(Guid empresaId, PaginaSolicitada pagina, string? tipo, string? buscar, bool soloSensibles, CancellationToken ct)
    {
        var q = db.Set<EntradaBitacora>().AsNoTracking().Where(e => e.EmpresaId == empresaId);
        if (tipo is TipoActor.Ia or TipoActor.Persona or TipoActor.Sistema or TipoActor.Titular)
        {
            q = q.Where(e => e.TipoActor == tipo);
        }

        if (soloSensibles)
        {
            q = q.Where(e => e.DatoSensible);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(e => EF.Functions.ILike(e.Actor, patron) || EF.Functions.ILike(e.Accion, patron) || EF.Functions.ILike(e.Detalle, patron));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id).Skip(pagina.Omitir).Take(pagina.Tamano)
            .Select(e => new EntradaBitacoraDto(e.Id, e.Fecha, e.TipoActor, e.Actor, e.Accion, e.Detalle, e.Nota, e.Modulo, e.DatoSensible, e.Huella))
            .ToListAsync(ct);
        return new Pagina<EntradaBitacoraDto>(items, pagina.Numero, pagina.Tamano, total);
    }

    public async Task<IReadOnlyList<EntradaBitacora>> CadenaAsync(Guid empresaId, CancellationToken ct)
    {
        var todas = await db.Set<EntradaBitacora>().AsNoTracking().Where(e => e.EmpresaId == empresaId).ToListAsync(ct);
        // Se recorre por los enlaces (no por fecha): desde la primera (sin anterior) hasta la última
        var porAnterior = todas.GroupBy(e => e.HuellaAnterior).ToDictionary(g => g.Key, g => g.First());
        var enOrden = new List<EntradaBitacora>(todas.Count);
        var siguiente = "";
        while (porAnterior.TryGetValue(siguiente, out var e) && enOrden.Count < todas.Count)
        {
            enOrden.Add(e);
            siguiente = e.Huella;
        }

        // Si quedaron líneas fuera de la cadena (alguien borró una), se agregan al final para que la verificación falle
        enOrden.AddRange(todas.Except(enOrden).OrderBy(e => e.Fecha));
        return enOrden;
    }

    public async Task<Pagina<SolicitudDto>> SolicitudesAsync(Guid empresaId, PaginaSolicitada pagina, string? estado, DateOnly hoy, CancellationToken ct)
    {
        var q = db.Set<SolicitudTitular>().AsNoTracking().Where(s => s.EmpresaId == empresaId);
        if (Enum.TryParse<EstadoSolicitud>(estado, ignoreCase: true, out var e))
        {
            q = q.Where(s => s.Estado == e);
        }

        var total = await q.CountAsync(ct);
        var filas = await q.OrderBy(s => s.Estado).ThenBy(s => s.Vence).ThenByDescending(s => s.RecibidaEn)
            .Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(ct);
        var items = filas.Select(s => new SolicitudDto(
            s.Id, s.Tipo.ToString(), s.Titular, Enmascarar(s.Documento), s.Contacto, s.Canal, s.Descripcion, s.RecibidaEn, s.Vence,
            CalendarioColombia.DiasHabilesRestantes(hoy, s.Vence), SolicitudTitular.PlazoDiasHabiles(s.Tipo), s.BorradorRespuesta, s.Estado.ToString(),
            s.Respuesta, s.RespondidaPor, s.RespondidaEn, s.EsSupresion, s.SupresionAprobada)).ToList();
        return new Pagina<SolicitudDto>(items, pagina.Numero, pagina.Tamano, total);
    }

    public async Task<ResumenPrivacidadDto> ResumenAsync(Guid empresaId, DateTimeOffset ahora, DateOnly hoy, CancellationToken ct)
    {
        var abiertas = await db.Set<SolicitudTitular>().AsNoTracking().Where(s => s.EmpresaId == empresaId && s.Estado == EstadoSolicitud.Abierta)
            .Select(s => s.Vence).ToListAsync(ct);
        var restantes = abiertas.Select(v => CalendarioColombia.DiasHabilesRestantes(hoy, v)).ToList();
        var bitacora = db.Set<EntradaBitacora>().AsNoTracking().Where(e => e.EmpresaId == empresaId);
        var sensibles = await bitacora.CountAsync(e => e.DatoSensible && e.Fecha >= ahora.AddDays(-7), ct);
        var inicioHoy = new DateTimeOffset(hoy.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-5)).ToUniversalTime();
        var hoyEntradas = await bitacora.CountAsync(e => e.Fecha >= inicioHoy, ct);
        return new ResumenPrivacidadDto(abiertas.Count, restantes.Count(r => r is >= 0 and <= 3), restantes.Count(r => r < 0), sensibles, hoyEntradas);
    }

    private static string Enmascarar(string documento) => documento.Length <= 3 ? "CC ***" : $"CC ***.{documento[^3..]}";
}
