namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a medicine/pharmaceutical goods supplier.
/// </summary>
public class Supplier : ITenantEntity
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public Pharmacy Pharmacy { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
