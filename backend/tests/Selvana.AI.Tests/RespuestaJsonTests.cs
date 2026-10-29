using FluentAssertions;
using Microsoft.Extensions.AI;
using Selvana.AI.Gateway;

namespace Selvana.AI.Tests;

public class RespuestaJsonTests
{
    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("Aquí está la hoja de vida:\n{\"a\":{\"b\":2}}\nEspero que sirva.", "{\"a\":{\"b\":2}}")]
    [InlineData("sin json", "sin json")]
    public void DejaSoloElJson(string texto, string esperado) => RespuestaJson.ExtraerJson(texto).Should().Be(esperado);

    [Fact]
    public void LimpiaElUltimoMensajeDelAsistente()
    {
        var respuesta = new ChatResponse(new ChatMessage(ChatRole.Assistant, "```json\n{\"nombre\":\"Ana\"}\n```"));

        RespuestaJson.SoloJson(respuesta);

        respuesta.Text.Should().Be("{\"nombre\":\"Ana\"}");
    }
}
