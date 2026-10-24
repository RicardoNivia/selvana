using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Selvana.Infrastructure.Common.Inbox;

/// <summary>Evento recibido por HTTP, pendiente de procesar. El Id es el del evento: los duplicados se ignoran.</summary>
public sealed class InboxMessage
{
    public const int MaximoIntentos = 5;

    public Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset ReceivedOn { get; init; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public int Intentos { get; set; }
    public string? Error { get; set; }
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasMaxLength(500);
        builder.Property(m => m.Content).HasColumnType("jsonb");
        builder.Property(m => m.Error).HasMaxLength(2000);
        builder.HasIndex(m => m.ProcessedOn);
    }
}
