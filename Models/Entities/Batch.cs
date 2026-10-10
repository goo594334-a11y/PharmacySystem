namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents an individual stock lot/batch for a product at a physical location.
/// Inventory is derived from Batch.QuantityOnHand.
/// </summary>
public class Batch
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int LocationId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime AddedDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int QuantityOnHand { get; set; }
    public decimal PurchasePrice { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public ICollection<SaleItemBatchAllocation> SaleItemBatchAllocations { get; set; } = new List<SaleItemBatchAllocation>();
    public ICollection<SalesReturnItem> SalesReturnItems { get; set; } = new List<SalesReturnItem>();
    public ICollection<PurchaseReturnItem> PurchaseReturnItems { get; set; } = new List<PurchaseReturnItem>();

    /// <summary>
    /// Checks whether this batch is expired as of a given date (defaulting to today).
    /// Used for FEFO ordering and validation.
    /// </summary>
    public bool IsExpired(DateOnly? asOfDate = null) =>
        ExpiryDate < (asOfDate ?? DateOnly.FromDateTime(DateTime.Today));

    /// <summary>
    /// Calculates remaining days until expiry at runtime.
    /// Note: Days are calculated at runtime and never persisted as a database column.
    /// </summary>
    public int GetRemainingDays(DateOnly? asOfDate = null) =>
        ExpiryDate.DayNumber - (asOfDate ?? DateOnly.FromDateTime(DateTime.Today)).DayNumber;
}
