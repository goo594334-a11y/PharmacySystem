namespace PharmacySystem.Models.ViewModels.Inventory;

public enum ProductInventoryStatus
{
    Normal,
    LowStock,
    OutOfStock
}

public class InventoryProductListItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public int MinimumStockLevel { get; set; }
    public int TotalStock { get; set; }
    public int NonExpiredStock { get; set; }
    public int ExpiredStock { get; set; }
    public ProductInventoryStatus Status { get; set; }
}

public class BatchListItemViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int DaysRemaining { get; set; }
    public int QuantityOnHand { get; set; }
    public decimal PurchasePrice { get; set; }
    public bool IsExpired { get; set; }
    public bool IsNearExpiry { get; set; }
}
