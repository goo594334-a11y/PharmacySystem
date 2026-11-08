namespace PharmacySystem.Models.Entities;

public enum PharmacyStatus
{
    Active = 1,
    PendingApproval = 2,
    Rejected = 3,
    Suspended = 4
}

/// <summary>
/// Represents an independent pharmacy organization/tenant.
/// Each pharmacy has its own isolated inventory, sales, purchases, and staff.
/// </summary>
public class Pharmacy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PharmacyStatus Status
    {
        get
        {
            if (Code != null && Code.StartsWith("STATUS:PENDING", StringComparison.OrdinalIgnoreCase))
                return PharmacyStatus.PendingApproval;
            if (Code != null && Code.StartsWith("STATUS:REJECTED", StringComparison.OrdinalIgnoreCase))
                return PharmacyStatus.Rejected;
            if (Code != null && Code.StartsWith("STATUS:SUSPENDED", StringComparison.OrdinalIgnoreCase))
                return PharmacyStatus.Suspended;
            if (!IsActive)
                return PharmacyStatus.PendingApproval;
            return PharmacyStatus.Active;
        }
    }

    public bool IsPendingApproval => Status == PharmacyStatus.PendingApproval;
    public bool IsRejected => Status == PharmacyStatus.Rejected;
    public bool IsSuspended => Status == PharmacyStatus.Suspended;

    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Location> Locations { get; set; } = new List<Location>();
    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
