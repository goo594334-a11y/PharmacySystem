namespace PharmacySystem.Models.Entities;

/// <summary>
/// Physical storage location where stock batches are placed in the pharmacy.
/// </summary>
public class Location
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}
