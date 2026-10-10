using PharmacySystem.Enums;

namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents an operational expense of the pharmacy.
/// </summary>
public class Expense
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime ExpenseDate { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
