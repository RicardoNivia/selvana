using RazonIaDominio = Selvana.Core.Domain.Estructura.RazonIa;
using Selvana.AI.Contracts.Estructura;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Bandeja;
using Selvana.Core.Contracts.Auditoria;
using Selvana.Core.Contracts.Bandeja;
using Selvana.Core.Domain.Estructura;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Core.Application.Estructura.RespuestasIa;

/// <summary>El agente organizador respondió: sus uniones de cargos quedan como propuestas para revisar.</summary>
internal sealed class UnionesCargosPropuestasConsumer(IEstructuras estructuras, IBitacora bitacora, IBandeja bandeja, IUnitOfWork unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<UnionesCargosPropuestas>
{
    public async Task Handle(UnionesCargosPropuestas evento, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(evento.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            return;
        }

        if (evento.Error is not null)
        {
            estructura.RegistrarFallaIa(evento.ImportacionId, evento.Error);
        }
        else
        {
            estructura.AgregarUnionesIa(evento.ImportacionId, evento.Uniones.Select(u => (u.NombreFinal, u.Codigos.ToList(), u.Porque)), evento.Modelo);
            await bitacora.RegistrarAsync(evento.EmpresaId, TipoActor.Ia, "Agente organizador de estructura", "Propuso unir cargos repetidos de la nómina",
                $"{evento.Uniones.Count} uniones con su razón · {evento.Modelo}", "Espera aprobación de una persona", "ImportacionNomina", evento.ImportacionId,
                cancellationToken: cancellationToken);
            await bandeja.PublicarAsync(new PropuestaPublicada(Guid.CreateVersion7(), reloj.GetUtcNow(), evento.EmpresaId, null, "Core", "UnionesCargos", evento.ImportacionId,
                $"Unir {evento.Uniones.Count} cargos repetidos de la nómina", "La IA encontró nombres distintos para el mismo cargo. Revise y aplique la importación.",
                "Agente organizador de estructura", evento.Modelo,
                [.. evento.Uniones.Take(5).Select(u => new RazonPropuesta("+", $"«{u.NombreFinal}»: {u.Porque}", $"{u.Codigos.Count} códigos en la nómina"))],
                [new CampoPropuesta("Uniones propuestas", evento.Uniones.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))],
                null, "/consola/empresa/conectar-nomina", false, null), cancellationToken);
        }

        await unidad.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>La IA redactó un perfil de cargo: queda como borrador hasta que una persona lo apruebe.</summary>
internal sealed class PerfilCargoRedactadoConsumer(IEstructuras estructuras, IBitacora bitacora, IBandeja bandeja, IUnitOfWork unidad, TimeProvider reloj)
    : IIntegrationEventConsumer<PerfilCargoRedactado>
{
    public async Task Handle(PerfilCargoRedactado evento, CancellationToken cancellationToken)
    {
        var estructura = await estructuras.ObtenerAsync(evento.EmpresaId, cancellationToken);
        if (estructura is null)
        {
            return;
        }

        var perfil = new PerfilCargo
        {
            Mision = evento.Mision,
            Funciones = [.. evento.Funciones],
            Requisitos = [.. evento.Requisitos],
            RiesgoArl = evento.RiesgoArl,
        };

        estructura.RegistrarBorradorPerfil(evento.CargoId, perfil,
            evento.Razones.Select(r => new RazonIaDominio { Signo = r.Signo, Texto = r.Texto, Fuente = r.Fuente }), evento.Modelo);
        var cargo = estructura.Cargos.FirstOrDefault(c => c.Id == evento.CargoId);
        await bitacora.RegistrarAsync(evento.EmpresaId, TipoActor.Ia, "Agente redactor de perfiles", $"Redactó el perfil del cargo {cargo?.Nombre}",
            $"Misión, {evento.Funciones.Count} funciones, requisitos y riesgo ARL {evento.RiesgoArl} · {evento.Modelo}", "Espera aprobación de una persona",
            "Cargo", evento.CargoId, cancellationToken: cancellationToken);
        await bandeja.PublicarAsync(new PropuestaPublicada(Guid.CreateVersion7(), reloj.GetUtcNow(), evento.EmpresaId, null, "Core", "PerfilCargo", evento.CargoId,
            $"Perfil de cargo · {cargo?.Nombre}", $"{cargo?.Empleados ?? 0} personas con este cargo · borrador de la IA", "Agente redactor de perfiles", evento.Modelo,
            [.. evento.Razones.Select(r => new RazonPropuesta(r.Signo, r.Texto, r.Fuente))],
            [new CampoPropuesta("Misión", evento.Mision), new CampoPropuesta("Funciones", string.Join(" · ", evento.Funciones.Take(4))),
             new CampoPropuesta("Riesgo ARL", evento.RiesgoArl)],
            new AccionPropuesta("Aprobar perfil", "POST", $"/api/v1/core/estructura/cargos/{evento.CargoId}/aprobar-perfil"), "/consola/empresa/estructura", false, null),
            cancellationToken);
        await unidad.SaveChangesAsync(cancellationToken);
    }
}
