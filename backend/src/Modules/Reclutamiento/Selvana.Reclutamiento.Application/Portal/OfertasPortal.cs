using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Portal;
using Selvana.Reclutamiento.Domain.Postulaciones;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Portal;

/// <summary>Una vacante publicada que encaja con la hoja de vida, con el porqué (reglas, sin IA ni puntaje oculto).</summary>
public sealed record OfertaParaTiDto(string Codigo, string Titulo, string Empresa, string Ciudad, string Jornada, decimal SalarioMinimo, decimal SalarioMaximo,
    IReadOnlyList<string> Porque, DateTimeOffset PublicadaEn);

/// <summary>Lo que se compara de una vacante publicada.</summary>
public sealed record VacanteParaComparar(string Codigo, string Titulo, string Cargo, string Empresa, string Ciudad, string Jornada, decimal SalarioMinimo,
    decimal SalarioMaximo, string Texto, DateTimeOffset PublicadaEn);

public interface ILecturaOfertas
{
    /// <summary>Las vacantes publicadas más recientes a las que la persona no se ha postulado.</summary>
    Task<IReadOnlyList<VacanteParaComparar>> PublicadasAsync(IReadOnlyCollection<string> codigosYaPostulados, int maximo, CancellationToken cancellationToken);
}

public sealed record ObtenerOfertasParaTiQuery : IQuery<IReadOnlyList<OfertaParaTiDto>>;

/// <summary>Postularse con la hoja de vida del perfil: deja listo el borrador de la vacante y la persona confirma y responde las preguntas.</summary>
public sealed record PostularmeConMiPerfilCommand(string CodigoVacante) : ICommand<Guid>;

/// <summary>
/// Reglas que cruzan la hoja de vida con la vacante: cargos en los que trabajó, estudios, cursos y habilidades contra el título,
/// el cargo y los requisitos; y la ciudad. Explicable y sin costo de IA.
/// </summary>
public static class EncajeOferta
{
    private static readonly HashSet<string> Vacias = ["para", "como", "con", "del", "los", "las", "una", "uno", "por", "que", "sus", "experiencia",
        "anos", "minimo", "manejo", "conocimiento", "conocimientos", "trabajo", "empresa", "tecnico", "profesional", "auxiliar", "nivel"];

    public static IReadOnlyList<string> Porque(DatosCandidato perfil, VacanteParaComparar v)
    {
        var texto = $" {Normalizacion.Simplificar($"{v.Titulo} {v.Cargo} {v.Texto}")} ";
        var razones = new List<string>();

        var cargo = perfil.Experiencia.Select(e => e.Cargo).FirstOrDefault(c => Palabras(c).Any(p => texto.Contains($" {p}", StringComparison.Ordinal)));
        if (cargo is not null)
        {
            razones.Add($"Tu experiencia como {cargo.Trim().ToLowerInvariant()}");
        }

        var estudio = perfil.Formacion.Select(f => f.Titulo).FirstOrDefault(t => Palabras(t).Any(p => texto.Contains($" {p}", StringComparison.Ordinal)));
        if (estudio is not null)
        {
            razones.Add($"Tus estudios: {estudio.Trim()}");
        }

        var habilidades = perfil.Habilidades.Concat(perfil.Cursos)
            .Where(h => Palabras(h).Any(p => texto.Contains($" {p}", StringComparison.Ordinal))).Take(3).ToList();
        if (habilidades.Count > 0)
        {
            razones.Add($"Pide lo que sabes: {string.Join(", ", habilidades.Select(h => h.Trim()))}");
        }

        if (razones.Count > 0 && Normalizacion.Simplificar(perfil.Ciudad).Split(',')[0] is { Length: > 2 } ciudad
            && Normalizacion.Simplificar(v.Ciudad).StartsWith(ciudad, StringComparison.Ordinal))
        {
            razones.Add($"Es en tu ciudad ({v.Ciudad})");
        }

        return razones;
    }

    private static IEnumerable<string> Palabras(string? texto) =>
        Normalizacion.Simplificar(texto).Split([' ', ',', '.', '/', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 4 && !Vacias.Contains(p));
}

internal sealed class ObtenerOfertasParaTiHandler(IPersonaActual persona, ICuentasCandidato cuentas, ILecturaPortalPersona postulaciones, ILecturaOfertas ofertas)
    : IQueryHandler<ObtenerOfertasParaTiQuery, IReadOnlyList<OfertaParaTiDto>>
{
    public async Task<Result<IReadOnlyList<OfertaParaTiDto>>> Handle(ObtenerOfertasParaTiQuery query, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return CuentaErrors.NoExiste;
        }

        var yaPostulado = (await postulaciones.PostulacionesAsync(cuenta.Documento, cuenta.Correo, cancellationToken)).Select(p => p.VacanteCodigo).ToHashSet();
        var publicadas = await ofertas.PublicadasAsync(yaPostulado, 200, cancellationToken);
        return publicadas.Select(v => (v, porque: EncajeOferta.Porque(cuenta.Datos, v)))
            .Where(x => x.porque.Count > 0)
            .OrderByDescending(x => x.porque.Count).ThenByDescending(x => x.v.PublicadaEn).Take(12)
            .Select(x => new OfertaParaTiDto(x.v.Codigo, x.v.Titulo, x.v.Empresa, x.v.Ciudad, x.v.Jornada, x.v.SalarioMinimo, x.v.SalarioMaximo, x.porque, x.v.PublicadaEn))
            .ToList();
    }
}

/// <summary>
/// Copia la hoja de vida del perfil a la empresa de la vacante (cada empresa guarda la suya) y deja el borrador ya leído con los datos
/// del perfil. La persona sigue en la pantalla de postulación: responde las preguntas y acepta la autorización de esa empresa.
/// </summary>
internal sealed class PostularmeConMiPerfilHandler(
    IPersonaActual persona, ICuentasCandidato cuentas, ILecturaPortalPersona postulaciones, IVacantes vacantes, IBorradoresPostulacion borradores,
    IAlmacenHojasDeVida almacen, IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<PostularmeConMiPerfilCommand, Guid>
{
    private static readonly Error SinHojaDeVida = Error.Validation("Portal.SinHojaDeVida", "Sube tu hoja de vida en tu perfil para postularte con un clic.");

    public async Task<Result<Guid>> Handle(PostularmeConMiPerfilCommand c, CancellationToken cancellationToken)
    {
        var cuenta = persona.CuentaId is { } id ? await cuentas.ObtenerAsync(id, cancellationToken) : null;
        if (cuenta is null)
        {
            return CuentaErrors.NoExiste;
        }

        var vacante = await vacantes.ObtenerPorCodigoAsync(c.CodigoVacante, cancellationToken);
        if (vacante is null)
        {
            return VacanteErrors.NoExiste;
        }

        if (!vacante.RecibePostulaciones)
        {
            return VacanteErrors.NoPublicada;
        }

        // Si ya aparece en sus postulaciones, no se vuelve a postular
        if ((await postulaciones.PostulacionesAsync(cuenta.Documento, cuenta.Correo, cancellationToken)).Any(p => p.VacanteCodigo == vacante.Codigo))
        {
            return CandidatoErrors.YaPostulado;
        }

        var archivo = cuenta.ArchivoHojaDeVidaId is { } archivoId ? await almacen.LeerAsync(archivoId, cancellationToken) : null;
        if (archivo is null)
        {
            return SinHojaDeVida;
        }

        var ahora = reloj.GetUtcNow();
        var copia = almacen.Guardar(vacante.EmpresaId, archivo.Value.Nombre, archivo.Value.TipoContenido, archivo.Value.Contenido, ahora);
        var borrador = BorradorPostulacion.Crear(vacante.Id, vacante.EmpresaId, copia, archivo.Value.Nombre, ahora);
        borrador.RegistrarLectura(cuenta.Datos, "Perfil del portal", ahora);
        borradores.Agregar(borrador);
        await unidad.SaveChangesAsync(cancellationToken);
        return borrador.Id;
    }
}
