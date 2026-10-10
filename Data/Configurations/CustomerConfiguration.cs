using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Phone)
            .HasMaxLength(30);

        builder.Property(c => c.Address)
            .HasMaxLength(250);

        builder.Property(c => c.Notes)
            .HasMaxLength(500);

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Index on Name and Phone for customer lookups
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.Phone);

        // Relationships (Customer is optional for a Sale)
        builder.HasMany(c => c.Sales)
            .WithOne(s => s.Customer)
            .HasForeignKey(s => s.CustomerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
