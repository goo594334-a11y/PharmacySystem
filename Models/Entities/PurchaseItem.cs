namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents an item line in a purchase order.
/// Each PurchaseItem represents ONE received batch/stock lot.
/// If the same product arrives in two different batches, there are two separate PurchaseItem records.
/// </summary>
public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPurchasePrice { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int LocationId { get; set; }

    // Navigation properties
    public Purchase Purchase { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public ICollection<PurchaseReturnItem> PurchaseReturnItems { get; set; } = new List<PurchaseReturnItem>();
}
