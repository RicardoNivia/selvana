using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Selvana.AI.Agentes.Vinculacion;
using Selvana.AI.Contracts.Expediente;
using Selvana.AI.Persistence;
using Selvana.Expediente.Contracts.Ia;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Agentes.Expediente;

/// <summary>
/// Documento nuevo en la carpeta del empleado: el worker lo descarga con la clave interna y el mismo lector de documentos
/// (el de los documentos de ingreso) dice qué es y extrae sus datos. Las reglas del expediente proponen los cambios; RRHH decide.
/// </summary>
internal sealed class DocumentoEmpleadoRecibidoConsumer(ILectorDocumentosIngreso lector, IHttpClientFactory http, IConfiguration configuracion,
    IOptions<MensajeriaOptions> mensajeria, IaDbContext db, TimeProvider reloj) : IIntegrationEventConsumer<DocumentoEmpleadoRecibido>
{
    public async Task Handle(DocumentoEmpleadoRecibido e, CancellationToken cancellationToken)
    {
        var url = (configuracion["Expediente:UrlArchivos"] ?? "http://localhost:5000/internal/archivos/expediente/") + e.ArchivoId;
        using var cliente = http.CreateClient("archivos-reclutamiento");
        using var peticion = new HttpRequestMessage(HttpMethod.Get, url);
        peticion.Headers.Add(MensajeriaOptions.EncabezadoClave, mensajeria.Value.ClaveInterna);
        using var respuesta = await cliente.SendAsync(peticion, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadAsByteArrayAsync(cancellationToken);

        var r = await lector.LeerAsync(e.EmpresaId, e.TipoEsperado, e.TipoContenido, contenido, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new DocumentoEmpleadoLeido(Guid.CreateVersion7(), ahora, e.EmpresaId, e.EmpleadoId, e.DocumentoId, e.ArchivoId, r.Value.Lectura.TipoDetectado,
                r.Value.Lectura.Legible, r.Value.Lectura.TipoCorrecto, r.Value.Lectura.Campos, r.Value.Lectura.Observaciones, r.Value.Lectura.Razones, r.Value.Modelo,
                r.Value.CostoUsd, null)
            : new DocumentoEmpleadoLeido(Guid.CreateVersion7(), ahora, e.EmpresaId, e.EmpleadoId, e.DocumentoId, e.ArchivoId, "", false, false, [], [], [], "", 0,
                r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
