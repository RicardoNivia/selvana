using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Selvana.Infrastructure.Common.Inbox;
using Selvana.Infrastructure.Common.Outbox;
using Selvana.SharedKernel.Messaging;

namespace Selvana.Infrastructure.Common.Persistence;

/// <summary>DbContext base de un módulo: un esquema propio de PostgreSQL y su tabla de outbox.</summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options), IUnitOfWork
{
    public string Schema { get; } = schema;

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Encola un evento de integración; se guarda en la misma transacción que el agregado.</summary>
    public void AddIntegrationEvent(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        OutboxMessages.Add(new OutboxMessage
        {
            Id = integrationEvent.Id,
            Type = integrationEvent.GetType().FullName!,
            Content = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredOn = integrationEvent.OccurredOn,
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
