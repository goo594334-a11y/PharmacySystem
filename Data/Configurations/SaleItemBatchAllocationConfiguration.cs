using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class SaleItemBatchAllocationConfiguration : IEntityTypeConfiguration<SaleItemBatchAllocation>
{
    public void Configure(EntityTypeBuilder<SaleItemBatchAllocation> builder)
    {
        builder.ToTable("SaleItemBatchAllocations", t =>
        {
            t.HasCheckConstraint("CK_SaleItemBatchAllocation_Quantity_Positive", "[Quantity] > 0");
            t.HasCheckConstraint("CK_SaleItemBatchAllocation_UnitCost_NonNegative", "[UnitCost] >= 0");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Quantity)
            .IsRequired();

        builder.Property(a => a.UnitCost)
            .HasPrecision(18, 2);

        // Indexes
        builder.HasIndex(a => new { a.SaleItemId, a.BatchId });
        builder.HasIndex(a => a.BatchId);

        // Relationships
        builder.HasOne(a => a.SaleItem)
            .WithMany(si => si.BatchAllocations)
            .HasForeignKey(a => a.SaleItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Batch)
            .WithMany(b => b.SaleItemBatchAllocations)
            .HasForeignKey(a => a.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
