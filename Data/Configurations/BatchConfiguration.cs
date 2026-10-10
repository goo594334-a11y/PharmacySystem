using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("Batches", t =>
        {
            t.HasCheckConstraint("CK_Batch_QuantityOnHand_NonNegative", "[QuantityOnHand] >= 0");
            t.HasCheckConstraint("CK_Batch_PurchasePrice_NonNegative", "[PurchasePrice] >= 0");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BatchNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.AddedDate)
            .IsRequired();

        builder.Property(b => b.ExpiryDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(b => b.QuantityOnHand)
            .IsRequired();

        builder.Property(b => b.PurchasePrice)
            .HasPrecision(18, 2);

        // Required index for FEFO query: (ProductId, ExpiryDate)
        builder.HasIndex(b => new { b.ProductId, b.ExpiryDate });

        // Index on LocationId
        builder.HasIndex(b => b.LocationId);

        // Relationships
        builder.HasOne(b => b.Product)
            .WithMany(p => p.Batches)
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Location)
            .WithMany(l => l.Batches)
            .HasForeignKey(b => b.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.SaleItemBatchAllocations)
            .WithOne(a => a.Batch)
            .HasForeignKey(a => a.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.SalesReturnItems)
            .WithOne(sri => sri.Batch)
            .HasForeignKey(sri => sri.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.PurchaseReturnItems)
            .WithOne(pri => pri.Batch)
            .HasForeignKey(pri => pri.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
