using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Selvana.Core.Infrastructure.Persistence;
using Selvana.Infrastructure.Common.Persistence;
using Testcontainers.PostgreSql;

namespace Selvana.Core.IntegrationTests;

public sealed class PostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public Task InitializeAsync() => DockerFactAttribute.SeOmite ? Task.CompletedTask : _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [DockerFact]
    public async Task LaMigracionCreaElOutboxEnElEsquemaCore()
    {
        await using var context = CrearContexto();

        await context.Database.MigrateAsync();

        var tablas = await context.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'core'")
            .ToListAsync();
        tablas.Should().Contain(["outbox_messages", "__ef_migrations_history"]);
    }

    [DockerFact]
    public async Task ElOutboxGuardaEventosDeIntegracion()
    {
        await using var context = CrearContexto();
        await context.Database.MigrateAsync();

        context.AddIntegrationEvent(new EventoDePrueba(Guid.NewGuid(), DateTimeOffset.UtcNow, "hola"));
        await context.SaveChangesAsync();

        var pendientes = await context.OutboxMessages.Where(m => m.ProcessedOn == null).ToListAsync();
        pendientes.Should().ContainSingle().Which.Content.Should().Contain("hola");
    }

    private CoreDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>();
        options.UseModulePostgres(_postgres.GetConnectionString(), CoreDbContext.SchemaName);
        return new CoreDbContext(options.Options);
    }

    private sealed record EventoDePrueba(Guid Id, DateTimeOffset OccurredOn, string Mensaje)
        : SharedKernel.Messaging.IIntegrationEvent;
}
