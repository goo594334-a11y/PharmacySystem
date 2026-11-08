using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases", t =>
        {
            t.HasCheckConstraint("CK_Purchase_TotalAmount_NonNegative", "[TotalAmount] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.PurchaseDate)
            .IsRequired();

        builder.Property(p => p.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(p => p.Notes)
            .HasMaxLength(500);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Required index: (SupplierId, PurchaseDate)
        builder.HasIndex(p => new { p.SupplierId, p.PurchaseDate });
        builder.HasIndex(p => new { p.PharmacyId, p.InvoiceNumber })
            .IsUnique();

        builder.HasIndex(p => p.PurchaseDate);
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.PharmacyId);

        // Relationships
        builder.HasOne(p => p.Pharmacy)
            .WithMany(ph => ph.Purchases)
            .HasForeignKey(p => p.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Supplier)
            .WithMany(s => s.Purchases)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Purchases)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PurchaseItems)
            .WithOne(pi => pi.Purchase)
            .HasForeignKey(pi => pi.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PurchaseReturns)
            .WithOne(pr => pr.Purchase)
            .HasForeignKey(pr => pr.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
