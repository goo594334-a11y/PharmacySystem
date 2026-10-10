using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class SalesReturnItemConfiguration : IEntityTypeConfiguration<SalesReturnItem>
{
    public void Configure(EntityTypeBuilder<SalesReturnItem> builder)
    {
        builder.ToTable("SalesReturnItems", t =>
        {
            t.HasCheckConstraint("CK_SalesReturnItem_Quantity_Positive", "[Quantity] > 0");
        });

        builder.HasKey(sri => sri.Id);

        builder.Property(sri => sri.Quantity)
            .IsRequired();

        // Indexes
        builder.HasIndex(sri => sri.SalesReturnId);
        builder.HasIndex(sri => sri.SaleItemId);
        builder.HasIndex(sri => sri.BatchId);

        // Relationships
        builder.HasOne(sri => sri.SalesReturn)
            .WithMany(sr => sr.ReturnItems)
            .HasForeignKey(sri => sri.SalesReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sri => sri.SaleItem)
            .WithMany(si => si.SalesReturnItems)
            .HasForeignKey(sri => sri.SaleItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sri => sri.Batch)
            .WithMany(b => b.SalesReturnItems)
            .HasForeignKey(sri => sri.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
