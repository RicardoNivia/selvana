namespace Selvana.SharedKernel.Results;

/// <summary>Una página de resultados: toda consulta de listado devuelve esto (docs/arquitectura/backend.md, CQRS).</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Items, int Numero, int Tamano, int Total)
{
    public int TotalPaginas => Tamano == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);
}

/// <summary>Qué página se pide. Normaliza valores fuera de rango: página mínima 1, tamaño entre 1 y 100 (20 por defecto).</summary>
public readonly record struct PaginaSolicitada
{
    public const int TamanoPorDefecto = 10;
    public const int TamanoMaximo = 100;

    public PaginaSolicitada(int? numero, int? tamano)
    {
        Numero = Math.Max(1, numero ?? 1);
        Tamano = Math.Clamp(tamano ?? TamanoPorDefecto, 1, TamanoMaximo);
    }

    public int Numero { get; }
    public int Tamano { get; }
    public int Omitir => (Numero - 1) * Tamano;
}
