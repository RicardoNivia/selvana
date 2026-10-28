using Selvana.SharedKernel.Domain;
using Selvana.Vinculacion.Domain.Procesos;

namespace Selvana.Vinculacion.Domain.TiposContrato;

/// <summary>
/// Tipo de contrato del maestro de la empresa: el nombre que usa la empresa, la modalidad legal que activa las reglas
/// (<see cref="ReglasContrato"/>) y la descripción de cómo se trabaja ese contrato, que la IA sigue al sugerir condiciones
/// y al redactar la oferta y el contrato. Se siembra con los tipos por defecto y RRHH lo edita.
/// </summary>
public sealed class TipoContratoEmpresa : AggregateRoot<Guid>
{
    public const int NombreMaximo = 80;
    public const int DescripcionMaxima = 2_000;

    private TipoContratoEmpresa(Guid id, Guid empresaId) : base(id) => EmpresaId = empresaId;

    public Guid EmpresaId { get; private init; }
    public string Nombre { get; private set; } = "";
    public TipoContrato Modalidad { get; private set; }

    /// <summary>Cómo se trabaja este contrato en la empresa; va a la IA tal cual.</summary>
    public string Descripcion { get; private set; } = "";
    public bool Activo { get; private set; } = true;

    /// <summary>Vino de la semilla y nadie lo ha cambiado.</summary>
    public bool PorDefecto { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }
    public string ActualizadoPor { get; private set; } = "";

    /// <summary>Contrato de ejemplo o plantilla (PDF o Word, cifrado en el almacén del módulo) que la IA sigue al redactar.</summary>
    public Guid? PlantillaArchivoId { get; private set; }
    public string? PlantillaNombre { get; private set; }
    public string? PlantillaTipoContenido { get; private set; }

    public void PonerPlantilla(Guid archivoId, string nombre, string tipoContenido, string por, DateTimeOffset ahora)
    {
        PlantillaArchivoId = archivoId;
        PlantillaNombre = nombre;
        PlantillaTipoContenido = tipoContenido;
        Tocar(por, ahora);
    }

    public void QuitarPlantilla(string por, DateTimeOffset ahora)
    {
        PlantillaArchivoId = null;
        PlantillaNombre = null;
        PlantillaTipoContenido = null;
        Tocar(por, ahora);
    }

    private void Tocar(string por, DateTimeOffset ahora)
    {
        ActualizadoPor = por;
        ActualizadoEn = ahora;
        PorDefecto = false;
    }

    public static TipoContratoEmpresa Crear(Guid empresaId, string nombre, TipoContrato modalidad, string descripcion, string por, DateTimeOffset ahora)
    {
        var tipo = new TipoContratoEmpresa(Guid.CreateVersion7(), empresaId);
        tipo.Poner(nombre, modalidad, descripcion, activo: true, por, ahora);
        return tipo;
    }

    public void Editar(string nombre, TipoContrato modalidad, string descripcion, bool activo, string por, DateTimeOffset ahora)
    {
        Poner(nombre, modalidad, descripcion, activo, por, ahora);
        PorDefecto = false;
    }

    private void Poner(string nombre, TipoContrato modalidad, string descripcion, bool activo, string por, DateTimeOffset ahora)
    {
        Nombre = nombre.Trim();
        Modalidad = modalidad;
        Descripcion = descripcion.Trim();
        Activo = activo;
        ActualizadoPor = por;
        ActualizadoEn = ahora;
    }

    /// <summary>Los tipos con que arranca cada empresa. La misión en empresa usuaria solo aplica a las empresas de servicios temporales.</summary>
    public static IReadOnlyList<TipoContratoEmpresa> PorDefectoPara(Guid empresaId, bool esTemporal, DateTimeOffset ahora)
    {
        var lista = TiposPorDefecto.Lista
            .Where(t => esTemporal || t.Modalidad != TipoContrato.MisionTemporal)
            .Select(t => Crear(empresaId, t.Nombre, t.Modalidad, t.Descripcion, "Selvana", ahora))
            .ToList();
        lista.ForEach(t => t.PorDefecto = true);
        return lista;
    }
}

/// <summary>Descripciones por defecto, escritas para que la IA las siga. No tienen revisión jurídica: RRHH las ajusta a su empresa.</summary>
public static class TiposPorDefecto
{
    public sealed record Tipo(string Nombre, TipoContrato Modalidad, string Descripcion);

    public static readonly IReadOnlyList<Tipo> Lista =
    [
        new("Término indefinido", TipoContrato.Indefinido,
            "Contrato sin fecha de terminación (art. 47 del Código Sustantivo del Trabajo). Es la regla general para la planta propia de la empresa. " +
            "Se especifica solo la fecha de inicio; nunca lleva fecha de fin. Periodo de prueba de hasta 2 meses, siempre por escrito. " +
            "Termina por renuncia, mutuo acuerdo, justa causa o despido sin justa causa con la indemnización de ley. " +
            "Al redactar: di que es a término indefinido, pon la fecha de inicio y no escribas ninguna fecha de terminación ni de renovación."),
        new("Término fijo", TipoContrato.TerminoFijo,
            "Contrato con fecha de inicio y fecha de terminación, siempre por escrito (art. 46 del Código Sustantivo del Trabajo); dura máximo 4 años. " +
            "Se especifican las dos fechas: inicio y fin. Si dura menos de un año, el periodo de prueba no pasa de la quinta parte de la duración " +
            "y puede prorrogarse hasta 3 veces por periodos iguales o menores; después cada renovación es mínimo de un año. " +
            "Si ninguna de las partes avisa por escrito con 30 días de anticipación que no lo renovará, se renueva por un periodo igual. " +
            "Al redactar: la cláusula de duración lleva la fecha de inicio, la fecha de terminación y el preaviso de 30 días."),
        new("Obra o labor", TipoContrato.ObraOLabor,
            "Contrato que dura lo que dure la obra o labor contratada (art. 45 del Código Sustantivo del Trabajo); no lleva fecha fija de terminación. " +
            "Se especifica la fecha de inicio y se describe con precisión la obra o labor, porque de ella depende cuándo termina. " +
            "Termina cuando la obra o labor se acaba; terminarlo antes sin justa causa obliga a indemnizar. " +
            "Al redactar: el objeto y la duración citan la descripción exacta de la labor y dicen que el contrato termina al finalizarla."),
        new("Aprendizaje", TipoContrato.Aprendizaje,
            "Contrato especial de formación (Ley 789 de 2002): el aprendiz estudia en una institución y practica en la empresa, máximo 2 años. " +
            "Recibe una cuota de sostenimiento, que no es salario: mínimo el 75 % del salario mínimo en la etapa lectiva y el 100 % en la práctica. " +
            "No tiene periodo de prueba ni prestaciones sociales; sí salud, y riesgos laborales en la etapa práctica. " +
            "Al redactar: habla de cuota de sostenimiento (nunca de salario), nombra la institución y el programa si se conocen y las fechas de cada etapa."),
        new("Misión en empresa usuaria", TipoContrato.MisionTemporal,
            "Trabajador en misión de una empresa de servicios temporales (art. 77 de la Ley 50 de 1990 y Decreto 4369 de 2006). " +
            "El empleador es la empresa de servicios temporales y el trabajo se presta en la empresa usuaria. " +
            "Dura lo que dure la misión, hasta 6 meses prorrogables por 6 más en la misma empresa usuaria. " +
            "Se especifican la fecha de inicio, la empresa usuaria y la labor; el periodo de prueba no pasa de la quinta parte si la misión dura menos de un año. " +
            "Al redactar: deja claro quién es el empleador, nombra la empresa usuaria y la labor, y di que el contrato termina al terminar la misión."),
    ];
}
