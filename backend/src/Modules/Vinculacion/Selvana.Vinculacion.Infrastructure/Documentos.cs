using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Selvana.Vinculacion.Application.Documentos;
using Selvana.Vinculacion.Infrastructure.Persistence;

namespace Selvana.Vinculacion.Infrastructure;

/// <summary>Documento de ingreso guardado cifrado (Data Protection). El dominio solo conoce su Id.</summary>
internal sealed class ArchivoDocumento
{
    public Guid Id { get; init; }
    public Guid EmpresaId { get; init; }
    public string Nombre { get; init; } = "";
    public string TipoContenido { get; init; } = "";
    public int Tamano { get; init; }
    public byte[] ContenidoCifrado { get; init; } = [];
    public DateTimeOffset SubidoEn { get; init; }
}

internal sealed class ArchivoDocumentoConfiguration : IEntityTypeConfiguration<ArchivoDocumento>
{
    public void Configure(EntityTypeBuilder<ArchivoDocumento> builder)
    {
        builder.ToTable("archivos_documentos");
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Nombre).HasMaxLength(260);
        builder.Property(a => a.TipoContenido).HasMaxLength(120);
    }
}

internal sealed class AlmacenDocumentos(VinculacionDbContext db, IDataProtectionProvider proteccion) : IAlmacenDocumentos
{
    private readonly IDataProtector _protector = proteccion.CreateProtector("Selvana.Vinculacion.Documentos.v1");

    public Guid Guardar(Guid empresaId, string nombre, string tipoContenido, byte[] contenido, DateTimeOffset ahora)
    {
        var archivo = new ArchivoDocumento
        {
            Id = Guid.NewGuid(),
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

    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> LeerAsync(Guid id, CancellationToken cancellationToken)
    {
        var a = await db.Set<ArchivoDocumento>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return a is null ? null : (a.Nombre, a.TipoContenido, _protector.Unprotect(a.ContenidoCifrado));
    }
}

/// <summary>Concepto médico y otros datos sensibles: cifrados con Data Protection, con un propósito propio.</summary>
internal sealed class CifradoSensible(IDataProtectionProvider proteccion) : ICifradoSensible
{
    private readonly IDataProtector _protector = proteccion.CreateProtector("Selvana.Vinculacion.DatosSensibles.v1");

    public string Cifrar(string texto) => _protector.Protect(texto);

    public string Descifrar(string cifrado) => _protector.Unprotect(cifrado);
}
