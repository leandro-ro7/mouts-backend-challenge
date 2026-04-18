using Ambev.DeveloperEvaluation.Domain.Entities;
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

        builder.Property(s => s.SaleDate).IsRequired();

        // External Identities — plain columns, no FK to User or Branch domains
        builder.Property(s => s.CustomerId).IsRequired();
        builder.Property(s => s.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(s => s.BranchId).IsRequired();
        builder.Property(s => s.BranchName).IsRequired().HasMaxLength(150);

        builder.Property(s => s.TotalAmount).HasColumnType("numeric(18,2)");
        builder.Property(s => s.IsCancelled).IsRequired().HasDefaultValue(false);
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
        builder.Property(i => i.Id).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.SaleId).IsRequired();

        // External Identity — no FK to Product domain
        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.ProductName).IsRequired().HasMaxLength(200);

        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.Discount).IsRequired().HasColumnType("numeric(5,4)");
        builder.Property(i => i.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.IsCancelled).IsRequired().HasDefaultValue(false);
    }
}
