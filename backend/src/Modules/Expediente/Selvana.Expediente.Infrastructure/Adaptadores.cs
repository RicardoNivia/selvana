using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Expediente.Infrastructure.Persistence;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Infrastructure;

internal sealed class EmpleadosRepositorio(ExpedienteDbContext db) : IEmpleados
{
    public Task<Empleado?> PorIdAsync(Guid empresaId, Guid id, CancellationToken cancellationToken) =>
        db.Set<Empleado>().FirstOrDefaultAsync(e => e.EmpresaId == empresaId && e.Id == id, cancellationToken);

    public Task<bool> ActivoConDocumentoAsync(Guid empresaId, string documento, CancellationToken cancellationToken) =>
        db.Set<Empleado>().AnyAsync(e => e.EmpresaId == empresaId && e.Documento == documento && e.Estado == EstadoEmpleado.Activo, cancellationToken);

    public Task<bool> DeProcesoAsync(Guid procesoVinculacionId, CancellationToken cancellationToken) =>
        db.Set<Empleado>().AnyAsync(e => e.ProcesoVinculacionId == procesoVinculacionId, cancellationToken);

    public async Task<HashSet<string>> DocumentosActivosAsync(Guid empresaId, IReadOnlyList<string> documentos, CancellationToken cancellationToken) =>
        [.. await db.Set<Empleado>().Where(e => e.EmpresaId == empresaId && e.Estado == EstadoEmpleado.Activo && documentos.Contains(e.Documento))
            .Select(e => e.Documento).ToListAsync(cancellationToken)];

    public void Agregar(Empleado empleado) => db.Add(empleado);
}

internal sealed class LecturaEmpleados(ExpedienteDbContext db) : ILecturaEmpleados
{
    private IQueryable<Empleado> Visibles(Guid empresaId, IReadOnlyCollection<Guid>? alcance) =>
        db.Set<Empleado>().AsNoTracking().Where(e => e.EmpresaId == empresaId
            && (alcance == null || e.EmpresaUsuariaId == null || alcance.Contains(e.EmpresaUsuariaId.Value)));

    /// <summary>Activos con algo por atender: vence algo en 45 días (documento o contrato) o faltan afiliaciones.</summary>
    private static IQueryable<Empleado> ConAlertas(IQueryable<Empleado> q, DateOnly hoy)
    {
        var limite = hoy.AddDays(Empleado.DiasAvisoVencimiento + 15);
        return q.Where(e => e.Estado == EstadoEmpleado.Activo && ((e.ProximoVencimiento != null && e.ProximoVencimiento <= limite) || !e.AfiliacionesCompletas));
    }

    public async Task<Pagina<FilaEmpleadoDto>> EmpleadosAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, PaginaSolicitada pagina, string? buscar,
        string? estado, bool soloConAlertas, DateOnly hoy, CancellationToken cancellationToken)
    {
        var q = Visibles(empresaId, alcance);
        if (Enum.TryParse<EstadoEmpleado>(estado, out var e))
        {
            q = q.Where(x => x.Estado == e);
        }

        if (soloConAlertas)
        {
            q = ConAlertas(q, hoy);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.Nombre, patron) || EF.Functions.ILike(x.Documento, patron) || EF.Functions.ILike(x.Codigo, patron)
                || EF.Functions.ILike(x.Cargo, patron) || EF.Functions.ILike(x.Area, patron) || EF.Functions.ILike(x.Sede, patron));
        }

        var total = await q.CountAsync(cancellationToken);
        var filas = await q.OrderBy(x => x.Estado).ThenBy(x => x.Nombre).Skip(pagina.Omitir).Take(pagina.Tamano).ToListAsync(cancellationToken);
        return new Pagina<FilaEmpleadoDto>([.. filas.Select(x => new FilaEmpleadoDto(x.Id, x.Codigo, x.Nombre, x.Documento, x.Cargo, x.Area, x.Sede, x.EmpresaUsuaria,
            x.TipoContrato, x.SalarioCop, x.FechaIngreso, x.Estado.ToString(), x.Alertas(hoy).Count))], pagina.Numero, pagina.Tamano, total);
    }

    public async Task<ConteoEmpleadosDto> ConteoAsync(Guid empresaId, IReadOnlyCollection<Guid>? alcance, DateOnly hoy, CancellationToken cancellationToken)
    {
        var q = Visibles(empresaId, alcance);
        return new ConteoEmpleadosDto(await q.CountAsync(e => e.Estado == EstadoEmpleado.Activo, cancellationToken),
            await q.CountAsync(e => e.Estado == EstadoEmpleado.Retirado, cancellationToken), await ConAlertas(q, hoy).CountAsync(cancellationToken));
    }
}

internal sealed class EventosExpediente(ExpedienteDbContext db) : IEventosExpediente
{
    public void Publicar(IIntegrationEvent evento) => db.AddIntegrationEvent(evento);
}

internal sealed class Bitacora(IEventosExpediente eventos, TimeProvider reloj) : IBitacora
{
    public void Registrar(Guid empresaId, string tipoActor, string actor, string accion, string detalle, string? nota, string entidad, Guid? entidadId,
        bool datoSensible = false) =>
        eventos.Publicar(new AccionAuditada(Guid.NewGuid(), reloj.GetUtcNow(), empresaId, tipoActor, actor, accion, detalle, nota,
            "Expediente", entidad, entidadId, datoSensible));
}

/// <summary>Archivo de la carpeta del empleado guardado cifrado (Data Protection).</summary>
internal sealed class ArchivoEmpleado
{
    public Guid Id { get; init; }
    public Guid EmpresaId { get; init; }
    public string Nombre { get; init; } = "";
    public string TipoContenido { get; init; } = "";
    public int Tamano { get; init; }
    public byte[] ContenidoCifrado { get; init; } = [];
    public DateTimeOffset SubidoEn { get; init; }
}

internal sealed class ArchivoEmpleadoConfiguration : IEntityTypeConfiguration<ArchivoEmpleado>
{
    public void Configure(EntityTypeBuilder<ArchivoEmpleado> builder)
    {
        builder.ToTable("archivos");
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Nombre).HasMaxLength(260);
        builder.Property(a => a.TipoContenido).HasMaxLength(120);
        builder.HasIndex(a => a.EmpresaId);
    }
}

internal sealed class AlmacenArchivos(ExpedienteDbContext db, IDataProtectionProvider proteccion) : IAlmacenArchivos
{
    private readonly IDataProtector _protector = proteccion.CreateProtector("Selvana.Expediente.Archivos.v1");

    public Guid Guardar(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, DateTimeOffset ahora)
    {
        var archivo = new ArchivoEmpleado
        {
            Id = Guid.CreateVersion7(),
            EmpresaId = empresaId,
            Nombre = nombre.Length > 260 ? nombre[..260] : nombre,
            TipoContenido = tipoContenido,
            Tamano = contenido.Length,
            ContenidoCifrado = _protector.Protect(contenido),
            SubidoEn = ahora,
        };
        db.Add(archivo);
        return archivo.Id;
    }

    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> LeerAsync(Guid empresaId, Guid id, CancellationToken cancellationToken)
    {
        var a = await db.Set<ArchivoEmpleado>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, cancellationToken);
        return a is null ? null : (a.Nombre, a.TipoContenido, _protector.Unprotect(a.ContenidoCifrado));
    }
}
