using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.Phone)
            .HasMaxLength(30);

        builder.Property(s => s.Address)
            .HasMaxLength(250);

        builder.Property(s => s.Email)
            .HasMaxLength(150);

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Index on Name for supplier lookups
        builder.HasIndex(s => s.Name);

        // Relationships
        builder.HasMany(s => s.Purchases)
            .WithOne(p => p.Supplier)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
