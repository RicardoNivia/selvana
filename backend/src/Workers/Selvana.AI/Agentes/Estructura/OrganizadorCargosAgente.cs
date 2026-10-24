using Microsoft.Extensions.AI;
using Selvana.AI.Gateway;
using Selvana.Core.Contracts.Estructura;
using Selvana.SharedKernel.Results;

namespace Selvana.AI.Agentes.Estructura;

public sealed record UnionCargos(string NombreFinal, IReadOnlyList<string> Codigos, string Porque);

public sealed record RespuestaOrganizador(IReadOnlyList<UnionCargos> Uniones);

public sealed record ResultadoOrganizador(IReadOnlyList<UnionCargos> Uniones, string Modelo);

public interface IOrganizadorCargos
{
    Task<Result<ResultadoOrganizador>> ProponerUnionesAsync(Guid empresaId, IReadOnlyList<CargoLeido> cargos, CancellationToken cancellationToken);
}

/// <summary>
/// Agente organizador: encuentra nombres de cargo de la nómina que son el mismo cargo escrito distinto.
/// Los nombres de cargo no son datos personales, así que puede usar los modelos gratis.
/// </summary>
internal sealed class OrganizadorCargosAgente(IChatClient ia) : IOrganizadorCargos
{
    public const string Nombre = "organizador-estructura";

    private const string Instrucciones = """
        Eres el agente organizador de Selvana, un software de talento humano para empresas colombianas.
        Recibes la lista de cargos de una nómina: código, nombre, área y cuántos empleados lo tienen.
        Encuentra los grupos de nombres que son EL MISMO cargo escrito de formas distintas: abreviaturas (AUX, ASIST),
        mayúsculas, tildes, el nombre de una sede o ciudad pegado al cargo, o sinónimos evidentes.

        Reglas:
        - No unas cargos distintos aunque se parezcan: auxiliar no es jefe ni coordinador; asesor no es supervisor; niveles I, II y III son distintos.
        - Cada grupo tiene al menos 2 códigos y un código aparece en un solo grupo.
        - Si no hay nombres repetidos, devuelve la lista vacía.
        - "nombreFinal" es el nombre bien escrito en español de Colombia, con tildes y mayúscula inicial, sin sede ni ciudad.
          Si el cargo lo puede ocupar cualquier persona, usa la forma inclusiva: "Asesor(a) de servicio al cliente".
        - "porque" es una sola frase corta que explica por qué son el mismo cargo.
        """;

    public async Task<Result<ResultadoOrganizador>> ProponerUnionesAsync(Guid empresaId, IReadOnlyList<CargoLeido> cargos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cargos);
        if (cargos.Count < 2)
        {
            return new ResultadoOrganizador([], "");
        }

        var lista = string.Join("\n", cargos.Select(c => $"{c.Codigo} | {c.Nombre} | {c.Area} | {c.Empleados} empleados"));
        var mensajes = new List<ChatMessage>
        {
            new(ChatRole.System, Instrucciones),
            new(ChatRole.User, "Cargos (código | nombre | área | empleados):\n" + lista),
        };
        var opciones = new ContextoIa(empresaId, Nombre, ContieneDatosPersonales: false).AplicarA(new ChatOptions { MaxOutputTokens = 16_000 });

        try
        {
            var respuesta = await ia.GetResponseAsync<RespuestaOrganizador>(mensajes, opciones, cancellationToken: cancellationToken);
            if (!respuesta.TryGetResult(out var resultado) || resultado?.Uniones is null)
            {
                return Error.Failure("Ia.RespuestaInvalida", "La IA no devolvió una lista de uniones válida.");
            }

            var codigos = cargos.Select(c => c.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validas = new List<UnionCargos>();
            foreach (var union in resultado.Uniones)
            {
                var propios = union.Codigos.Where(codigos.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (propios.Count >= 2 && !propios.Any(usados.Contains) && !string.IsNullOrWhiteSpace(union.NombreFinal))
                {
                    usados.UnionWith(propios);
                    validas.Add(union with { Codigos = propios });
                }
            }

            return new ResultadoOrganizador(validas, respuesta.ModelId ?? "");
        }
        catch (CadenaModelosAgotadaException)
        {
            return Error.Failure("Ia.SinModelos", "Ningún modelo de IA respondió. Puede aplicar la estructura sin unir cargos o intentar más tarde.");
        }
    }
}
