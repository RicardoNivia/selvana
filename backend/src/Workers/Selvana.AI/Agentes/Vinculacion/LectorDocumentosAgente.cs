using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Selvana.AI.Agentes.Reclutamiento;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Contracts.Vinculacion;
using Selvana.AI.Gateway;
using Selvana.AI.Persistence;
using Selvana.Infrastructure.Common.Messaging;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Contracts.Ia;

namespace Selvana.AI.Agentes.Vinculacion;

/// <summary>Lo que devuelve el modelo sobre un documento de ingreso.</summary>
public sealed record LecturaIngreso(string TipoDetectado, bool Legible, bool TipoCorrecto, IReadOnlyList<CampoDocumento> Campos, IReadOnlyList<string> Observaciones,
    IReadOnlyList<RazonRedaccion> Razones);

public sealed record ResultadoLecturaIngreso(LecturaIngreso Lectura, string Modelo, decimal CostoUsd);

public interface ILectorDocumentosIngreso
{
    Task<Result<ResultadoLecturaIngreso>> LeerAsync(Guid empresaId, string tipoEsperado, string tipoContenido, byte[] contenido, CancellationToken cancellationToken);
}

/// <summary>
/// Agente lector de documentos de ingreso (cédula, certificado bancario, EPS, pensiones, diplomas, certificados laborales):
/// dice qué documento es, si se lee bien y extrae sus datos. No decide si es de la persona (eso lo comparan las reglas)
/// ni si se aprueba (lo decide RRHH). Nunca recibe el examen médico ni antecedentes. Solo modelos de pago (datos personales).
/// </summary>
internal sealed class LectorDocumentosAgente(IChatClient ia, IFiltroVariablesProtegidas filtro, TimeProvider reloj) : ILectorDocumentosIngreso
{
    public const string Nombre = "lector-documentos-ingreso";

    private const string Instrucciones = """
        Eres el lector de documentos de ingreso de Selvana, un software de talento humano colombiano.
        Te envían UN documento que el trabajador subió como el «documento pedido» que se indica. Responde:

        - "tipoDetectado": qué documento es en realidad (por ejemplo "Cédula de ciudadanía", "Certificado bancario", "Certificado de afiliación EPS", "Otro: factura").
        - "tipoCorrecto": true si es el documento pedido.
        - "legible": false si está borroso, cortado, al revés o le falta una cara (la cédula tiene dos caras).
        - "campos": los datos que aparecen, con "campo" y "valor" tal cual: "nombre del titular", "número de documento" (solo dígitos),
          y según el tipo: "banco", "tipo de cuenta", "número de cuenta", "entidad" (EPS o fondo), "estado de la afiliación", "fecha de expedición",
          "institución", "título", "empresa", "cargo", "desde", "hasta",
          "fecha de vencimiento" (licencias, cursos y certificados con vigencia; dd/mm/aaaa). No inventes: si no aparece, no lo pongas.
        - "observaciones": lo que RRHH debe revisar (fecha de expedición de más de 30 días en certificados, afiliación inactiva, falta una cara, tachones).
          Lista vacía si está bien.
        - "razones": 2 a 4, con "signo" ("+" o "−"), "texto" corto y "fuente" ("Documento").
        - Nunca extraigas ni menciones fecha de nacimiento, edad, sexo, grupo sanguíneo, estado civil, foto ni dirección, aunque aparezcan (la cédula los trae).
        """;

    public async Task<Result<ResultadoLecturaIngreso>> LeerAsync(Guid empresaId, string tipoEsperado, string tipoContenido, byte[] contenido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var mensajes = new List<ChatMessage>
        {
            new(ChatRole.System, Instrucciones),
            new(ChatRole.User, [new TextContent($"FECHA DE HOY: {hoy:dd/MM/yyyy}. Documento pedido: «{tipoEsperado}»."), new DataContent(contenido, tipoContenido)]),
        };
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: true).AplicarA(new ChatOptions { MaxOutputTokens = 3_000 });

        ChatResponse<LecturaIngreso> respuesta;
        try
        {
            respuesta = await ia.GetResponseAsync<LecturaIngreso>(mensajes, opciones, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió");
        }

        if (!respuesta.TryGetResult(out var lectura) || lectura is null)
        {
            return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió una lectura válida");
        }

        var costo = respuesta.AdditionalProperties?.TryGetValue(CadenaModelosChatClient.PropiedadCostoUsd, out var c) == true && c is decimal d ? d : 0m;
        return new ResultadoLecturaIngreso(Validar(lectura, filtro), respuesta.ModelId ?? "", costo);
    }

    /// <summary>Quita campos protegidos (nacimiento, sexo, sangre, dirección…) aunque la IA los traiga, y limpia el texto.</summary>
    internal static LecturaIngreso Validar(LecturaIngreso l, IFiltroVariablesProtegidas filtro)
    {
        static string Limpio(string? t) => CalificadorAfinidadAgente.SinEscapes(t).Trim();
        string[] prohibidos = ["nacimiento", "edad", "sexo", "genero", "género", "sangre", "rh", "estado civil", "direccion", "dirección", "estatura", "foto"];
        var campos = (l.Campos ?? []).Select(x => new CampoDocumento(Limpio(x.Campo), Limpio(x.Valor)))
            .Where(x => x.Campo.Length > 0 && x.Valor.Length > 0 && !prohibidos.Any(p => x.Campo.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .Take(15).ToList();
        return new LecturaIngreso(Limpio(l.TipoDetectado), l.Legible, l.TipoCorrecto, campos,
            [.. (l.Observaciones ?? []).Select(Limpio).Where(o => o.Length > 0 && !filtro.EsDiscriminatorio(o)).Take(5)],
            [.. (l.Razones ?? []).Where(r => !string.IsNullOrWhiteSpace(r.Texto) && !filtro.EsDiscriminatorio(r.Texto))
                .Select(r => new RazonRedaccion(r.Signo is "-" or "−" ? "−" : "+", Limpio(r.Texto), "Documento")).Take(4)]);
    }
}

/// <summary>Llegó un documento de ingreso: el worker lo descarga con la clave interna, la IA lo lee y la lectura vuelve por el outbox.</summary>
internal sealed class DocumentoIngresoRecibidoConsumer(ILectorDocumentosIngreso lector, IHttpClientFactory http, IConfiguration configuracion,
    IOptions<MensajeriaOptions> mensajeria, IaDbContext db, TimeProvider reloj) : IIntegrationEventConsumer<DocumentoIngresoRecibido>
{
    public async Task Handle(DocumentoIngresoRecibido e, CancellationToken cancellationToken)
    {
        var url = (configuracion["Vinculacion:UrlArchivos"] ?? "http://localhost:5000/internal/archivos/vinculacion/") + e.ArchivoId;
        using var cliente = http.CreateClient("archivos-reclutamiento");
        using var peticion = new HttpRequestMessage(HttpMethod.Get, url);
        peticion.Headers.Add(MensajeriaOptions.EncabezadoClave, mensajeria.Value.ClaveInterna);
        using var respuesta = await cliente.SendAsync(peticion, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadAsByteArrayAsync(cancellationToken);

        var r = await lector.LeerAsync(e.EmpresaId, e.TipoEsperado, e.TipoContenido, contenido, cancellationToken);
        var ahora = reloj.GetUtcNow();
        db.AddIntegrationEvent(r.IsSuccess
            ? new DocumentoIngresoLeido(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, e.Tipo, e.ArchivoId, r.Value.Lectura.TipoDetectado, r.Value.Lectura.Legible,
                r.Value.Lectura.TipoCorrecto, r.Value.Lectura.Campos, r.Value.Lectura.Observaciones, r.Value.Lectura.Razones, r.Value.Modelo, r.Value.CostoUsd, null)
            : new DocumentoIngresoLeido(Guid.CreateVersion7(), ahora, e.EmpresaId, e.ProcesoId, e.Tipo, e.ArchivoId, "", false, false, [], [], [], "", 0, r.Error.Description));
        await db.SaveChangesAsync(cancellationToken);
    }
}
