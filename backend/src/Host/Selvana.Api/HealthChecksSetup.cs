namespace Selvana.Api;

internal static class HealthChecksSetup
{
    public static IServiceCollection AddSelvanaHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var checks = services.AddHealthChecks();
        var connectionString = configuration.GetConnectionString("Selvana");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            checks.AddNpgSql(connectionString, name: "postgresql");
        }

        return services;
    }
}
