using System.Globalization;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Infrastructure.Common.Ejemplo;
using Selvana.Reclutamiento.Contracts;
using Selvana.SharedKernel.Results;
using Selvana.Vinculacion.Application.Abstracciones;
using Selvana.Vinculacion.Application.Documentos;
using Selvana.Vinculacion.Contracts.Eventos;
using Selvana.Vinculacion.Domain.Procesos;
using Selvana.Vinculacion.Infrastructure.Persistence;

namespace Selvana.Vinculacion.Infrastructure.Ejemplo;

/// <summary>
/// Procesos de vinculación de ejemplo a partir de la terna sembrada en Reclutamiento: uno con la oferta por aprobar, otro con los
/// documentos leídos por revisar y otro completo hasta crear el empleado. Lo que haría la IA (carta oferta, lectura de documentos) se
/// fija a mano: no se encola nada hacia el AI.Worker.
/// </summary>
internal static class SemillaEjemplo
{
    private const string Modelo = "ejemplo (sin IA)";
    private const string Ip = "181.50.0.10";
    private const string Navegador = "Mozilla/5.0 (datos de ejemplo)";

    public static async Task SembrarAsync(VinculacionDbContext db, IAlmacenDocumentos almacen, ICifradoSensible cifrado, ITokensEnlace tokens,
        IParametrosLegales parametros, IReadOnlyList<(Guid EmpresaId, PostulanteContratable Postulante, string Avance)> contratables, string porRrhh, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahora.UtcDateTime);
        var dias = 0;
        foreach (var (empresaId, c, avance) in contratables)
        {
            var ingreso = hoy.AddDays(avance == "Onboarding" ? 5 : 12 + dias++);
            var condiciones = new CondicionesContratacion
            {
                Tipo = c.EmpresaUsuaria is null ? TipoContrato.Indefinido : TipoContrato.MisionTemporal,
                SalarioCop = c.AspiracionSalarialCop is { } a && a >= c.SalarioMinimo && a <= c.SalarioMaximo ? a : c.SalarioMinimo,
                FechaIngreso = ingreso,
                FechaFin = c.EmpresaUsuaria is null ? null : ingreso.AddDays(180),
                PeriodoPruebaDias = 30,
                Labor = c.EmpresaUsuaria is null ? null : $"{c.Cargo} en misión en {c.EmpresaUsuaria}, por incremento de la producción",
                Jornada = c.Jornada,
                Ciudad = c.CiudadVacante,
            };
            var abierto = ahora.AddDays(avance == "Onboarding" ? -6 : -2);
            var p = ProcesoVinculacion.Abrir(empresaId, c.Empresa, c.EmpresaUsuariaId, c.EmpresaUsuaria,
                new PersonaVinculada { Nombre = c.Nombre, Documento = c.Documento, Correo = c.Correo, Celular = c.Celular, Ciudad = c.Ciudad },
                new VacanteOrigen
                {
                    VacanteId = c.VacanteId, PostulacionId = c.PostulacionId, CandidatoId = c.CandidatoId, Codigo = c.CodigoVacante, Titulo = c.Vacante,
                    Cargo = c.Cargo, Descripcion = c.Descripcion, Requisitos = [.. c.Requisitos], SalarioMinimo = c.SalarioMinimo, SalarioMaximo = c.SalarioMaximo,
                },
                condiciones, porRrhh, abierto);
            db.Add(p);
            db.AddIntegrationEvent(new ContratacionAprobada(Guid.CreateVersion7(), abierto, p.EmpresaId, c.PostulacionId, p.Id, porRrhh));

            p.RegistrarBorrador(Carta(c), Clausulas(condiciones),
                [new RazonOferta { Signo = "+", Texto = "Salario dentro del rango de la vacante y de la aspiración del candidato", Fuente = "Preselección" },
                 new RazonOferta { Signo = "+", Texto = $"Contrato de {ReglasContrato.Nombre(condiciones.Tipo).ToLowerInvariant()} según la empresa", Fuente = "Condiciones" },
                 new RazonOferta { Signo = "+", Texto = "Periodo de prueba dentro del límite legal", Fuente = "Art. 78 CST" }],
                [], Modelo, 0m, abierto.AddMinutes(2));

            if (avance == "Oferta")
            {
                AvisarOferta(db, p, abierto.AddMinutes(2));
                continue;
            }

            var alertas = ReglasContrato.Revisar(p.Condiciones, parametros.Actuales, hoy, p.EmpresaUsuaria, c.SalarioMinimo, c.SalarioMaximo);
            Ok(p.AprobarOferta(alertas, tokens.Generar().Huella, porRrhh, abierto.AddHours(1)));
            Ok(p.ResponderOferta(true, c.Nombre, c.Documento, null, Ip, Navegador, abierto.AddHours(5)));

            var subidos = abierto.AddDays(1);
            foreach (var (tipo, campos) in DocumentosCandidato(c))
            {
                var archivo = almacen.Guardar(p.EmpresaId, $"{RequisitosDocumentales.Nombre(tipo)}.pdf", "application/pdf",
                    PdfTexto.Crear([RequisitosDocumentales.Nombre(tipo), .. campos.Select(x => $"{x.Campo}: {x.Valor}"), "", "Documento ficticio de ejemplo."]), subidos);
                Ok(p.SubirDocumento(tipo, archivo, $"{RequisitosDocumentales.Nombre(tipo)}.pdf", desdeEnlace: true, c.Nombre, subidos));
                p.RegistrarLectura(tipo, archivo, new LecturaDocumento
                {
                    TipoDetectado = RequisitosDocumentales.Nombre(tipo), Legible = true, Campos = campos, Observaciones = [],
                    Razones = [new RazonOferta { Signo = "+", Texto = "Legible y del tipo pedido", Fuente = "Documento" }],
                    CoincideTitular = RequisitosDocumentales.CoincideTitular(campos, c.Nombre, c.Documento), TipoCorrecto = true, Modelo = Modelo, LeidoEn = subidos,
                }, subidos.AddMinutes(1));
            }

            if (avance == "Documentos")
            {
                AvisarDocumentos(db, p, subidos.AddMinutes(1));
                continue;
            }

            var revisados = subidos.AddHours(3);
            foreach (var d in p.Documentos.Where(d => !d.LoCargaEmpresa && d.Estado == EstadoDocumento.PorRevisar).Select(d => d.Tipo).ToList())
            {
                Ok(p.AprobarDocumento(d, porRrhh, revisados));
            }

            var antecedentes = almacen.Guardar(p.EmpresaId, "Antecedentes.pdf", "application/pdf",
                PdfTexto.Crear(["Consulta de antecedentes", c.Nombre, $"CC {c.Documento}", "Sin anotaciones (ficticio)"]), revisados);
            Ok(p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.Antecedentes, antecedentes, "Antecedentes.pdf", ResultadoAntecedentes.SinAnotaciones, null, null, porRrhh, revisados));
            var examen = almacen.Guardar(p.EmpresaId, "Examen médico de ingreso.pdf", "application/pdf",
                PdfTexto.Crear(["Certificado de aptitud laboral", c.Nombre, "Concepto: apto (ficticio)"]), revisados);
            Ok(p.RegistrarSoporteEmpresa(TipoDocumentoIngreso.ExamenMedico, examen, "Examen médico de ingreso.pdf", null, cifrado.Cifrar($"{ConceptoMedico.Apto}|"), false,
                porRrhh, revisados));

            var afiliado = revisados.AddDays(1);
            Ok(p.ActualizarAfiliacion(TipoAfiliacion.Eps, EstadoAfiliacion.Confirmada, "Nueva EPS", "EPS-2026-48120", ingreso, null, porRrhh, afiliado));
            Ok(p.ActualizarAfiliacion(TipoAfiliacion.Pension, EstadoAfiliacion.Confirmada, "Porvenir", "PEN-2026-11873", ingreso, null, porRrhh, afiliado));
            Ok(p.ActualizarAfiliacion(TipoAfiliacion.Arl, EstadoAfiliacion.Confirmada, "ARL Sura", "ARL-2026-90321", ingreso.AddDays(-1), 2, porRrhh, afiliado));
            Ok(p.ActualizarAfiliacion(TipoAfiliacion.CajaCompensacion, EstadoAfiliacion.Confirmada, "Comfatolima", "CCF-2026-5532", ingreso, null, porRrhh, afiliado));
            Ok(p.EditarAlta("Operaciones", "CC-210 Bodega Ibagué", c.CiudadVacante, "Bancolombia", "Ahorros", "24512345678", porRrhh, afiliado));
            Ok(p.CrearEmpleado(true, porRrhh, afiliado.AddHours(1)));
            // Como el caso de uso real: el empleado nace en Expediente por el evento (el plan de inducción con IA no se pide)
            string Entidad(TipoAfiliacion t) => p.Afiliaciones.FirstOrDefault(a => a.Tipo == t) is { Estado: EstadoAfiliacion.Confirmada } a ? a.Entidad : "";
            db.AddIntegrationEvent(new Contracts.Empleados.EmpleadoVinculado(Guid.CreateVersion7(), afiliado.AddHours(1), p.EmpresaId, p.Id, p.Vacante.CandidatoId,
                p.EmpresaUsuariaId, p.Persona.Documento, p.Persona.Nombre, p.Persona.Correo, p.Persona.Celular, p.Persona.Ciudad, p.Vacante.Cargo, p.Alta.Area,
                p.Alta.CentroCosto, p.Alta.Sede, ReglasContrato.Nombre(p.Condiciones), p.Condiciones.SalarioCop, p.Condiciones.FechaIngreso, p.Condiciones.FechaFin,
                Entidad(TipoAfiliacion.Eps), Entidad(TipoAfiliacion.Pension), Entidad(TipoAfiliacion.Arl),
                p.Afiliaciones.FirstOrDefault(a => a.Tipo == TipoAfiliacion.Arl)?.ClaseRiesgo, Entidad(TipoAfiliacion.CajaCompensacion), porRrhh, p.EmpresaUsuaria));
        }

        await db.SaveChangesAsync();
    }

    private static T Ok<T>(T resultado) where T : Result =>
        resultado.IsFailure ? throw new InvalidOperationException($"Semilla de ejemplo: {resultado.Error.Code} · {resultado.Error.Description}") : resultado;

    private static string Carta(PostulanteContratable c) =>
        $"Apreciado(a) [TRABAJADOR], identificado(a) con [DOCUMENTO]:\n\nNos alegra ofrecerle el cargo de {c.Cargo} en {c.Empresa}, en {c.CiudadVacante}. " +
        "Valoramos su experiencia y lo que nos contó en el proceso de selección.\n\nLas condiciones del contrato van a continuación. Si está de acuerdo, " +
        "acepte la oferta desde el enlace que le enviamos.\n\nCordialmente,\nGestión Humana";

    private static List<Clausula> Clausulas(CondicionesContratacion c) =>
    [
        new() { Titulo = "Objeto", Texto = c.Labor ?? "Prestar sus servicios personales en el cargo indicado, con dedicación exclusiva." },
        new() { Titulo = "Salario", Texto = $"{ReglasContrato.Pesos(c.SalarioCop)} mensuales, pagaderos por quincenas." },
        new() { Titulo = "Jornada", Texto = c.Jornada },
        new() { Titulo = "Periodo de prueba", Texto = $"{c.PeriodoPruebaDias} días, según el artículo 78 del Código Sustantivo del Trabajo." },
        new() { Titulo = "Duración", Texto = c.FechaFin is { } fin
            ? $"Desde el {c.FechaIngreso.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} hasta el {fin.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} o hasta terminar la labor."
            : "Término indefinido." },
    ];

    private static List<(TipoDocumentoIngreso Tipo, List<CampoLeido> Campos)> DocumentosCandidato(PostulanteContratable c)
    {
        CampoLeido Campo(string nombre, string valor) => new() { Campo = nombre, Valor = valor };
        return
        [
            (TipoDocumentoIngreso.Cedula, [Campo("Nombre", c.Nombre), Campo("Número de documento", c.Documento)]),
            (TipoDocumentoIngreso.CertificadoBancario, [Campo("Titular", c.Nombre), Campo("Número de documento", c.Documento), Campo("Banco", "Bancolombia"),
                Campo("Tipo de cuenta", "Ahorros"), Campo("Número de cuenta", "24512345678")]),
            (TipoDocumentoIngreso.CertificadoEps, [Campo("Nombre", c.Nombre), Campo("Número de documento", c.Documento), Campo("Entidad EPS", "Nueva EPS")]),
            (TipoDocumentoIngreso.CertificadoPension, [Campo("Nombre", c.Nombre), Campo("Número de documento", c.Documento), Campo("Fondo de pensiones", "Porvenir")]),
        ];
    }

    private static void AvisarOferta(VinculacionDbContext db, ProcesoVinculacion p, DateTimeOffset ahora)
    {
        var c = p.Condiciones;
        db.AddIntegrationEvent(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Vinculacion", "OfertaLaboral", p.Id,
            $"Oferta por aprobar · {p.Persona.Nombre}",
            $"La IA redactó la carta oferta y el contrato de {ReglasContrato.Nombre(c.Tipo).ToLowerInvariant()} para «{p.Vacante.Cargo}». Revíselos y apruébelos.",
            "Redactor de ofertas", Modelo,
            [new RazonPropuesta("+", "Salario dentro del rango de la vacante y de la aspiración del candidato", "Preselección"),
             new RazonPropuesta("+", "Periodo de prueba dentro del límite legal", "Art. 78 CST")],
            [new CampoPropuesta("Contrato", ReglasContrato.Nombre(c.Tipo)), new CampoPropuesta("Salario", ReglasContrato.Pesos(c.SalarioCop)),
             new CampoPropuesta("Ingreso", c.FechaIngreso.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))],
            null, $"/consola/vinculacion/{p.Id}", true, c.FechaIngreso));
    }

    private static void AvisarDocumentos(VinculacionDbContext db, ProcesoVinculacion p, DateTimeOffset ahora)
    {
        var porRevisar = p.Documentos.Where(d => d.Estado == EstadoDocumento.PorRevisar).ToList();
        var limpios = p.SinObservaciones;
        db.AddIntegrationEvent(new PropuestaPublicada(Guid.CreateVersion7(), ahora, p.EmpresaId, p.EmpresaUsuariaId, "Vinculacion", "DocumentosIngreso", p.Id,
            $"Documentos de ingreso por revisar · {p.Persona.Nombre}",
            $"{p.Persona.Nombre} subió todos sus documentos obligatorios. La IA los leyó: {limpios.Count} sin observaciones y {porRevisar.Count - limpios.Count} para mirar con calma.",
            "Lector de documentos", Modelo,
            [.. porRevisar.Take(5).Select(d => new RazonPropuesta("+", $"{RequisitosDocumentales.Nombre(d.Tipo)}: coincide con el titular", "Documento"))],
            [new CampoPropuesta("Pendientes de la empresa", "Antecedentes, Examen médico de ingreso")],
            limpios.Count > 0 ? new AccionPropuesta($"Aprobar {limpios.Count} sin observaciones", "POST", $"/api/v1/vinculacion/procesos/{p.Id}/documentos/aprobar-sin-observaciones") : null,
            $"/consola/vinculacion/{p.Id}", true, p.Condiciones.FechaIngreso));
    }
}
