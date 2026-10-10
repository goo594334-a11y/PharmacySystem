using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class PurchaseReturnItemConfiguration : IEntityTypeConfiguration<PurchaseReturnItem>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnItem> builder)
    {
        builder.ToTable("PurchaseReturnItems", t =>
        {
            t.HasCheckConstraint("CK_PurchaseReturnItem_Quantity_Positive", "[Quantity] > 0");
        });

        builder.HasKey(pri => pri.Id);

        builder.Property(pri => pri.Quantity)
            .IsRequired();

        // Indexes
        builder.HasIndex(pri => pri.PurchaseReturnId);
        builder.HasIndex(pri => pri.PurchaseItemId);
        builder.HasIndex(pri => pri.BatchId);

        // Relationships
        builder.HasOne(pri => pri.PurchaseReturn)
            .WithMany(pr => pr.ReturnItems)
            .HasForeignKey(pri => pri.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pri => pri.PurchaseItem)
            .WithMany(pi => pi.PurchaseReturnItems)
            .HasForeignKey(pri => pri.PurchaseItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pri => pri.Batch)
            .WithMany(b => b.PurchaseReturnItems)
            .HasForeignKey(pri => pri.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
