using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("SalesReturns", t =>
        {
            t.HasCheckConstraint("CK_SalesReturn_TotalAmount_NonNegative", "[TotalAmount] >= 0");
        });

        builder.HasKey(sr => sr.Id);

        builder.Property(sr => sr.ReturnDate)
            .IsRequired();

        builder.Property(sr => sr.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(sr => sr.Reason)
            .IsRequired()
            .HasMaxLength(500);

        // Required index: Referenced transaction + date
        builder.HasIndex(sr => new { sr.SaleId, sr.ReturnDate });

        builder.HasIndex(sr => sr.UserId);

        // Relationships
        builder.HasOne(sr => sr.Sale)
            .WithMany(s => s.SalesReturns)
            .HasForeignKey(sr => sr.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sr => sr.User)
            .WithMany(u => u.SalesReturns)
            .HasForeignKey(sr => sr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(sr => sr.ReturnItems)
            .WithOne(sri => sri.SalesReturn)
            .HasForeignKey(sri => sri.SalesReturnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
