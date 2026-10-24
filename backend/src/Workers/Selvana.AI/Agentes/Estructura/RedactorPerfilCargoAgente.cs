using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Selvana.AI.Agentes.RedactorVacantes;
using Selvana.AI.Gateway;
using Selvana.Core.Contracts.Estructura;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.Estructura;

public sealed record RazonPerfil(string Signo, string Texto, string Fuente);

public sealed record BorradorPerfilCargo(
    string Mision, IReadOnlyList<string> Funciones, IReadOnlyList<string> Requisitos, string RiesgoArl, IReadOnlyList<RazonPerfil> Razones);

public sealed record ResultadoPerfil(BorradorPerfilCargo Perfil, string Modelo);

public interface IRedactorPerfilCargo
{
    Task<Result<ResultadoPerfil>> RedactarAsync(Guid empresaId, CargoParaPerfil cargo, CancellationToken cancellationToken);
}

/// <summary>Agente que redacta el borrador del perfil de un cargo. RRHH lo revisa y aprueba.</summary>
internal sealed partial class RedactorPerfilCargoAgente(IChatClient ia, IFiltroVariablesProtegidas filtro) : IRedactorPerfilCargo
{
    public const string Nombre = "redactor-perfil-cargo";

    private const string Instrucciones = """
        Eres el agente redactor de perfiles de cargo de Selvana, un software de talento humano para empresas colombianas.
        Redacta el borrador del perfil del cargo que te indican, en español de Colombia, concreto y verificable.

        Reglas:
        - "mision": una frase que diga para qué existe el cargo.
        - "funciones": entre 3 y 6 funciones que empiecen con un verbo en infinitivo.
        - "requisitos": entre 2 y 5 requisitos de formación, experiencia o certificaciones que el cargo exige.
          Si la ley colombiana exige algo para el oficio (por ejemplo ReTHUS en salud, curso de alturas, licencia de conducción), inclúyelo.
        - Nunca pidas edad, género, estado civil, hijos, apariencia, foto, religión, barrio, estrato ni dirección.
        - "riesgoArl": la clase de riesgo de la ARL más probable para el cargo, solo "Clase I", "Clase II", "Clase III", "Clase IV" o "Clase V".
        - "razones": exactamente 3 razones de por qué redactaste el perfil así. "signo" es "+" si es un dato que sostiene el perfil
          y "−" si es algo que RRHH debe revisar. "fuente" es "Nómina" si sale de los datos que te dieron, "Normativa" si sale de una ley
          colombiana (nómbrala en el texto) o "Supuesto" si lo supusiste.
        """;

    public async Task<Result<ResultadoPerfil>> RedactarAsync(Guid empresaId, CargoParaPerfil cargo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        var datos = $"""
            Cargo: {cargo.Nombre}
            Área: {cargo.Area}
            Empleados con este cargo: {cargo.Empleados}
            Nombres con los que aparece en la nómina: {string.Join(", ", cargo.NombresEnNomina)}
            """;
        var mensajes = new List<ChatMessage> { new(ChatRole.System, Instrucciones), new(ChatRole.User, datos) };
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: false).AplicarA(new ChatOptions { MaxOutputTokens = 16_000 });

        try
        {
            var respuesta = await ia.GetResponseAsync<BorradorPerfilCargo>(mensajes, opciones, cancellationToken: cancellationToken);
            if (!respuesta.TryGetResult(out var perfil) || string.IsNullOrWhiteSpace(perfil?.Mision))
            {
                return Error.Failure("Ia.RespuestaInvalida", $"La IA no devolvió un perfil válido para {cargo.Nombre}.");
            }

            var limpio = perfil with
            {
                Requisitos = [.. perfil.Requisitos.Where(r => !filtro.EsDiscriminatorio(r))],
                RiesgoArl = ClaseArl().IsMatch(perfil.RiesgoArl ?? "") ? perfil.RiesgoArl! : "Por definir",
                Razones = [.. perfil.Razones.Take(5).Select(r => r with { Signo = r.Signo is "-" or "−" ? "−" : "+" })],
            };

            return new ResultadoPerfil(limpio, respuesta.ModelId ?? "");
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió.");
        }
    }

    [GeneratedRegex(@"^Clase (I|II|III|IV|V)$")]
    private static partial Regex ClaseArl();
}
