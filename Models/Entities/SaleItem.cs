namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents an item line in a sale.
/// UnitPrice preserves the product selling price at the moment of sale.
/// </summary>
public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Navigation properties
    public Sale Sale { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<SaleItemBatchAllocation> BatchAllocations { get; set; } = new List<SaleItemBatchAllocation>();
    public ICollection<SalesReturnItem> SalesReturnItems { get; set; } = new List<SalesReturnItem>();
}
