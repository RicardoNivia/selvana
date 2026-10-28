using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Selvana.Core.Application.Abstracciones;
using Selvana.Core.Application.Usuarios;
using Selvana.Reclutamiento.Application.Abstracciones;
using Selvana.Reclutamiento.Application.Portal;
using Selvana.Web.Common;

namespace Selvana.Api;

public static class Seguridad
{
    private const string EsquemaSelector = "Selvana";

    /// <summary>
    /// Sesión con cookie (ingreso con correo y contraseña en /api/v1/sesion). Si hay emisor OpenID Connect
    /// configurado (Auth:Authority), también se aceptan tokens Bearer; un selector elige según el encabezado.
    /// </summary>
    public static IServiceCollection AddSelvanaAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var conEmisor = !string.IsNullOrWhiteSpace(configuration["Auth:Authority"]);
        var auth = services.AddAuthentication(EsquemaSelector)
            .AddPolicyScheme(EsquemaSelector, "Cookie o Bearer", o => o.ForwardDefaultSelector = http =>
                conEmisor && http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme
                    : Sesiones.Esquema)
            .AddCookie(Sesiones.Esquema, o =>
            {
                o.Cookie.Name = Sesiones.NombreCookie;
                o.Cookie.HttpOnly = true;
                // Front y API comparten sitio (localhost en desarrollo, el mismo dominio en producción): Strict corta el CSRF
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                // Es una API: sin redirecciones a páginas de login
                o.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; },
                    OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; },
                };
            });

        // Portal de la persona (ADR 0013): su propia cookie y su propio esquema, nunca sirve para la consola
        auth.AddCookie(SesionesPortal.Esquema, o =>
        {
            o.Cookie.Name = SesionesPortal.NombreCookie;
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromDays(14);
            o.SlidingExpiration = true;
            o.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; },
                OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; },
            };
        });

        if (conEmisor)
        {
            auth.AddJwtBearer(o =>
            {
                o.Authority = configuration["Auth:Authority"];
                o.Audience = configuration["Auth:Audience"];
                o.RequireHttpsMetadata = !environment.IsDevelopment();
            });
        }

        // Políticas por acción: los roles salen de la matriz de permisos del dominio
        services.AddAuthorization(o =>
        {
            o.AddPolicy(SesionesPortal.Politica, p => p.AddAuthenticationSchemes(SesionesPortal.Esquema).RequireAuthenticatedUser());
            foreach (var (politica, roles) in PoliticasCore.Todas())
            {
                o.AddPolicy(politica, p => p.RequireRole(roles));
            }
        });
        services.AddHttpContextAccessor();
        services.AddScoped<ContextoUsuarioHttp>();
        services.AddScoped<IContextoUsuario>(sp => sp.GetRequiredService<ContextoUsuarioHttp>());
        services.AddScoped<IUsuarioActual>(sp => sp.GetRequiredService<ContextoUsuarioHttp>());
        services.AddScoped<Vinculacion.Application.Abstracciones.IUsuarioActual>(sp => sp.GetRequiredService<ContextoUsuarioHttp>());
        services.AddScoped<Expediente.Application.Abstracciones.IUsuarioActual>(sp => sp.GetRequiredService<ContextoUsuarioHttp>());
        services.AddScoped<IPersonaActual, PersonaActualHttp>();
        return services;
    }
}

/// <summary>Usuario, empresa y nombre de quien hace la petición, tomados de los claims de la sesión.</summary>
internal sealed class ContextoUsuarioHttp(IHttpContextAccessor accesor)
    : IContextoUsuario, IUsuarioActual, Vinculacion.Application.Abstracciones.IUsuarioActual, Expediente.Application.Abstracciones.IUsuarioActual
{
    private ClaimsPrincipal Usuario => accesor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid EmpresaId => Guid.TryParse(Usuario.FindFirstValue(Sesiones.ClaimEmpresa), out var id)
        ? id
        : throw new InvalidOperationException("El usuario no tiene empresa asignada.");

    public Guid? UsuarioId => Guid.TryParse(Usuario.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string Nombre => Usuario.Identity?.Name ?? "Sin nombre";

    /// <summary>Rol de la matriz de permisos (Administrador, GerenteRrhh, Analista, Lider…).</summary>
    public string Rol => Usuario.FindFirstValue(ClaimTypes.Role) ?? "";

    /// <summary>null = ve todas las empresas usuarias; si no, solo las de la lista (sin claim: ninguna).</summary>
    public IReadOnlyCollection<Guid>? EmpresasUsuarias => Usuario.FindFirstValue(Sesiones.ClaimEmpresasUsuarias) switch
    {
        "*" => null,
        null or "" => [],
        var ids => [.. ids.Split(',').Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty)],
    };
}

/// <summary>La persona con sesión en el portal (cookie del esquema Portal).</summary>
internal sealed class PersonaActualHttp(IHttpContextAccessor accesor) : IPersonaActual
{
    public Guid? CuentaId => Guid.TryParse(accesor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
