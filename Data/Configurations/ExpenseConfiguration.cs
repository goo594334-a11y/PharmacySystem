using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", t =>
        {
            t.HasCheckConstraint("CK_Expense_Amount_NonNegative", "[Amount] >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExpenseDate)
            .IsRequired();

        builder.Property(e => e.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(e => e.ExpenseDate);
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.UserId);

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany(u => u.Expenses)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
