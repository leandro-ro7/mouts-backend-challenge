using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(m => m.EventType).IsRequired().HasMaxLength(300);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredAt).IsRequired();
        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.LockedUntil);

        builder.HasIndex(m => m.ProcessedAt);
        builder.HasIndex(m => m.LockedUntil);
    }
}
