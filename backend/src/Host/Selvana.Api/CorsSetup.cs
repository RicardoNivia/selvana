namespace Selvana.Api;

internal static class CorsSetup
{
    public const string PolicyName = "SelvanaWeb";

    /// <summary>Orígenes desde Cors:AllowedOrigins; nunca AllowAnyOrigin (ver docs/arquitectura/backend.md, sección CORS).</summary>
    public static IServiceCollection AddSelvanaCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        return services.AddCors(o => o.AddPolicy(PolicyName, p => p
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id")
            .AllowCredentials()));
    }
}
