namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents an item returned in a sales return.
/// References original SaleItem and Batch so stock is accurately restored.
/// </summary>
public class SalesReturnItem
{
    public int Id { get; set; }
    public int SalesReturnId { get; set; }
    public int SaleItemId { get; set; }
    public int BatchId { get; set; }
    public int Quantity { get; set; }

    // Navigation properties
    public SalesReturn SalesReturn { get; set; } = null!;
    public SaleItem SaleItem { get; set; } = null!;
    public Batch Batch { get; set; } = null!;
}
