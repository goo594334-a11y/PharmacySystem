using PharmacySystem.Enums;

namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents an operational expense of the pharmacy.
/// </summary>
public class Expense : ITenantEntity
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public Pharmacy Pharmacy { get; set; } = null!;
    public int UserId { get; set; }
    public DateTime ExpenseDate { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
