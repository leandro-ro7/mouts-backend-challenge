using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.SaleNumber).IsRequired().HasMaxLength(30);
        builder.HasIndex(s => s.SaleNumber).IsUnique();

        // Nullable idempotency key — unique when present, ignored when null.
        builder.Property(s => s.IdempotencyKey).HasColumnType("uuid");
        builder.HasIndex(s => s.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.Property(s => s.SaleDate).IsRequired();

        // External Identities — plain columns, no FK to User or Branch domains
        builder.Property(s => s.CustomerId).IsRequired();
        builder.Property(s => s.CustomerName).IsRequired().HasMaxLength(150);
        // GIN trigram index supports case-insensitive ILIKE with wildcard prefix (%value%).
        // Requires pg_trgm extension (enabled in the corresponding migration).
        builder.HasIndex(s => s.CustomerName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_sales_customername_trgm");
        builder.Property(s => s.BranchId).IsRequired();
        builder.Property(s => s.BranchName).IsRequired().HasMaxLength(150);

        builder.Property(s => s.TotalAmount).HasColumnType("numeric(18,2)");
        builder.Property(s => s.IsCancelled).IsRequired().HasDefaultValue(false);

        // Optimistic concurrency: EF Core includes "AND RowVersion = @original" in UPDATE WHERE.
        // DbUpdateConcurrencyException is thrown if the row was modified between the load and save.
        builder.Property(s => s.RowVersion).IsConcurrencyToken();

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");
        builder.HasKey(i => i.Id);
        // IDs are always set in .NET (Guid.NewGuid() in constructor).
        // ValueGeneratedNever prevents EF Core from misclassifying new items as Modified
        // when DetectChanges processes navigation-collection changes after ReplaceItems.
        builder.Property(i => i.Id).HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(i => i.SaleId).IsRequired();

        // External Identity — no FK to Product domain
        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.ProductName).IsRequired().HasMaxLength(200);

        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).IsRequired().HasColumnType("numeric(18,2)");

        // DiscountRate stored as a decimal via value converter.
        // This avoids a separate ChangeTracker entry (as OwnsOne would create), which
        // causes a double-delete on the InMemory provider when cascade-deleting items.
        builder.Property(i => i.Discount)
            .HasConversion(d => d.Value, v => DiscountRate.FromValue(v))
            .HasColumnName("Discount")
            .HasColumnType("numeric(5,4)")
            .IsRequired();

        builder.Property(i => i.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.IsCancelled).IsRequired().HasDefaultValue(false);
    }
}
