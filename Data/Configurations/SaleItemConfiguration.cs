using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems", t =>
        {
            t.HasCheckConstraint("CK_SaleItem_Quantity_Positive", "[Quantity] > 0");
            t.HasCheckConstraint("CK_SaleItem_UnitPrice_NonNegative", "[UnitPrice] >= 0");
        });

        builder.HasKey(si => si.Id);

        builder.Property(si => si.Quantity)
            .IsRequired();

        builder.Property(si => si.UnitPrice)
            .HasPrecision(18, 2);

        // Indexes
        builder.HasIndex(si => si.SaleId);
        builder.HasIndex(si => si.ProductId);

        // Relationships
        builder.HasOne(si => si.Sale)
            .WithMany(s => s.SaleItems)
            .HasForeignKey(si => si.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(si => si.Product)
            .WithMany(p => p.SaleItems)
            .HasForeignKey(si => si.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(si => si.BatchAllocations)
            .WithOne(a => a.SaleItem)
            .HasForeignKey(a => a.SaleItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(si => si.SalesReturnItems)
            .WithOne(sri => sri.SaleItem)
            .HasForeignKey(sri => sri.SaleItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
