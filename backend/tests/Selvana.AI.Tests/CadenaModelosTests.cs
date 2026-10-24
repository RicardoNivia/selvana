using System.Net;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Selvana.AI.Gateway;

namespace Selvana.AI.Tests;

public sealed class CadenaModelosTests
{
    private static readonly ChatMessage[] Pregunta = [new(ChatRole.User, "hola")];
    private static readonly ContextoIa SinDatosPersonales = new(Guid.NewGuid(), "prueba", ContieneDatosPersonales: false);

    private readonly RegistroEnMemoria _registro = new();
    private readonly RelojManual _reloj = new(new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task UsaElPrimerModeloGratisCuandoResponde()
    {
        var gratis = ModeloFalso.Que("desde gratis");
        var pago = ModeloFalso.Que("desde pago");
        using var cadena = Crear((Gratis("flash-lite"), gratis), (Pago("opus", 4, 20), pago));

        var respuesta = await cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null));

        respuesta.Text.Should().Be("desde gratis");
        respuesta.ModelId.Should().Be("flash-lite");
        pago.Llamadas.Should().Be(0);
        gratis.UltimasOpciones!.ModelId.Should().Be("flash-lite");
    }

    [Fact]
    public async Task PasaAlSiguienteModeloSiElGratisAgotaLaCuotaYLoOmiteDespues()
    {
        var gratis = ModeloFalso.QueFalla(new HttpRequestException("RESOURCE_EXHAUSTED", null, HttpStatusCode.TooManyRequests));
        var pago = ModeloFalso.Que("desde pago");
        using var cadena = Crear((Gratis("flash-lite"), gratis), (Pago("opus", 4, 20), pago));

        var primera = await cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null));
        await cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null));

        primera.Text.Should().Be("desde pago");
        gratis.Llamadas.Should().Be(1, "después de un 429 el modelo se omite durante la espera configurada");
        _registro.Usos.Should().HaveCount(3);
        _registro.Usos[0].Exito.Should().BeFalse();
        _registro.Usos[0].Falla.Should().StartWith(nameof(TipoFalla.CuotaAgotada));
        _registro.Usos[1].Should().Match<UsoModelo>(u => u.Exito && u.Modelo == "opus" && u.Intento == 2);
    }

    [Fact]
    public async Task VuelveAProbarElModeloGratisCuandoPasaLaEspera()
    {
        var gratis = ModeloFalso.QueFalla(new HttpRequestException("cuota", null, HttpStatusCode.TooManyRequests));
        using var cadena = Crear((Gratis("flash-lite"), gratis), (Pago("opus", 4, 20), ModeloFalso.Que("ok")));

        await cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null));
        _reloj.Avanzar(TimeSpan.FromMinutes(11));
        await cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null));

        gratis.Llamadas.Should().Be(2);
    }

    [Fact]
    public async Task NuncaEnviaDatosPersonalesAUnModeloGratis()
    {
        var gratis = ModeloFalso.Que("no debería responder");
        var pago = ModeloFalso.Que("desde pago");
        using var cadena = Crear((Gratis("flash-lite"), gratis), (Pago("opus", 4, 20), pago));
        var conHojaDeVida = new ContextoIa(Guid.NewGuid(), "lector-hv", ContieneDatosPersonales: true);

        var respuesta = await cadena.GetResponseAsync(Pregunta, conHojaDeVida.AplicarA(null));

        respuesta.Text.Should().Be("desde pago");
        gratis.Llamadas.Should().Be(0);
    }

    [Fact]
    public async Task SinContextoSeAsumeQueHayDatosPersonales()
    {
        var gratis = ModeloFalso.Que("gratis");
        using var cadena = Crear((Gratis("flash-lite"), gratis), (Pago("opus", 4, 20), ModeloFalso.Que("pago")));

        var respuesta = await cadena.GetResponseAsync(Pregunta);

        respuesta.Text.Should().Be("pago");
        gratis.Llamadas.Should().Be(0);
    }

    [Fact]
    public async Task RegistraElCostoPorEmpresaYAgente()
    {
        using var cadena = Crear((Pago("opus", 4, 20), ModeloFalso.Que("ok", entrada: 1_000_000, salida: 500_000)));

        var respuesta = await cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null));

        var uso = _registro.Usos.Single();
        uso.CostoUsd.Should().Be(14m);
        uso.EmpresaId.Should().Be(SinDatosPersonales.EmpresaId);
        uso.Agente.Should().Be("prueba");
        respuesta.AdditionalProperties![CadenaModelosChatClient.PropiedadCostoUsd].Should().Be(14m);
    }

    [Fact]
    public async Task FallaConElDetalleDeCadaIntentoSiNingunModeloResponde()
    {
        using var cadena = Crear(
            (Gratis("flash-lite"), ModeloFalso.QueFalla(new HttpRequestException("no existe", null, HttpStatusCode.NotFound))),
            (Pago("opus", 4, 20), ModeloFalso.QueFalla(new HttpRequestException("caído", null, HttpStatusCode.ServiceUnavailable))));

        var accion = () => cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null));

        var error = await accion.Should().ThrowAsync<CadenaModelosAgotadaException>();
        error.Which.Fallas.Should().HaveCount(2);
        error.Which.Fallas[0].Should().Contain(nameof(TipoFalla.ModeloNoDisponible));
        error.Which.Fallas[1].Should().Contain(nameof(TipoFalla.Transitoria));
    }

    [Fact]
    public void QuitaElRazonamientoDeOtroModeloYConservaLasLlamadas()
    {
        var llamada = new FunctionCallContent("1", "salario_minimo");
        var deGemini = new ChatMessage(ChatRole.Assistant, [new TextReasoningContent("pensando") { ProtectedData = "firma" }, llamada])
        {
            AdditionalProperties = new() { [CadenaModelosChatClient.PropiedadModelo] = "flash" },
        };
        IList<ChatMessage> mensajes = [.. Pregunta, deGemini];

        var paraHaiku = CadenaModelosChatClient.ParaModelo(mensajes, "haiku");
        var paraFlash = CadenaModelosChatClient.ParaModelo(mensajes, "flash");

        paraHaiku[1].Contents.Should().ContainSingle().Which.Should().BeSameAs(llamada);
        paraFlash.Should().BeSameAs(mensajes, "el mismo modelo sí acepta su propio razonamiento");
    }

    [Fact]
    public async Task RespetaLaCancelacionDelLlamadorSinProbarOtrosModelos()
    {
        var pago = ModeloFalso.Que("ok");
        using var cadena = Crear((Gratis("flash-lite"), ModeloFalso.Que("ok")), (Pago("opus", 4, 20), pago));
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        var accion = () => cadena.GetResponseAsync(Pregunta, SinDatosPersonales.AplicarA(null), cancelado.Token);

        await accion.Should().ThrowAsync<OperationCanceledException>();
        pago.Llamadas.Should().Be(0);
    }

    private CadenaModelosChatClient Crear(params (EslabonModeloOptions Opciones, ModeloFalso Modelo)[] eslabones) =>
        new(
            [.. eslabones.Select(e => new EslabonCadena(e.Opciones, e.Modelo))],
            _registro,
            new ClasificadorFallas(),
            Options.Create(new IaOptions()),
            _reloj,
            NullLogger<CadenaModelosChatClient>.Instance);

    private static EslabonModeloOptions Gratis(string modelo) =>
        new() { Proveedor = "Gemini", Modelo = modelo, Nivel = NivelModelo.Gratis };

    private static EslabonModeloOptions Pago(string modelo, decimal entrada, decimal salida) =>
        new() { Proveedor = "Anthropic", Modelo = modelo, Nivel = NivelModelo.Pago, PrecioEntradaUsdPorMillon = entrada, PrecioSalidaUsdPorMillon = salida };
}
