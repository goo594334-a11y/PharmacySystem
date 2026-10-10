using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("PurchaseReturns", t =>
        {
            t.HasCheckConstraint("CK_PurchaseReturn_TotalAmount_NonNegative", "[TotalAmount] >= 0");
        });

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.ReturnDate)
            .IsRequired();

        builder.Property(pr => pr.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(pr => pr.Reason)
            .IsRequired()
            .HasMaxLength(500);

        // Required index: Referenced transaction + date
        builder.HasIndex(pr => new { pr.PurchaseId, pr.ReturnDate });

        builder.HasIndex(pr => pr.UserId);

        // Relationships
        builder.HasOne(pr => pr.Purchase)
            .WithMany(p => p.PurchaseReturns)
            .HasForeignKey(pr => pr.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.User)
            .WithMany(u => u.PurchaseReturns)
            .HasForeignKey(pr => pr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(pr => pr.ReturnItems)
            .WithOne(pri => pri.PurchaseReturn)
            .HasForeignKey(pri => pri.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
