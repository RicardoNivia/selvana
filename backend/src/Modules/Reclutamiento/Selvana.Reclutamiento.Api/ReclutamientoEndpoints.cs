using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Reclutamiento.Application;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Candidatos;
using Selvana.Reclutamiento.Application.Filtros;
using Selvana.Reclutamiento.Application.Portal;
using Selvana.Reclutamiento.Application.Preseleccion;
using Selvana.Reclutamiento.Application.Privacidad;
using Selvana.Reclutamiento.Application.Pruebas;
using Selvana.Reclutamiento.Application.Ranking;
using Selvana.Reclutamiento.Application.Recontacto;
using Selvana.Reclutamiento.Application.Seleccion;
using Selvana.Reclutamiento.Application.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Web.Common;

namespace Selvana.Reclutamiento.Api;

public sealed record SolicitarVacanteRequest(string Frase, Guid? EmpresaUsuariaId, Guid? CargoId = null);

public sealed record EditarVacanteRequest(
    string Titulo, string Cargo, string Ciudad, int Cupos, string Jornada, string Descripcion,
    decimal SalarioMinimo, decimal SalarioMaximo, IReadOnlyList<string> Requisitos, IReadOnlyList<string> PreguntasFiltro,
    string? CodigoMunicipio = null, bool EmpresaConfidencial = false, bool SalarioOculto = false);

public sealed record CrearVacanteRequest(
    Guid? EmpresaUsuariaId, Guid? CargoId, string Titulo, string Cargo, string Ciudad, string? CodigoMunicipio, int Cupos, string Jornada,
    string Descripcion, decimal SalarioMinimo, decimal SalarioMaximo, IReadOnlyList<string> Requisitos, IReadOnlyList<string> PreguntasFiltro,
    bool EmpresaConfidencial = false, bool SalarioOculto = false);

public sealed record RedactarVacanteRequest(string Frase);

public sealed record CreadoResponse(Guid Id);

public sealed record AbrirHojaDeVidaRequest(string Motivo);

public sealed record DevolverRequisicionRequest(string Motivo);

public sealed record AjustarAfinidadRequest(int Puntaje, string Motivo);

public sealed record AfinidadPedidaResponse(int Pedidas);

public sealed record MoverPostulacionRequest(string Etapa, string? Motivo);

public sealed record AvanzarPostulacionRequest(string Destino, string? Motivo);

public sealed record AplicarSugerenciasRequest(IReadOnlyList<Guid> PostulacionIds);

public sealed record SugerenciasAplicadasResponse(int Aplicadas);

public sealed record ReglasAvanceRequest(string Modo, int UmbralAfinidad, int ToleranciaSalarial);

public sealed record TernaPropuestaResponse(int Candidatos);

public sealed record RetroalimentarRequest(string Valor, string? Nota);

public sealed record MensajePreseleccionRequest(string Texto);

public sealed record GuardarFiltrosRequest(IReadOnlyList<string>? Requisitos, IReadOnlyList<string>? Preguntas, IReadOnlyList<string>? Ciudades, int? ExperienciaMinimaMeses);

public sealed record AprobarDescartesRequest(IReadOnlyList<Guid>? PostulacionIds);

public sealed record DescartesResponse(int Postulaciones);

public sealed record RespuestaPersonaRequest(string Texto, bool Cerrar);

public sealed record NoEncajaRequest(string Motivo);

public sealed record SugeridosBancoResponse(int Sugeridos);

public sealed record GuardarBateriaCargoRequest(string Cargo, IReadOnlyList<PruebaEnBateriaDto>? Pruebas);

public sealed record GuardarBateriaVacanteRequest(IReadOnlyList<PruebaEnBateriaDto>? Pruebas);

public sealed record EnviarPruebasRequest(IReadOnlyList<Guid>? PostulacionIds);

public sealed record ConsentimientoPruebasRequest(bool Acepta);

public sealed record TerminarPruebaRequest(IReadOnlyList<RespuestaDto>? Respuestas, int CambiosDePestana);

public sealed record EnviarPostulacionRequest(
    string Nombre, string Documento, string Celular, string Correo, string Ciudad, IReadOnlyList<bool> Respuestas, bool AceptaAutorizacion, string? Canal,
    string? LinkedIn = null, IReadOnlyList<decimal?>? Valores = null);

public sealed record PostulacionEnviadaResponse(string Numero);

public static class ReclutamientoEndpoints
{
    public static IEndpointRouteBuilder MapReclutamientoEndpoints(this IEndpointRouteBuilder app)
    {
        MapConsola(app.MapGroup("/reclutamiento").WithTags("Vacantes").RequireAuthorization());
        MapCandidatos(app.MapGroup("/reclutamiento/candidatos").WithTags("Candidatos y banco de talento").RequireAuthorization(Politicas.VerHojasDeVida));
        MapPrivacidad(app.MapGroup("/reclutamiento").WithTags("Privacidad"));
        MapPruebas(app.MapGroup("/reclutamiento").WithTags("Pruebas psicotécnicas").RequireAuthorization());
        MapPruebasCandidato(app.MapGroup("/portal/pruebas").WithTags("Pruebas psicotécnicas (candidato)").AllowAnonymous()
            .RequireRateLimiting(ClaveInterna.PoliticaPortalPublico));
        MapPreseleccionCandidato(app.MapGroup("/portal/preseleccion").WithTags("Preselección conversacional (candidato)").AllowAnonymous()
            .RequireRateLimiting(ClaveInterna.PoliticaPortalPublico));
        MapPortal(app.MapGroup("/portal").WithTags("Portal de empleo público").AllowAnonymous().RequireRateLimiting(ClaveInterna.PoliticaPortalPublico));
        // Postularse no exige cuenta (ADR 0014): si hay sesión del portal, se usa su correo y su identificación
        MapPostularse(app.MapGroup("/portal").WithTags("Postularse").AllowAnonymous().AddEndpointFilter(SesionesPortal.UsarSiHay)
            .RequireRateLimiting(ClaveInterna.PoliticaPortalPublico));
        app.MapPortalPersona();
        return app;
    }

    /// <summary>El lector de IA descarga la hoja de vida descifrada. Solo con la clave interna del worker.</summary>
    public static IEndpointRouteBuilder MapReclutamientoInterno(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/archivos/{id:guid}", async (Guid id, IAlmacenHojasDeVida almacen, CancellationToken ct) =>
                await almacen.LeerAsync(id, ct) is { } a ? Results.File(a.Contenido, a.TipoContenido, a.Nombre) : Results.NotFound())
            .RequiereClaveInterna();
        return app;
    }

    private static void MapPruebas(RouteGroupBuilder g)
    {
        g.MapGet("/pruebas/suite", async (IQueryHandler<ListarSuiteQuery, IReadOnlyList<PruebaSuiteDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarSuiteQuery(), ct)).ToHttp())
            .WithName("ListarSuitePruebas")
            .Produces<IReadOnlyList<PruebaSuiteDto>>();

        g.MapGet("/pruebas/baterias", async (string cargo, IQueryHandler<ObtenerBateriaCargoQuery, BateriaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerBateriaCargoQuery(cargo), ct)).ToHttp())
            .WithName("ObtenerBateriaCargo")
            .Produces<BateriaDto>();

        g.MapPut("/pruebas/baterias", async (GuardarBateriaCargoRequest b, ICommandHandler<GuardarBateriaCargoCommand> h, CancellationToken ct) =>
                (await h.Handle(new GuardarBateriaCargoCommand(b.Cargo, b.Pruebas ?? []), ct)).ToHttp())
            .WithName("GuardarBateriaCargo")
            .RequireAuthorization(Politicas.DeAccion("CrearVacantes"));

        g.MapPut("/vacantes/{id:guid}/pruebas", async (Guid id, GuardarBateriaVacanteRequest b, ICommandHandler<GuardarBateriaVacanteCommand> h, CancellationToken ct) =>
                (await h.Handle(new GuardarBateriaVacanteCommand(id, b.Pruebas ?? []), ct)).ToHttp())
            .WithName("GuardarBateriaVacante")
            .RequireAuthorization(Politicas.DeAccion("CrearVacantes"));

        g.MapPost("/vacantes/{id:guid}/pruebas/enviar", async (Guid id, EnviarPruebasRequest b, ICommandHandler<EnviarPruebasCommand, IReadOnlyList<EnvioPruebasDto>> h, CancellationToken ct) =>
                (await h.Handle(new EnviarPruebasCommand(id, b.PostulacionIds ?? []), ct)).ToHttp())
            .WithName("EnviarPruebas")
            .RequireAuthorization(Politicas.DeAccion("CrearVacantes"))
            .Produces<IReadOnlyList<EnvioPruebasDto>>();

        g.MapGet("/vacantes/{id:guid}/pruebas/resultados", async (Guid id, IQueryHandler<ListarResultadosPruebasQuery, IReadOnlyList<ResultadoPostulanteDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarResultadosPruebasQuery(id), ct)).ToHttp())
            .WithName("ResultadosPruebas")
            .RequireAuthorization(Politicas.VerHojasDeVida)
            .Produces<IReadOnlyList<ResultadoPostulanteDto>>();

        // Ver el detalle de personalidad es un acceso a datos sensibles: POST y queda en la bitácora
        g.MapPost("/pruebas/aplicaciones/{id:guid}/sensible/{codigo}", async (Guid id, string codigo, ICommandHandler<VerResultadoSensibleCommand, ResultadoSensibleDto> h, CancellationToken ct) =>
                (await h.Handle(new VerResultadoSensibleCommand(id, codigo), ct)).ToHttp())
            .WithName("VerResultadoSensible")
            .RequireAuthorization(Politicas.DeAccion("VerDatosSensibles"))
            .Produces<ResultadoSensibleDto>();
    }

    private static void MapPruebasCandidato(RouteGroupBuilder g)
    {
        g.MapGet("/{token}", async (string token, IQueryHandler<ObtenerAplicacionQuery, AplicacionPublicaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerAplicacionQuery(token), ct)).ToHttp())
            .WithName("ObtenerPruebasCandidato")
            .Produces<AplicacionPublicaDto>();

        g.MapPost("/{token}/consentimiento", async (string token, ConsentimientoPruebasRequest b, ICommandHandler<ResponderConsentimientoCommand> h, CancellationToken ct) =>
                (await h.Handle(new ResponderConsentimientoCommand(token, b.Acepta), ct)).ToHttp())
            .WithName("ConsentimientoPruebas");

        g.MapPost("/{token}/{codigo}/iniciar", async (string token, string codigo, ICommandHandler<IniciarPruebaCommand, PreguntasPruebaDto> h, CancellationToken ct) =>
                (await h.Handle(new IniciarPruebaCommand(token, codigo), ct)).ToHttp())
            .WithName("IniciarPrueba")
            .Produces<PreguntasPruebaDto>();

        g.MapPost("/{token}/{codigo}/terminar", async (string token, string codigo, TerminarPruebaRequest b, ICommandHandler<TerminarPruebaCommand> h, CancellationToken ct) =>
                (await h.Handle(new TerminarPruebaCommand(token, codigo, b.Respuestas ?? [], b.CambiosDePestana), ct)).ToHttp())
            .WithName("TerminarPrueba");
    }

    /// <summary>El candidato conversa con el agente desde el enlace de un solo uso (sin cuenta).</summary>
    private static void MapPreseleccionCandidato(RouteGroupBuilder g)
    {
        g.MapGet("/{token}", async (string token, IQueryHandler<ObtenerConversacionPublicaQuery, ConversacionPublicaDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerConversacionPublicaQuery(token), ct)).ToHttp())
            .WithName("ConversacionPreseleccionCandidato").Produces<ConversacionPublicaDto>();

        g.MapPost("/{token}/mensajes", async (string token, MensajePreseleccionRequest b, ICommandHandler<EscribirCandidatoCommand> h, CancellationToken ct) =>
                (await h.Handle(new EscribirCandidatoCommand(token, b.Texto), ct)).ToHttp())
            .WithName("MensajeCandidatoPreseleccion");
    }

    private static void MapPrivacidad(RouteGroupBuilder g)
    {
        // Abrir una hoja de vida queda en la bitácora (quién, cuándo, para qué); por eso es POST y no un enlace directo
        g.MapPost("/postulaciones/{id:guid}/hoja-de-vida", async (Guid id, AbrirHojaDeVidaRequest b, ICommandHandler<AbrirHojaDeVidaCommand, ArchivoDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AbrirHojaDeVidaCommand(id, b.Motivo), ct);
                return r.IsSuccess ? Results.File(r.Value.Contenido, r.Value.TipoContenido, r.Value.Nombre) : r.ToHttp();
            })
            .WithName("AbrirHojaDeVida")
            .RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapGet("/autorizaciones", async (int? pagina, int? tamano, string? buscar, string? estado,
                IQueryHandler<ListarAutorizacionesQuery, Pagina<AutorizacionDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarAutorizacionesQuery(pagina, tamano, buscar, estado), ct)).ToHttp())
            .WithName("ListarAutorizaciones")
            .RequireAuthorization(Politicas.VerBitacora)
            .Produces<Pagina<AutorizacionDto>>();

        g.MapGet("/autorizaciones/resumen", async (IQueryHandler<ObtenerResumenAutorizacionesQuery, ResumenAutorizacionesDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerResumenAutorizacionesQuery(), ct)).ToHttp())
            .WithName("ResumenAutorizaciones")
            .RequireAuthorization(Politicas.VerBitacora)
            .Produces<ResumenAutorizacionesDto>();
    }

    private static void MapCandidatos(RouteGroupBuilder g)
    {
        g.MapGet("/", async (string? buscar, int? pagina, int? tamano, IQueryHandler<BuscarCandidatosQuery, BancoTalentoDto> h, CancellationToken ct) =>
                (await h.Handle(new BuscarCandidatosQuery(buscar, pagina, tamano), ct)).ToHttp())
            .WithName("BuscarCandidatos").Produces<BancoTalentoDto>();

        g.MapGet("/{id:guid}", async (Guid id, IQueryHandler<ObtenerCandidatoQuery, CandidatoDetalleDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerCandidatoQuery(id), ct)).ToHttp())
            .WithName("ObtenerCandidato").Produces<CandidatoDetalleDto>();
    }

    private static void MapConsola(RouteGroupBuilder g)
    {
        g.MapGet("/vacantes", async (int? pagina, int? tamano, string? buscar, string? estado, Guid? empresaUsuariaId,
                IQueryHandler<ListarVacantesQuery, Pagina<VacanteResumenDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarVacantesQuery(pagina, tamano, buscar, estado, empresaUsuariaId), ct)).ToHttp())
            .WithName("ListarVacantes").Produces<Pagina<VacanteResumenDto>>();

        g.MapPost("/vacantes", async (SolicitarVacanteRequest body, ICommandHandler<SolicitarVacanteCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new SolicitarVacanteCommand(body.Frase, body.EmpresaUsuariaId, body.CargoId), ct);
                return r.IsSuccess ? Results.Created($"/api/v1/reclutamiento/vacantes/{r.Value}", new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("SolicitarVacante").Produces<CreadoResponse>(StatusCodes.Status201Created);

        // La persona llena el formulario sin la IA: nace como borrador confirmado por ella
        g.MapPost("/vacantes/manual", async (CrearVacanteRequest b, ICommandHandler<CrearVacanteCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CrearVacanteCommand(b.EmpresaUsuariaId, b.CargoId, b.Titulo, b.Cargo, b.Ciudad, b.CodigoMunicipio, b.Cupos, b.Jornada,
                    b.Descripcion, b.SalarioMinimo, b.SalarioMaximo, b.Requisitos, b.PreguntasFiltro, b.EmpresaConfidencial, b.SalarioOculto), ct);
                return r.IsSuccess ? Results.Created($"/api/v1/reclutamiento/vacantes/{r.Value}", new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CrearVacante").Produces<CreadoResponse>(StatusCodes.Status201Created);

        // Otro borrador de la IA con una frase nueva (por ejemplo, la respuesta a lo que preguntó)
        g.MapPost("/vacantes/{id:guid}/redactar", async (Guid id, RedactarVacanteRequest b, ICommandHandler<RedactarVacanteCommand> h, CancellationToken ct) =>
                (await h.Handle(new RedactarVacanteCommand(id, b.Frase), ct)).ToHttp())
            .WithName("RedactarVacante");

        // Catálogo de cargos para elegir de cuál parte la vacante (sin pedir permiso de configurar la estructura)
        g.MapGet("/cargos", async (string? buscar, IQueryHandler<BuscarCargosCatalogoQuery, IReadOnlyList<CargoCatalogoDto>> h, CancellationToken ct) =>
                (await h.Handle(new BuscarCargosCatalogoQuery(buscar), ct)).ToHttp())
            .WithName("BuscarCargosCatalogo").Produces<IReadOnlyList<CargoCatalogoDto>>();

        g.MapGet("/vacantes/{id:guid}", async (Guid id, IQueryHandler<ObtenerVacanteQuery, VacanteDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerVacanteQuery(id), ct)).ToHttp())
            .WithName("ObtenerVacante").Produces<VacanteDto>();

        g.MapPut("/vacantes/{id:guid}", async (Guid id, EditarVacanteRequest b, ICommandHandler<EditarVacanteCommand> h, CancellationToken ct) =>
                (await h.Handle(new EditarVacanteCommand(id, b.Titulo, b.Cargo, b.Ciudad, b.Cupos, b.Jornada, b.Descripcion,
                    b.SalarioMinimo, b.SalarioMaximo, b.Requisitos, b.PreguntasFiltro, b.CodigoMunicipio, b.EmpresaConfidencial, b.SalarioOculto), ct)).ToHttp())
            .WithName("EditarVacante");

        // Requisición: líder → RRHH → gerencia; la última aprobación la publica en el portal y busca en el banco de talento
        // (la búsqueda es otro comando: si la vacante aún no queda publicada, no hace nada)
        g.MapPost("/vacantes/{id:guid}/solicitar-aprobacion", async (Guid id, ICommandHandler<SolicitarAprobacionVacanteCommand> h,
                ICommandHandler<BuscarEnBancoCommand, int> banco, CancellationToken ct) =>
                (await AlPublicar(await h.Handle(new SolicitarAprobacionVacanteCommand(id), ct), banco, id, ct)).ToHttp())
            .WithName("SolicitarAprobacionVacante");

        g.MapPost("/vacantes/{id:guid}/aprobar", async (Guid id, ICommandHandler<AprobarRequisicionCommand> h, ICommandHandler<BuscarEnBancoCommand, int> banco,
                CancellationToken ct) =>
                (await AlPublicar(await h.Handle(new AprobarRequisicionCommand(id), ct), banco, id, ct)).ToHttp())
            .WithName("AprobarRequisicion").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/vacantes/{id:guid}/devolver", async (Guid id, DevolverRequisicionRequest b, ICommandHandler<DevolverRequisicionCommand> h, CancellationToken ct) =>
                (await h.Handle(new DevolverRequisicionCommand(id, b.Motivo), ct)).ToHttp())
            .WithName("DevolverRequisicion").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/vacantes/{id:guid}/cerrar", async (Guid id, ICommandHandler<CerrarVacanteCommand> h, CancellationToken ct) =>
                (await h.Handle(new CerrarVacanteCommand(id), ct)).ToHttp())
            .WithName("CerrarVacante");

        // Ranking explicable: la IA califica (en segundo plano) y una persona ajusta con motivo
        g.MapPost("/vacantes/{id:guid}/afinidad", async (Guid id, bool? todas, ICommandHandler<CalcularAfinidadVacanteCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new CalcularAfinidadVacanteCommand(id, todas ?? false), ct);
                return r.IsSuccess ? Results.Ok(new AfinidadPedidaResponse(r.Value)) : r.ToHttp();
            })
            .WithName("CalcularAfinidad").Produces<AfinidadPedidaResponse>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPut("/postulaciones/{id:guid}/afinidad", async (Guid id, AjustarAfinidadRequest b, ICommandHandler<AjustarAfinidadCommand> h, CancellationToken ct) =>
                (await h.Handle(new AjustarAfinidadCommand(id, b.Puntaje, b.Motivo), ct)).ToHttp())
            .WithName("AjustarAfinidad").RequireAuthorization(Politicas.VerHojasDeVida);

        // Tablero de la vacante y terna: la IA propone, una persona aprueba en la bandeja
        g.MapGet("/vacantes/{id:guid}/tablero", async (Guid id, int? porColumna, IQueryHandler<ObtenerTableroQuery, TableroDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerTableroQuery(id, porColumna), ct)).ToHttp())
            .WithName("TableroVacante").Produces<TableroDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPut("/postulaciones/{id:guid}/etapa", async (Guid id, MoverPostulacionRequest b, ICommandHandler<MoverPostulacionCommand> h, CancellationToken ct) =>
                (await h.Handle(new MoverPostulacionCommand(id, b.Etapa, b.Motivo), ct)).ToHttp())
            .WithName("MoverPostulacion").RequireAuthorization(Politicas.VerHojasDeVida);

        // Proceso de selección: embudo, reglas de paso y el siguiente paso de cada persona (la IA sugiere, una persona decide)
        g.MapGet("/vacantes/{id:guid}/proceso", async (Guid id, string? etapa, bool? porDecidir, string? buscar, int? pagina, int? tamano,
                IQueryHandler<ObtenerProcesoQuery, ProcesoDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerProcesoQuery(id, etapa, porDecidir ?? false, buscar, pagina ?? 1, tamano ?? 10), ct)).ToHttp())
            .WithName("ProcesoVacante").Produces<ProcesoDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/postulaciones/{id:guid}/avanzar", async (Guid id, AvanzarPostulacionRequest b, ICommandHandler<AvanzarPostulacionCommand, AvanceDto> h, CancellationToken ct) =>
                (await h.Handle(new AvanzarPostulacionCommand(id, b.Destino, b.Motivo), ct)).ToHttp())
            .WithName("AvanzarPostulacion").Produces<AvanceDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/vacantes/{id:guid}/proceso/sugerencias", async (Guid id, AplicarSugerenciasRequest b, ICommandHandler<AplicarSugerenciasCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AplicarSugerenciasCommand(id, b.PostulacionIds), ct);
                return r.IsSuccess ? Results.Ok(new SugerenciasAplicadasResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AplicarSugerencias").Produces<SugerenciasAplicadasResponse>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPut("/vacantes/{id:guid}/proceso/reglas", async (Guid id, ReglasAvanceRequest b, ICommandHandler<ConfigurarReglasAvanceCommand> h, CancellationToken ct) =>
                (await h.Handle(new ConfigurarReglasAvanceCommand(id, b.Modo, b.UmbralAfinidad, b.ToleranciaSalarial), ct)).ToHttp())
            .WithName("ReglasAvance").RequireAuthorization(Politicas.DeAccion("CrearVacantes"));

        g.MapPost("/vacantes/{id:guid}/terna/proponer", async (Guid id, ICommandHandler<ProponerTernaCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new ProponerTernaCommand(id), ct);
                return r.IsSuccess ? Results.Ok(new TernaPropuestaResponse(r.Value)) : r.ToHttp();
            })
            .WithName("ProponerTerna").Produces<TernaPropuestaResponse>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/vacantes/{id:guid}/terna", async (Guid id, string? postulaciones, ICommandHandler<AprobarTernaCommand> h, CancellationToken ct) =>
                (await h.Handle(new AprobarTernaCommand(id, [.. (postulaciones ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty)]), ct)).ToHttp())
            .WithName("AprobarTerna").RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        // Filtros ATS: qué es excluyente, cómo queda cada postulante y descartes sugeridos que aprueba una persona
        g.MapGet("/vacantes/{id:guid}/filtros", async (Guid id, IQueryHandler<ObtenerFiltrosQuery, FiltrosDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerFiltrosQuery(id), ct)).ToHttp())
            .WithName("FiltrosVacante").Produces<FiltrosDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPut("/vacantes/{id:guid}/filtros", async (Guid id, GuardarFiltrosRequest b, ICommandHandler<GuardarFiltrosCommand> h, CancellationToken ct) =>
                (await h.Handle(new GuardarFiltrosCommand(id, b.Requisitos ?? [], b.Preguntas ?? [], b.Ciudades ?? [], b.ExperienciaMinimaMeses), ct)).ToHttp())
            .WithName("GuardarFiltros").RequireAuthorization(Politicas.DeAccion("CrearVacantes"));

        g.MapGet("/vacantes/{id:guid}/filtros/postulantes", async (Guid id, string? resultado, int? pagina, int? tamano,
                IQueryHandler<ListarEvaluacionFiltrosQuery, Pagina<EvaluacionPostulanteDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarEvaluacionFiltrosQuery(id, resultado, pagina, tamano), ct)).ToHttp())
            .WithName("EvaluacionFiltros").Produces<Pagina<EvaluacionPostulanteDto>>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/vacantes/{id:guid}/filtros/descartes", async (Guid id, ICommandHandler<ProponerDescartesCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new ProponerDescartesCommand(id), ct);
                return r.IsSuccess ? Results.Ok(new DescartesResponse(r.Value)) : r.ToHttp();
            })
            .WithName("ProponerDescartes").Produces<DescartesResponse>().RequireAuthorization(Politicas.VerHojasDeVida);

        // Sin cuerpo (desde la bandeja) descarta a todos los sugeridos; con lista, solo a esos
        g.MapPost("/vacantes/{id:guid}/filtros/descartes/aprobar", async (Guid id, AprobarDescartesRequest? b, ICommandHandler<AprobarDescartesCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AprobarDescartesCommand(id, b?.PostulacionIds), ct);
                return r.IsSuccess ? Results.Ok(new DescartesResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AprobarDescartes").Produces<DescartesResponse>().RequireAuthorization(Politicas.DeAccion("AprobarBandeja"));

        g.MapPost("/vacantes/{id:guid}/filtros/rescatar/{postulacionId:guid}", async (Guid id, Guid postulacionId, ICommandHandler<RescatarPostulacionCommand> h, CancellationToken ct) =>
                (await h.Handle(new RescatarPostulacionCommand(id, postulacionId), ct)).ToHttp())
            .WithName("RescatarPostulacion").RequireAuthorization(Politicas.VerHojasDeVida);

        // Recontacto del banco de talento: las reglas encuentran, la IA califica y una persona decide a quién invitar
        g.MapGet("/vacantes/{id:guid}/banco", async (Guid id, int? pagina, int? tamano, IQueryHandler<ListarSugerenciasBancoQuery, Pagina<SugerenciaBancoDto>> h,
                CancellationToken ct) =>
                (await h.Handle(new ListarSugerenciasBancoQuery(id, pagina, tamano), ct)).ToHttp())
            .WithName("SugerenciasBanco").Produces<Pagina<SugerenciaBancoDto>>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/vacantes/{id:guid}/banco/buscar", async (Guid id, ICommandHandler<BuscarEnBancoCommand, int> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new BuscarEnBancoCommand(id), ct);
                return r.IsSuccess ? Results.Ok(new SugeridosBancoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("BuscarEnBanco").Produces<SugeridosBancoResponse>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/banco/sugerencias/{id:guid}/invitar", async (Guid id, ICommandHandler<InvitarDesdeBancoCommand, InvitacionBancoDto> h, CancellationToken ct) =>
                (await h.Handle(new InvitarDesdeBancoCommand(id), ct)).ToHttp())
            .WithName("InvitarDesdeBanco").Produces<InvitacionBancoDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/banco/sugerencias/{id:guid}/no-encaja", async (Guid id, NoEncajaRequest b, ICommandHandler<DescartarSugerenciaBancoCommand> h, CancellationToken ct) =>
                (await h.Handle(new DescartarSugerenciaBancoCommand(id, b.Motivo), ct)).ToHttp())
            .WithName("DescartarSugerenciaBanco").RequireAuthorization(Politicas.VerHojasDeVida);

        // Preselección conversacional: invitar, ver la conversación con su resumen y responder como persona
        g.MapPost("/postulaciones/{id:guid}/preseleccion", async (Guid id, ICommandHandler<InvitarPreseleccionCommand, EnlacePreseleccionDto> h, CancellationToken ct) =>
                (await h.Handle(new InvitarPreseleccionCommand(id), ct)).ToHttp())
            .WithName("InvitarPreseleccion").Produces<EnlacePreseleccionDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapGet("/postulaciones/{id:guid}/preseleccion", async (Guid id, IQueryHandler<ObtenerConversacionQuery, ConversacionDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerConversacionQuery(id), ct)).ToHttp())
            .WithName("ConversacionPreseleccion").Produces<ConversacionDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapPost("/preseleccion/{id:guid}/mensajes", async (Guid id, RespuestaPersonaRequest b, ICommandHandler<ResponderPreseleccionCommand> h, CancellationToken ct) =>
                (await h.Handle(new ResponderPreseleccionCommand(id, b.Texto, b.Cerrar), ct)).ToHttp())
            .WithName("ResponderPreseleccion").RequireAuthorization(Politicas.VerHojasDeVida);

        // Informe comparativo de finalistas: hoja de vida, afinidad, preselección y pruebas lado a lado
        g.MapGet("/vacantes/{id:guid}/finalistas", async (Guid id, string? postulaciones, IQueryHandler<ObtenerComparativoFinalistasQuery, ComparativoFinalistasDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerComparativoFinalistasQuery(id, string.IsNullOrWhiteSpace(postulaciones) ? null
                    : [.. postulaciones.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty)]), ct)).ToHttp())
            .WithName("ComparativoFinalistas").Produces<ComparativoFinalistasDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        // Retroalimentación del reclutador (calibra el ranking) y reporte de sesgo de la vacante
        g.MapPut("/postulaciones/{id:guid}/retroalimentacion", async (Guid id, RetroalimentarRequest b, ICommandHandler<RetroalimentarPostulacionCommand> h, CancellationToken ct) =>
                (await h.Handle(new RetroalimentarPostulacionCommand(id, b.Valor, b.Nota), ct)).ToHttp())
            .WithName("RetroalimentarPostulacion").RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapGet("/vacantes/{id:guid}/sesgo", async (Guid id, IQueryHandler<ObtenerReporteSesgoQuery, ReporteSesgoDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerReporteSesgoQuery(id), ct)).ToHttp())
            .WithName("ReporteSesgo").Produces<ReporteSesgoDto>().RequireAuthorization(Politicas.VerHojasDeVida);

        g.MapGet("/vacantes/{id:guid}/postulantes", async (Guid id, int? pagina, int? tamano, string? buscar, string? canal,
                IQueryHandler<ListarPostulantesQuery, Pagina<PostulanteDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarPostulantesQuery(id, pagina, tamano, buscar, canal), ct)).ToHttp())
            .WithName("ListarPostulantes").Produces<Pagina<PostulanteDto>>();
    }

    /// <summary>Si el comando salió bien, busca en el banco (solo actúa si la vacante quedó publicada). La respuesta es la del comando.</summary>
    private static async Task<Result> AlPublicar(Result r, ICommandHandler<BuscarEnBancoCommand, int> banco, Guid vacanteId, CancellationToken ct)
    {
        if (r.IsSuccess)
        {
            await banco.Handle(new BuscarEnBancoCommand(vacanteId, AlPublicar: true), ct);
        }

        return r;
    }

    private static void MapPortal(RouteGroupBuilder g)
    {
        g.MapGet("/vacantes", async (int? pagina, int? tamano, string? buscar, string? ciudad, string? jornada,
                IQueryHandler<ListarVacantesPublicasQuery, PortalDto> h, CancellationToken ct) =>
                (await h.Handle(new ListarVacantesPublicasQuery(pagina, tamano, buscar, ciudad, jornada), ct)).ToHttp())
            .WithName("PortalVacantes").Produces<PortalDto>();

        g.MapGet("/vacantes/{codigo}", async (string codigo, IQueryHandler<ObtenerVacantePublicaQuery, VacantePublicaDetalleDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerVacantePublicaQuery(codigo), ct)).ToHttp())
            .WithName("PortalVacante").Produces<VacantePublicaDetalleDto>();

    }

    /// <summary>Postularse exige la cuenta del portal (ADR 0013): la postulación sale con el correo y la identificación de la persona.</summary>
    private static void MapPostularse(RouteGroupBuilder conCuenta)
    {
        conCuenta.MapPost("/vacantes/{codigo}/postulaciones", async (string codigo, IFormFile hojaDeVida,
                ICommandHandler<IniciarPostulacionCommand, Guid> h, CancellationToken ct) =>
            {
                await using var flujo = hojaDeVida.OpenReadStream();
                using var memoria = new MemoryStream();
                await flujo.CopyToAsync(memoria, ct);
                var r = await h.Handle(new IniciarPostulacionCommand(codigo, hojaDeVida.FileName, TiposArchivo.PorExtension(hojaDeVida.FileName, hojaDeVida.ContentType),
                    memoria.ToArray()), ct);
                return r.IsSuccess ? Results.Created($"/api/v1/portal/postulaciones/{r.Value}", new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("IniciarPostulacion").DisableAntiforgery().Produces<CreadoResponse>(StatusCodes.Status201Created);

        conCuenta.MapGet("/postulaciones/{borradorId:guid}", async (Guid borradorId, IQueryHandler<ObtenerBorradorPostulacionQuery, BorradorPostulacionDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerBorradorPostulacionQuery(borradorId), ct)).ToHttp())
            .WithName("PortalBorradorPostulacion").Produces<BorradorPostulacionDto>();

        conCuenta.MapPost("/postulaciones/{borradorId:guid}/enviar", async (Guid borradorId, EnviarPostulacionRequest b, HttpContext http,
                ICommandHandler<EnviarPostulacionCommand, string> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new EnviarPostulacionCommand(borradorId, b.Nombre, b.Documento, b.Celular, b.Correo, b.Ciudad,
                    b.Respuestas ?? [], b.AceptaAutorizacion, b.Canal, http.Connection.RemoteIpAddress?.ToString() ?? "",
                    http.Request.Headers.UserAgent.ToString(), b.LinkedIn, b.Valores), ct);
                return r.IsSuccess ? Results.Ok(new PostulacionEnviadaResponse(r.Value)) : r.ToHttp();
            })
            .WithName("EnviarPostulacion").Produces<PostulacionEnviadaResponse>();
    }
}

/// <summary>Algunos navegadores (y clientes como PowerShell) mandan el Word o el PDF sin tipo: se deduce por la extensión.</summary>
internal static class TiposArchivo
{
    public static string PorExtension(string nombre, string tipoRecibido) => Path.GetExtension(nombre).ToUpperInvariant() switch
    {
        ".PDF" => "application/pdf",
        ".DOCX" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".JPG" or ".JPEG" => "image/jpeg",
        ".PNG" => "image/png",
        ".WEBP" => "image/webp",
        _ => tipoRecibido,
    };
}
