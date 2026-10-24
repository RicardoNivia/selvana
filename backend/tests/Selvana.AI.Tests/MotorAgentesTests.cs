using FluentAssertions;
using Microsoft.Extensions.AI;
using Selvana.AI.Gateway;
using Selvana.AI.Motor;

namespace Selvana.AI.Tests;

public sealed class MotorAgentesTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly SolicitudAgente Solicitud = new(Empresa, "vacante:42", "¿Cuántos cupos faltan?");

    private readonly MemoriasEnMemoria _memorias = new();
    private readonly AccionesEnMemoria _acciones = new();
    private readonly RelojManual _reloj = new(new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero));
    private int _escrituras;

    [Fact]
    public async Task EjecutaLasHerramientasDeLecturaYDevuelveLaRespuesta()
    {
        var modelo = new ModeloGuionado(Llamar("cupos_pendientes", ("vacanteId", 42)), Responder("Faltan 3 cupos."));

        var r = await Motor(modelo).EjecutarAsync(Agente(), Solicitud, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Respuesta.Should().Be("Faltan 3 cupos.");
        r.Value.Pasos.Should().Be(2);
        modelo.Recibidos[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result!.ToString().Should().Be("3");
        _acciones.Todas.Select(a => a.Tipo).Should().Equal(TipoAccionAgente.Modelo, TipoAccionAgente.Herramienta, TipoAccionAgente.Modelo, TipoAccionAgente.Respuesta);
        _acciones.Todas.Should().OnlyContain(a => a.EjecucionId == r.Value.EjecucionId && a.Caso == "vacante:42");
    }

    [Fact]
    public async Task LasHerramientasDeEscrituraNoSeEjecutanQuedanPropuestas()
    {
        var modelo = new ModeloGuionado(Llamar("cerrar_vacante", ("motivo", "cupos completos")), Responder("Propuse cerrar la vacante."));

        var r = await Motor(modelo).EjecutarAsync(Agente(), Solicitud, CancellationToken.None);

        _escrituras.Should().Be(0, "la IA propone y una persona decide");
        r.Value.Propuestas.Should().ContainSingle().Which.Herramienta.Should().Be("cerrar_vacante");
        _acciones.Todas.Should().Contain(a => a.Tipo == TipoAccionAgente.Propuesta && a.Detalle.Contains("cupos completos"));
    }

    [Fact]
    public async Task RecuerdaHechosDelCasoParaLaSiguienteEjecucion()
    {
        var primera = new ModeloGuionado(Llamar(MotorAgentes.HerramientaRecordar, ("clave", "cupos"), ("valor", "faltan 3")), Responder("Listo."));
        await Motor(primera).EjecutarAsync(Agente(), Solicitud, CancellationToken.None);

        var segunda = new ModeloGuionado(Responder("Siguen faltando 3."));
        var r = await Motor(segunda).EjecutarAsync(Agente(), Solicitud, CancellationToken.None);

        r.Value.Memoria.Should().ContainKey("cupos").WhoseValue.Should().Be("faltan 3");
        segunda.Recibidos[0][0].Text.Should().Contain("- cupos: faltan 3");
    }

    [Fact]
    public async Task SiNoTerminaEnLosPasosPermitidosFallaYDejaLaBitacora()
    {
        var modelo = new ModeloGuionado(Llamar("cupos_pendientes", ("vacanteId", 42)), Llamar("cupos_pendientes", ("vacanteId", 42)));

        var r = await Motor(modelo).EjecutarAsync(Agente() with { MaximoPasos = 2 }, Solicitud, CancellationToken.None);

        r.Error.Should().Be(MotorAgentesErrors.DemasiadosPasos);
        _acciones.Todas.Should().HaveCount(5).And.Contain(a => a.Tipo == TipoAccionAgente.Error);
    }

    [Fact]
    public async Task DeclaraSiElAgenteTrabajaConDatosPersonales()
    {
        var modelo = new ModeloGuionado(Responder("ok"));

        await Motor(modelo).EjecutarAsync(Agente() with { ContieneDatosPersonales = true }, Solicitud, CancellationToken.None);

        ContextoIa.De(modelo.Opciones[0]).Should().Be(new ContextoIa(Empresa, "agente-prueba", ContieneDatosPersonales: true));
    }

    private MotorAgentes Motor(IChatClient modelo) => new(modelo, _memorias, _acciones, _reloj);

    private DefinicionAgente Agente() => new("agente-prueba", "Eres un agente de prueba.",
    [
        HerramientaAgente.Lectura((int vacanteId) => vacanteId == 42 ? 3 : 0, "cupos_pendientes", "Cupos que faltan por llenar"),
        HerramientaAgente.Escritura((string motivo) => _escrituras++, "cerrar_vacante", "Cierra la vacante"),
    ], ContieneDatosPersonales: false);

    private static ChatResponse Responder(string texto) => new(new ChatMessage(ChatRole.Assistant, texto));

    private static ChatResponse Llamar(string herramienta, params (string Nombre, object Valor)[] argumentos) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString(), herramienta,
            argumentos.ToDictionary(a => a.Nombre, a => (object?)a.Valor))]));

    /// <summary>Modelo que responde en el orden del guion y guarda lo que recibió en cada llamada.</summary>
    private sealed class ModeloGuionado(params ChatResponse[] guion) : IChatClient
    {
        private int _siguiente;

        public List<List<ChatMessage>> Recibidos { get; } = [];
        public List<ChatOptions?> Opciones { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Recibidos.Add([.. messages]);
            Opciones.Add(options);
            return Task.FromResult(guion[_siguiente++]);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No se usa en las pruebas.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class MemoriasEnMemoria : IMemoriaCasos
    {
        private readonly Dictionary<string, MemoriaCaso> _memorias = [];

        public Task<MemoriaCaso?> BuscarAsync(Guid empresaId, string agente, string caso, CancellationToken cancellationToken) =>
            Task.FromResult(_memorias.GetValueOrDefault($"{empresaId}/{agente}/{caso}"));

        public Task GuardarAsync(MemoriaCaso memoria, CancellationToken cancellationToken)
        {
            _memorias[$"{memoria.EmpresaId}/{memoria.Agente}/{memoria.Caso}"] = memoria;
            return Task.CompletedTask;
        }
    }

    private sealed class AccionesEnMemoria : IRegistroAccionesAgente
    {
        public List<AccionAgente> Todas { get; } = [];

        public Task RegistrarAsync(IReadOnlyList<AccionAgente> acciones, CancellationToken cancellationToken)
        {
            Todas.AddRange(acciones);
            return Task.CompletedTask;
        }
    }
}
