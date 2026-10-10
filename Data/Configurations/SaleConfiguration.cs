using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales", t =>
        {
            t.HasCheckConstraint("CK_Sale_TotalAmount_NonNegative", "[TotalAmount] >= 0");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.SaleDate)
            .IsRequired();

        builder.Property(s => s.PaymentMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.TotalAmount)
            .HasPrecision(18, 2);

        // Indexes
        builder.HasIndex(s => s.InvoiceNumber)
            .IsUnique();

        builder.HasIndex(s => s.SaleDate);
        builder.HasIndex(s => s.CustomerId);
        builder.HasIndex(s => s.UserId);

        // Relationships
        builder.HasOne(s => s.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.User)
            .WithMany(u => u.Sales)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.SaleItems)
            .WithOne(si => si.Sale)
            .HasForeignKey(si => si.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.SalesReturns)
            .WithOne(sr => sr.Sale)
            .HasForeignKey(sr => sr.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
