using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Core.Infrastructure.Persistence;

/// <summary>Solo para `dotnet ef`: permite crear migraciones del módulo sin levantar la API.</summary>
internal sealed class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Selvana")
            ?? "Host=localhost;Port=5432;Database=selvana;Username=postgres";

        var options = new DbContextOptionsBuilder<CoreDbContext>();
        options.UseModulePostgres(connectionString, CoreDbContext.SchemaName);

        return new CoreDbContext(options.Options);
    }
}
