namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a pharmacy customer.
/// Customers are optional for sales (walk-in customers are supported).
/// </summary>
public class Customer : ITenantEntity
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public Pharmacy Pharmacy { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
