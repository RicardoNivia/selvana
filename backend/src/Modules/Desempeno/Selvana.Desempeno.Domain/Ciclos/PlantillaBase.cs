namespace Selvana.Desempeno.Domain.Ciclos;

/// <summary>
/// Formulario por reglas, sin IA (camino manual): competencias comunes más las del tipo de cargo y objetivos medibles de base.
/// RRHH lo ajusta y lo aprueba igual que una propuesta de la IA.
/// </summary>
public static class PlantillaBase
{
    private sealed record Familia(string Nombre, string[] Palabras, (string Nombre, string Descripcion)[] Competencias, (string Descripcion, string Meta)[] Objetivos);

    private static readonly Familia[] Familias =
    [
        new("Operación y logística", ["bodega", "logíst", "montacarg", "operario", "alistador", "conductor", "despacho", "producción", "empaque", "mercader"],
            [("Seguridad en el trabajo", "Cumple los procedimientos de SST y usa sus elementos de protección."),
             ("Calidad y precisión", "Hace el trabajo sin errores ni reprocesos."),
             ("Productividad", "Cumple los volúmenes y los tiempos del turno.")],
            [("Cumplir la meta de productividad del turno", "≥ 95 % de cumplimiento mensual"),
             ("Cero accidentes e incidentes por actos inseguros", "0 eventos en el periodo"),
             ("Exactitud de inventarios o despachos", "≥ 98 % sin diferencias")]),
        new("Comercial y servicio", ["asesor", "cajer", "venta", "comercial", "servicio", "contact", "agente", "recepcion"],
            [("Orientación al cliente", "Entiende lo que necesita el cliente y lo resuelve con amabilidad."),
             ("Comunicación", "Explica con claridad y escucha."),
             ("Orientación a metas", "Persigue y cumple las metas comerciales o de servicio.")],
            [("Cumplir la meta de ventas o de atención", "≥ 100 % del presupuesto mensual"),
             ("Satisfacción del cliente", "Calificación ≥ 4,5 de 5"),
             ("Cuadre de caja o cierre de casos sin novedades", "≥ 98 % de los días")]),
        new("Salud", ["enferm", "farmac", "camiller", "médic", "auxiliar de enfermería", "terapeut"],
            [("Seguridad del paciente", "Aplica los protocolos y reporta los eventos adversos."),
             ("Orientación al paciente", "Trata al paciente y a su familia con respeto y empatía."),
             ("Trabajo bajo presión", "Mantiene la calidad en momentos de alta demanda.")],
            [("Cumplir los protocolos de seguridad del paciente", "100 % en las auditorías"),
             ("Registros clínicos completos y a tiempo", "≥ 98 % de las historias"),
             ("Mantener vigentes los cursos obligatorios", "100 % vigentes")]),
        new("Administrativo y profesional", ["analista", "coordinador", "auxiliar administrativ", "nómina", "jefe", "líder", "desarrollador", "director"],
            [("Planeación y organización", "Organiza su trabajo y cumple los plazos."),
             ("Análisis y solución de problemas", "Encuentra la causa y propone soluciones."),
             ("Liderazgo e influencia", "Orienta a otros y logra acuerdos.")],
            [("Cumplir los entregables del área a tiempo", "≥ 95 % en la fecha acordada"),
             ("Mejorar un proceso del área", "1 mejora implementada y medida"),
             ("Calidad de la información que entrega", "0 reprocesos por errores")]),
    ];

    private static readonly (string Nombre, string Descripcion)[] Comunes =
    [
        ("Responsabilidad", "Cumple lo que se compromete y responde por su trabajo."),
        ("Trabajo en equipo", "Colabora con sus compañeros y comparte lo que sabe."),
    ];

    public static (List<Competencia> Competencias, List<ObjetivoBase> Objetivos, List<RazonPropuesta> Razones) Para(string cargo)
    {
        var texto = cargo.ToLowerInvariant();
        var familia = Familias.FirstOrDefault(f => f.Palabras.Any(p => texto.Contains(p, StringComparison.Ordinal))) ?? Familias[^1];
        var nombres = Comunes.Concat(familia.Competencias).ToList();
        // Las del cargo pesan más que las comunes; los pesos siempre suman 100
        var competencias = nombres.Select((c, i) => new Competencia { Nombre = c.Nombre, Descripcion = c.Descripcion, Peso = i < Comunes.Length ? 15 : 0 }).ToList();
        var resto = 100 - competencias.Sum(c => c.Peso);
        var propias = competencias.Count - Comunes.Length;
        for (var i = Comunes.Length; i < competencias.Count; i++)
        {
            var peso = resto / propias + (i - Comunes.Length < resto % propias ? 1 : 0);
            competencias[i] = new Competencia { Nombre = competencias[i].Nombre, Descripcion = competencias[i].Descripcion, Peso = peso };
        }

        var objetivos = familia.Objetivos.Select((o, i) => new ObjetivoBase { Descripcion = o.Descripcion, Meta = o.Meta, Peso = i == 0 ? 40 : 30 }).ToList();
        var razones = new List<RazonPropuesta>
        {
            new() { Texto = $"El cargo «{cargo}» es de la familia {familia.Nombre.ToLowerInvariant()}", Fuente = "Plantilla base" },
            new() { Texto = "Responsabilidad y trabajo en equipo se evalúan en todos los cargos", Fuente = "Plantilla base" },
            new() { Texto = "Objetivos medibles con su meta; ajústelos a la realidad del área", Fuente = "Plantilla base" },
        };
        return (competencias, objetivos, razones);
    }
}
