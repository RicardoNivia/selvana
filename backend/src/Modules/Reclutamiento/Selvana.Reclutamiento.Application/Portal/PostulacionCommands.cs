using FluentValidation;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Ranking;
using Selvana.Reclutamiento.Contracts;
using Selvana.Reclutamiento.Domain.Candidatos;
using Selvana.Reclutamiento.Domain.Postulaciones;
using Selvana.Reclutamiento.Domain.Vacantes;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Reclutamiento.Application.Portal;

/// <summary>El candidato sube su hoja de vida en el portal; se guarda cifrada y la IA empieza a leerla.</summary>
public sealed record IniciarPostulacionCommand(string CodigoVacante, string NombreArchivo, string TipoContenido, byte[] Contenido) : ICommand<Guid>;

/// <summary>El candidato confirmó sus datos, respondió las preguntas y aceptó la autorización: la postulación llega a la vacante.</summary>
public sealed record EnviarPostulacionCommand(
    Guid BorradorId, string Nombre, string Documento, string Celular, string Correo, string Ciudad,
    IReadOnlyList<bool> Respuestas, bool AceptaAutorizacion, string? Canal, string Ip, string Navegador, string? LinkedIn = null) : ICommand<string>;

internal sealed class IniciarPostulacionValidator : AbstractValidator<IniciarPostulacionCommand>
{
    public const int TamanoMaximo = 10 * 1024 * 1024;

    public static readonly string[] Tipos =
    [
        "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "image/jpeg", "image/png", "image/webp",
    ];

    public IniciarPostulacionValidator()
    {
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El archivo está vacío. Vuelve a subir tu hoja de vida.")
            .Must(c => c.Length <= TamanoMaximo).WithMessage("El archivo pesa más de 10 MB. Sube un PDF más liviano o una foto.");
        RuleFor(c => c.TipoContenido).Must(t => Tipos.Contains(t))
            .WithMessage("Sube tu hoja de vida en PDF, Word (.docx) o una foto (JPG o PNG).");
    }
}

internal sealed class EnviarPostulacionValidator : AbstractValidator<EnviarPostulacionCommand>
{
    public EnviarPostulacionValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("Escribe tu nombre completo.").MaximumLength(200);
        RuleFor(c => c.Documento).Must(d => Candidato.NormalizarDocumento(d).Length is >= 5 and <= 12)
            .WithMessage("Escribe tu número de cédula sin puntos.");
        RuleFor(c => c.Celular).Must(c => new string([.. (c ?? "").Where(char.IsDigit)]) is { Length: 10 } n && n.StartsWith('3'))
            .WithMessage("Escribe un celular de 10 dígitos que empiece por 3.");
        RuleFor(c => c.Correo).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Correo)).WithMessage("Revisa tu correo.");
        RuleFor(c => c.LinkedIn).Must(Candidato.EsLinkedInValido)
            .WithMessage("Pega el enlace de tu perfil, por ejemplo linkedin.com/in/tu-nombre.");
        RuleFor(c => c.AceptaAutorizacion).Equal(true).WithMessage("Para enviar tu postulación debes aceptar la autorización de datos.");
    }
}

internal sealed class IniciarPostulacionHandler(
    IVacantes vacantes, IBorradoresPostulacion borradores, IAlmacenHojasDeVida almacen, IEventosReclutamiento eventos,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<IniciarPostulacionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(IniciarPostulacionCommand command, CancellationToken cancellationToken)
    {
        var vacante = await vacantes.ObtenerPorCodigoAsync(command.CodigoVacante, cancellationToken);
        if (vacante is null)
        {
            return VacanteErrors.NoExiste;
        }

        if (!vacante.RecibePostulaciones)
        {
            return VacanteErrors.NoPublicada;
        }

        var ahora = reloj.GetUtcNow();
        var nombre = Path.GetFileName(command.NombreArchivo);
        var archivoId = almacen.Guardar(vacante.EmpresaId, nombre, command.TipoContenido, command.Contenido, ahora);
        var borrador = BorradorPostulacion.Crear(vacante.Id, vacante.EmpresaId, archivoId, nombre, ahora);
        borradores.Agregar(borrador);

        eventos.Publicar(new HojaDeVidaRecibida(Guid.CreateVersion7(), ahora, vacante.EmpresaId, borrador.Id, archivoId, nombre,
            command.TipoContenido, vacante.Titulo));
        await unidad.SaveChangesAsync(cancellationToken);
        return borrador.Id;
    }
}

internal sealed class EnviarPostulacionHandler(
    IBorradoresPostulacion borradores, IVacantes vacantes, ICandidatos candidatos, ICalibracionRanking calibracion, IBitacora bitacora, IEventosReclutamiento eventos,
    IUnidadReclutamiento unidad, TimeProvider reloj) : ICommandHandler<EnviarPostulacionCommand, string>
{
    public async Task<Result<string>> Handle(EnviarPostulacionCommand c, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var borrador = await borradores.ObtenerAsync(c.BorradorId, cancellationToken);
        if (borrador is null || borrador.Vencido(ahora))
        {
            return BorradorErrors.NoExiste;
        }

        if (borrador.Estado == EstadoLectura.Leyendo)
        {
            return BorradorErrors.Leyendo;
        }

        var vacante = await vacantes.ObtenerAsync(borrador.VacanteId, cancellationToken);
        if (vacante is null || !vacante.RecibePostulaciones)
        {
            return VacanteErrors.NoPublicada;
        }

        var leidos = borrador.DatosLeidos ?? new DatosCandidato();
        var datos = new DatosCandidato
        {
            Nombre = c.Nombre.Trim(),
            Documento = Candidato.NormalizarDocumento(c.Documento),
            Celular = new string([.. c.Celular.Where(char.IsDigit)]),
            Correo = (c.Correo ?? "").Trim().ToLowerInvariant(),
            Ciudad = (c.Ciudad ?? "").Trim(),
            Experiencia = leidos.Experiencia,
            Formacion = leidos.Formacion,
            Cursos = leidos.Cursos,
            Habilidades = leidos.Habilidades,
            LinkedIn = Candidato.NormalizarLinkedIn(c.LinkedIn),
        };

        var corregidos = CamposCorregidos(leidos, datos);
        var candidato = await candidatos.ObtenerPorDocumentoAsync(vacante.EmpresaId, datos.Documento, cancellationToken);
        if (candidato is null)
        {
            candidato = Candidato.Registrar(vacante.EmpresaId, datos, ahora);
            candidatos.Agregar(candidato);
        }
        else
        {
            candidato.ActualizarDatos(datos, ahora);
        }

        var respuestas = vacante.PreguntasFiltro.Select((p, i) => new RespuestaFiltro { Pregunta = p, Respuesta = i < c.Respuestas.Count && c.Respuestas[i] });
        var autorizacion = c.AceptaAutorizacion
            ? new AutorizacionDatos
            {
                Version = PoliticaDatos.Version,
                Texto = PoliticaDatos.Texto(vacante.EmpresaVisible),
                AceptadaEn = ahora,
                Ip = c.Ip,
                Navegador = c.Navegador.Length <= 300 ? c.Navegador : c.Navegador[..300],
                Huella = AutorizacionDatos.CalcularHuella(PoliticaDatos.Version, PoliticaDatos.Texto(vacante.EmpresaVisible), ahora, c.Ip,
                    c.Navegador.Length <= 300 ? c.Navegador : c.Navegador[..300], datos.Documento),
            }
            : null;

        var canal = Enum.TryParse<CanalPostulacion>(c.Canal, ignoreCase: true, out var leido) ? leido : CanalPostulacion.Portal;
        var postulacion = candidato.Postularse(vacante.Id, canal, respuestas, autorizacion, borrador.ArchivoId, corregidos, ahora);
        if (postulacion.IsFailure)
        {
            return postulacion.Error;
        }

        borradores.Quitar(borrador);

        // El agente de ranking califica la afinidad en segundo plano, con el perfil anónimo
        candidato.PedirAfinidad(postulacion.Value.Id);
        eventos.Publicar(PerfilParaRanking.Solicitud(vacante, candidato, postulacion.Value, ahora,
            await calibracion.CriteriosAsync(vacante.EmpresaId, vacante.Cargo, vacante.Id, cancellationToken)));
        bitacora.Registrar(vacante.EmpresaId, TipoActor.Titular, datos.Nombre, $"Se postuló a la vacante {vacante.Codigo} y aceptó la autorización {PoliticaDatos.Version}",
            $"{postulacion.Value.Numero} · canal {canal}{(corregidos.Count > 0 ? $" · corrigió {string.Join(", ", corregidos)}" : "")}",
            $"Evidencia sha256 {autorizacion!.Huella[..8]}…", "Postulacion", postulacion.Value.Id);
        await unidad.SaveChangesAsync(cancellationToken);
        return postulacion.Value.Numero;
    }

    /// <summary>Qué datos leídos por la IA corrigió el candidato: es la medida de calidad del lector de hojas de vida.</summary>
    private static List<string> CamposCorregidos(DatosCandidato leidos, DatosCandidato confirmados)
    {
        var cambios = new List<string>();
        void Comparar(string campo, string a, string b)
        {
            if (!string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                cambios.Add(campo);
            }
        }

        Comparar("nombre", leidos.Nombre, confirmados.Nombre);
        Comparar("documento", Candidato.NormalizarDocumento(leidos.Documento), confirmados.Documento);
        Comparar("celular", new string([.. leidos.Celular.Where(char.IsDigit)]), confirmados.Celular);
        Comparar("correo", leidos.Correo, confirmados.Correo);
        Comparar("ciudad", leidos.Ciudad, confirmados.Ciudad);
        Comparar("linkedin", leidos.LinkedIn, confirmados.LinkedIn);
        return cambios;
    }
}
