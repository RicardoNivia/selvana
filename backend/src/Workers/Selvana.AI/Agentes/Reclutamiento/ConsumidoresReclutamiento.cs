using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Estructura;
using Selvana.AI.Contracts.Reclutamiento;
using Selvana.AI.Persistence;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Messaging;

namespace Selvana.AI.Agentes.Reclutamiento;

/// <summary>Vacante en un minuto: el redactor convierte la frase en un borrador y responde por el outbox.</summary>
internal sealed class VacanteSolicitadaConsumer(IRedactorVacantes redactor, IaDbContext db, TimeProvider reloj) : IIntegrationEventConsumer<VacanteSolicitada>
{
    public async Task Handle(VacanteSolicitada e, CancellationToken cancellationToken)
    {
        var frase = e.EmpresaUsuaria is null ? e.Frase : $"{e.Frase} (cliente: {e.EmpresaUsuaria})";
        var r = await redactor.RedactarAsync(new SolicitudBorradorVacante(e.EmpresaId, frase, e.Perfil), cancellationToken);
        var ahora = reloj.GetUtcNow();

        if (r.IsFailure)
        {
            db.AddIntegrationEvent(new BorradorVacanteRedactado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.VacanteId,
                "", "", "", 0, "", "", [], [], [], 0, 0, "", [], [], [], "", r.Error.Description));
        }
        else
        {
            var b = r.Value.Borrador;
            db.AddIntegrationEvent(new BorradorVacanteRedactado(Guid.CreateVersion7(), ahora, e.EmpresaId, e.VacanteId,
                b.Titulo, b.Cargo, b.Ciudad, b.Cupos, b.Jornada, b.Descripcion, b.Requisitos, b.Competencias, b.PreguntasFiltro,
                b.RangoSalarial.MinimoCop, b.RangoSalarial.MaximoCop, b.RangoSalarial.Justificacion,
                [.. b.Razones.Select(x => new RazonIa(x.Evidencia == "Supuesto" ? "−" : "+", x.Texto, x.Evidencia))],
                b.Supuestos, r.Value.RequisitosRetiradosPorSesgo, r.Value.Modelo, null,
                [.. (b.Aclaraciones ?? []).Select(a => new AclaracionIa(a.Pregunta, a.Opciones))], r.Value.CostoUsd));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Un candidato subió su hoja de vida: se descarga del API (clave interna), la IA la lee y responde.</summary>
internal sealed class HojaDeVidaRecibidaConsumer(
    ILectorHojasDeVida lector, IHttpClientFactory http, IConfiguration configuracion, IOptions<MensajeriaOptions> mensajeria,
    IaDbContext db, TimeProvider reloj) : IIntegrationEventConsumer<HojaDeVidaRecibida>
{
    public async Task Handle(HojaDeVidaRecibida e, CancellationToken cancellationToken)
    {
        var url = (configuracion["Reclutamiento:UrlArchivos"] ?? "http://localhost:5000/internal/archivos/") + e.ArchivoId;
        using var cliente = http.CreateClient("archivos-reclutamiento");
        using var peticion = new HttpRequestMessage(HttpMethod.Get, url);
        peticion.Headers.Add(MensajeriaOptions.EncabezadoClave, mensajeria.Value.ClaveInterna);
        using var respuesta = await cliente.SendAsync(peticion, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadAsByteArrayAsync(cancellationToken);

        var r = await lector.LeerAsync(e.EmpresaId, e.NombreArchivo, e.TipoContenido, contenido, cancellationToken);
        db.AddIntegrationEvent(r.IsSuccess
            ? new HojaDeVidaLeida(Guid.CreateVersion7(), reloj.GetUtcNow(), e.EmpresaId, e.BorradorId, r.Value.Datos, r.Value.Modelo, null)
            : new HojaDeVidaLeida(Guid.CreateVersion7(), reloj.GetUtcNow(), e.EmpresaId, e.BorradorId, null, "", r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Llegó una postulación: el agente de ranking califica su afinidad y responde por el outbox.</summary>
internal sealed class AfinidadSolicitadaConsumer(ICalificadorAfinidad calificador, IaDbContext db, TimeProvider reloj) : IIntegrationEventConsumer<AfinidadSolicitada>
{
    public async Task Handle(AfinidadSolicitada e, CancellationToken cancellationToken)
    {
        var r = await calificador.CalificarAsync(e.EmpresaId, e.Vacante, e.Perfil, e.Calibracion, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new AfinidadCalculada(Guid.CreateVersion7(), ahora, e.EmpresaId, e.PostulacionId, r.Value.Afinidad.Puntaje, r.Value.Afinidad.Razones,
                r.Value.Afinidad.RequisitosCumplidos, r.Value.Afinidad.RequisitosFaltantes, r.Value.Modelo, r.Value.CostoUsd, null, r.Value.RazonesRetiradas)
            : new AfinidadCalculada(Guid.CreateVersion7(), ahora, e.EmpresaId, e.PostulacionId, 0, [], [], [], "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
