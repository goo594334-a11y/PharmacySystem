namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a master product catalog entry.
/// Note: Quantity, ExpiryDate, Location, and PurchasePrice belong to the Batch concept, not Product.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public int CategoryId { get; set; }
    public decimal SellingPrice { get; set; }
    public int MinimumStockLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

    /// <summary>
    /// Evaluates if current total available stock is at or below the minimum stock level threshold.
    /// Low stock is an alert/reporting concept and does not prevent selling.
    /// </summary>
    public bool IsLowStock(int currentQuantity) => currentQuantity <= MinimumStockLevel;
}
