namespace PharmacySystem.Models.Entities;

/// <summary>
/// Tracks stock depletion from a specific batch for a sale line item.
/// Supports FEFO splitting across multiple batches for accurate inventory and cost tracking.
/// </summary>
public class SaleItemBatchAllocation
{
    public int Id { get; set; }
    public int SaleItemId { get; set; }
    public int BatchId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }

    // Navigation properties
    public SaleItem SaleItem { get; set; } = null!;
    public Batch Batch { get; set; } = null!;
}
