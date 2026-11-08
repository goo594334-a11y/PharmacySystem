using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Product_SellingPrice_NonNegative", "[SellingPrice] >= 0");
            t.HasCheckConstraint("CK_Product_MinimumStockLevel_NonNegative", "[MinimumStockLevel] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Barcode)
            .HasMaxLength(50);

        builder.Property(p => p.SellingPrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.MinimumStockLevel)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        // Filtered unique index on Barcode (unique when not null)
        builder.HasIndex(p => p.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL");

        // Index on Name for product catalog searching
        builder.HasIndex(p => p.Name);

        // Index on CategoryId
        builder.HasIndex(p => p.CategoryId);

        // Index on PharmacyId
        builder.HasIndex(p => p.PharmacyId);

        // Relationships
        builder.HasOne(p => p.Pharmacy)
            .WithMany(ph => ph.Products)
            .HasForeignKey(p => p.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Batches)
            .WithOne(b => b.Product)
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PurchaseItems)
            .WithOne(pi => pi.Product)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.SaleItems)
            .WithOne(si => si.Product)
            .HasForeignKey(si => si.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
