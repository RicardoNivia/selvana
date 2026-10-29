using Microsoft.EntityFrameworkCore;
using Selvana.Core.Contracts.Empresas;
using Selvana.Expediente.Application.Abstracciones;
using Selvana.Expediente.Application.Empleados;
using Selvana.Expediente.Contracts;
using Selvana.Expediente.Domain.Empleados;
using Selvana.Expediente.Infrastructure.Persistence;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;

namespace Selvana.Expediente.Infrastructure;

internal sealed class PlantaActiva(ExpedienteDbContext db) : IPlantaActiva
{
    public async Task<IReadOnlyList<EmpleadoActivo>> ListarAsync(Guid empresaId, CancellationToken cancellationToken) =>
        await db.Set<Empleado>().AsNoTracking().Where(e => e.EmpresaId == empresaId && e.Estado == EstadoEmpleado.Activo).OrderBy(e => e.Nombre)
            .Select(e => new EmpleadoActivo(e.Id, e.Codigo, e.Nombre, e.Documento, e.Correo, e.Cargo, e.Area, e.Sede, e.EmpresaUsuariaId, e.EmpresaUsuaria,
                e.FechaIngreso, e.FechaFinContrato))
            .ToListAsync(cancellationToken);
}

/// <summary>P5 · Lo que el portal de la persona ve y hace de su empleo. Siempre por cédula y correo a la vez.</summary>
internal sealed class EmpleoDeLaPersona(
    ExpedienteDbContext db, IDirectorioEmpresas directorio, IAlmacenArchivos almacen, TimeProvider reloj,
    ICommandHandler<ProponerDatosDesdePortalCommand> proponer, ICommandHandler<ConfirmarDatosDesdePortalCommand> confirmar,
    ICommandHandler<EntregarSoporteDesdePortalCommand> entregar) : IEmpleoDeLaPersona
{
    public async Task<IReadOnlyList<Contracts.EmpleoDeLaPersona>> ListarAsync(string documento, string correo, CancellationToken cancellationToken)
    {
        var empleados = await EmpleadosRepositorio.DeLaPersona(db.Set<Empleado>().AsNoTracking(), documento, correo)
            .OrderBy(e => e.Estado).ThenByDescending(e => e.FechaIngreso).ToListAsync(cancellationToken);
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var lista = new List<Contracts.EmpleoDeLaPersona>();
        foreach (var e in empleados)
        {
            var empresa = (await directorio.ObtenerAsync(e.EmpresaId, cancellationToken))?.Nombre ?? "";
            lista.Add(new Contracts.EmpleoDeLaPersona(e.Id, empresa, e.Codigo, e.Cargo, e.Area, e.Sede, e.EmpresaUsuaria, e.TipoContrato, e.SalarioCop, e.FechaIngreso,
                e.FechaFinContrato, e.Estado.ToString(), e.Celular, e.Ciudad, e.Afiliaciones.Eps, e.Afiliaciones.FondoPension, e.Afiliaciones.Arl,
                e.Afiliaciones.CajaCompensacion,
                [.. e.Documentos.OrderBy(d => d.Tipo).ThenBy(d => d.Nombre).Select(d => new DocumentoDelEmpleo(d.Id, d.Tipo, d.Nombre, d.Vigente?.VenceEn,
                    Vigencia(d.Vigente?.VenceEn, hoy), d.Vigente?.Lectura is { Estado: EstadoLectura.Leyendo or EstadoLectura.PorRevisar }))],
                e.AvisosParaElEmpleado(hoy), e.ActualizacionPedidaEn is not null, e.DatosConfirmadosEn,
                e.CambioPropuesto is { } p ? new CambioPendienteDelEmpleo(p.Cambios, p.En) : null));
        }

        return lista;
    }

    private static string Vigencia(DateOnly? vence, DateOnly hoy) => vence switch
    {
        null => "SinVencimiento",
        var v when v < hoy => "Vencido",
        var v when v <= hoy.AddDays(Empleado.DiasAvisoVencimiento) => "PorVencer",
        _ => "Vigente",
    };

    public Task<Result> ProponerDatosAsync(Guid empleadoId, string documento, string correo, DatosPropuestosPorElEmpleado datos, CancellationToken cancellationToken) =>
        proponer.Handle(new ProponerDatosDesdePortalCommand(empleadoId, documento, correo, datos), cancellationToken);

    public Task<Result> ConfirmarDatosAsync(Guid empleadoId, string documento, string correo, CancellationToken cancellationToken) =>
        confirmar.Handle(new ConfirmarDatosDesdePortalCommand(empleadoId, documento, correo), cancellationToken);

    public Task<Result> EntregarSoporteAsync(Guid empleadoId, string documento, string correo, SoporteDelEmpleado soporte, CancellationToken cancellationToken) =>
        entregar.Handle(new EntregarSoporteDesdePortalCommand(empleadoId, documento, correo, soporte), cancellationToken);

    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> DescargarAsync(Guid empleadoId, string documento, string correo, Guid documentoId,
        CancellationToken cancellationToken)
    {
        var e = await EmpleadosRepositorio.DeLaPersona(db.Set<Empleado>().AsNoTracking(), documento, correo).FirstOrDefaultAsync(x => x.Id == empleadoId, cancellationToken);
        var vigente = e?.Documentos.FirstOrDefault(d => d.Id == documentoId)?.Vigente;
        return e is null || vigente is null ? null : await almacen.LeerAsync(e.EmpresaId, vigente.ArchivoId, cancellationToken);
    }
}
