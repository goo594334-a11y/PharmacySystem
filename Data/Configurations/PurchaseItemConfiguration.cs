using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("PurchaseItems", t =>
        {
            t.HasCheckConstraint("CK_PurchaseItem_Quantity_Positive", "[Quantity] > 0");
            t.HasCheckConstraint("CK_PurchaseItem_UnitPurchasePrice_NonNegative", "[UnitPurchasePrice] >= 0");
        });

        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.Quantity)
            .IsRequired();

        builder.Property(pi => pi.UnitPurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(pi => pi.BatchNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(pi => pi.ExpiryDate)
            .IsRequired()
            .HasColumnType("date");

        // Indexes
        builder.HasIndex(pi => pi.PurchaseId);
        builder.HasIndex(pi => pi.ProductId);
        builder.HasIndex(pi => pi.LocationId);

        // Relationships
        builder.HasOne(pi => pi.Purchase)
            .WithMany(p => p.PurchaseItems)
            .HasForeignKey(pi => pi.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pi => pi.Product)
            .WithMany(p => p.PurchaseItems)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pi => pi.Location)
            .WithMany(l => l.PurchaseItems)
            .HasForeignKey(pi => pi.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(pi => pi.PurchaseReturnItems)
            .WithOne(pri => pri.PurchaseItem)
            .HasForeignKey(pri => pri.PurchaseItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
