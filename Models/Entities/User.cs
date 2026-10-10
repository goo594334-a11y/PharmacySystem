using PharmacySystem.Enums;

namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a system user (Admin or Pharmacist).
/// Note: Pharmacist handles both dispensing and cashier responsibilities.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int AccessFailedCount { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<SalesReturn> SalesReturns { get; set; } = new List<SalesReturn>();
    public ICollection<PurchaseReturn> PurchaseReturns { get; set; } = new List<PurchaseReturn>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
