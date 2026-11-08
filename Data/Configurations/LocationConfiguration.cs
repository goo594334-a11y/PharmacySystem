using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(l => new { l.PharmacyId, l.Code })
            .IsUnique();

        builder.HasOne(l => l.Pharmacy)
            .WithMany(ph => ph.Locations)
            .HasForeignKey(l => l.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationships
        builder.HasMany(l => l.Batches)
            .WithOne(b => b.Location)
            .HasForeignKey(b => b.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.PurchaseItems)
            .WithOne(pi => pi.Location)
            .HasForeignKey(pi => pi.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
