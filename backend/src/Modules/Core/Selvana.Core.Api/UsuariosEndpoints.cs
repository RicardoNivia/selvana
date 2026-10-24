using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Selvana.Core.Application.Empresas;
using Selvana.Core.Application.Usuarios;
using Selvana.SharedKernel.Messaging;
using Selvana.SharedKernel.Results;
using Selvana.Web.Common;

namespace Selvana.Core.Api;

public sealed record InvitarUsuarioRequest(
    string Correo, string Nombre, string Rol, Guid? CargoId, bool TodasLasEmpresasUsuarias, IReadOnlyList<Guid>? EmpresasUsuarias,
    string? RolSugerido, string? RazonSugerencia);

public sealed record CambiarUsuarioRequest(string Nombre, string Rol, bool TodasLasEmpresasUsuarias, IReadOnlyList<Guid>? EmpresasUsuarias, bool Activo);

public sealed record EmpresaUsuariaRequest(string Nombre, string Nit, string? Sector, string? Ciudad);

public sealed record EditarEmpresaUsuariaRequest(string Nombre, string? Sector, string? Ciudad, bool Activa);

public sealed record ActivarCuentaRequest(string Token, string Contrasena)
{
    public override string ToString() => "ActivarCuentaRequest { }";
}

public sealed record CreadoResponse(Guid Id);

/// <summary>Usuarios y roles, empresas usuarias e invitaciones (bloque 2.2).</summary>
public static class UsuariosEndpoints
{
    public static IEndpointRouteBuilder MapUsuariosEndpoints(this IEndpointRouteBuilder app)
    {
        var usuarios = app.MapGroup("/core/usuarios").WithTags("Usuarios y roles").RequireAuthorization(PoliticasCore.GestionarUsuarios);

        usuarios.MapGet("/", async (int? pagina, int? tamano, string? buscar, string? rol, IQueryHandler<ListarUsuariosQuery, Pagina<UsuarioDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarUsuariosQuery(pagina, tamano, buscar, rol), ct)).ToHttp())
            .WithName("ListarUsuarios")
            .Produces<Pagina<UsuarioDto>>();

        usuarios.MapGet("/sugerir-rol", async (Guid cargoId, IQueryHandler<SugerirRolQuery, SugerenciaRolDto> h, CancellationToken ct) =>
                (await h.Handle(new SugerirRolQuery(cargoId), ct)).ToHttp())
            .WithName("SugerirRol")
            .Produces<SugerenciaRolDto>();

        usuarios.MapGet("/permisos", async (IQueryHandler<ObtenerMatrizPermisosQuery, MatrizPermisosDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerMatrizPermisosQuery(), ct)).ToHttp())
            .WithName("ObtenerMatrizPermisos")
            .Produces<MatrizPermisosDto>();

        usuarios.MapPost("/", async (InvitarUsuarioRequest b, ICommandHandler<InvitarUsuarioCommand, InvitacionEmitidaDto> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new InvitarUsuarioCommand(b.Correo, b.Nombre, b.Rol, b.CargoId, b.TodasLasEmpresasUsuarias,
                    b.EmpresasUsuarias ?? [], b.RolSugerido, b.RazonSugerencia), ct);
                return r.IsSuccess ? Results.Created($"/api/v1/core/usuarios/{r.Value.UsuarioId}", r.Value) : r.ToHttp();
            })
            .WithName("InvitarUsuario")
            .Produces<InvitacionEmitidaDto>(StatusCodes.Status201Created);

        usuarios.MapPut("/{id:guid}", async (Guid id, CambiarUsuarioRequest b, ICommandHandler<CambiarUsuarioCommand> h, CancellationToken ct) =>
                (await h.Handle(new CambiarUsuarioCommand(id, b.Nombre, b.Rol, b.TodasLasEmpresasUsuarias, b.EmpresasUsuarias ?? [], b.Activo), ct)).ToHttp())
            .WithName("CambiarUsuario");

        usuarios.MapPost("/{id:guid}/invitacion", async (Guid id, ICommandHandler<RenovarInvitacionCommand, InvitacionEmitidaDto> h, CancellationToken ct) =>
                (await h.Handle(new RenovarInvitacionCommand(id), ct)).ToHttp())
            .WithName("RenovarInvitacion")
            .Produces<InvitacionEmitidaDto>();

        var empresas = app.MapGroup("/core/empresas-usuarias").WithTags("Empresas usuarias").RequireAuthorization(PoliticasCore.GestionarUsuarios);

        empresas.MapGet("/", async (IQueryHandler<ListarEmpresasUsuariasQuery, IReadOnlyList<EmpresaUsuariaDto>> h, CancellationToken ct) =>
                (await h.Handle(new ListarEmpresasUsuariasQuery(), ct)).ToHttp())
            .WithName("ListarEmpresasUsuarias")
            .Produces<IReadOnlyList<EmpresaUsuariaDto>>();

        empresas.MapPost("/", async (EmpresaUsuariaRequest b, ICommandHandler<AgregarEmpresaUsuariaCommand, Guid> h, CancellationToken ct) =>
            {
                var r = await h.Handle(new AgregarEmpresaUsuariaCommand(b.Nombre, b.Nit, b.Sector ?? "", b.Ciudad ?? ""), ct);
                return r.IsSuccess ? Results.Created($"/api/v1/core/empresas-usuarias/{r.Value}", new CreadoResponse(r.Value)) : r.ToHttp();
            })
            .WithName("AgregarEmpresaUsuaria")
            .Produces<CreadoResponse>(StatusCodes.Status201Created);

        empresas.MapPut("/{id:guid}", async (Guid id, EditarEmpresaUsuariaRequest b, ICommandHandler<EditarEmpresaUsuariaCommand> h, CancellationToken ct) =>
                (await h.Handle(new EditarEmpresaUsuariaCommand(id, b.Nombre, b.Sector ?? "", b.Ciudad ?? "", b.Activa), ct)).ToHttp())
            .WithName("EditarEmpresaUsuaria");

        // Sin sesión: quien recibió la invitación ve a qué empresa lo invitaron y crea su contraseña
        var invitaciones = app.MapGroup("/sesion").WithTags("Sesión");

        invitaciones.MapGet("/invitaciones/{token}", async (string token, IQueryHandler<ObtenerInvitacionQuery, InvitacionDto> h, CancellationToken ct) =>
                (await h.Handle(new ObtenerInvitacionQuery(token), ct)).ToHttp())
            .WithName("ObtenerInvitacion")
            .AllowAnonymous()
            .RequireRateLimiting(Sesiones.PoliticaIngreso)
            .Produces<InvitacionDto>();

        invitaciones.MapPost("/activar", async (ActivarCuentaRequest b, ICommandHandler<ActivarCuentaCommand> h, CancellationToken ct) =>
                (await h.Handle(new ActivarCuentaCommand(b.Token, b.Contrasena), ct)).ToHttp())
            .WithName("ActivarCuenta")
            .AllowAnonymous()
            .RequireRateLimiting(Sesiones.PoliticaIngreso);

        return app;
    }
}
