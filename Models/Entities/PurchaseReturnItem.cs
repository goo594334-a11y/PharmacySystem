namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents an item returned in a purchase return.
/// References original PurchaseItem and Batch so stock is accurately reduced.
/// </summary>
public class PurchaseReturnItem
{
    public int Id { get; set; }
    public int PurchaseReturnId { get; set; }
    public int PurchaseItemId { get; set; }
    public int BatchId { get; set; }
    public int Quantity { get; set; }

    // Navigation properties
    public PurchaseReturn PurchaseReturn { get; set; } = null!;
    public PurchaseItem PurchaseItem { get; set; } = null!;
    public Batch Batch { get; set; } = null!;
}
