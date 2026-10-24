using Microsoft.EntityFrameworkCore;

namespace Selvana.Infrastructure.Common.Persistence;

public static class ModuleDbContextOptions
{
    /// <summary>PostgreSQL con snake_case y el historial de migraciones dentro del esquema del módulo.</summary>
    public static DbContextOptionsBuilder UseModulePostgres(
        this DbContextOptionsBuilder options, string connectionString, string schema) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention();
}
