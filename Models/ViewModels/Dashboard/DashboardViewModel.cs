using PharmacySystem.Models.Entities;

namespace PharmacySystem.Models.ViewModels.Dashboard;

public class DashboardViewModel
{
    public bool IsAdmin { get; set; }

    // Today's Financial & Operational metrics
    public decimal TodaySalesAmount { get; set; }
    public int TodaySalesCount { get; set; }

    public decimal TodayPurchasesAmount { get; set; }
    public int TodayPurchasesCount { get; set; }

    public decimal TodayExpensesAmount { get; set; }
    public int TodayExpensesCount { get; set; }

    public decimal TodayProfit { get; set; }

    // Product & Inventory metrics
    public int TotalProductsCount { get; set; }
    public int TotalActiveBatchesCount { get; set; }
    public int TotalInventoryQuantity { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public int LowStockCount { get; set; }
    public int NearExpiryCount { get; set; }
    public int ExpiredBatchesCount { get; set; }

    // 7-day trends for charts (Admin)
    public List<string> TrendDates { get; set; } = new();
    public List<decimal> SalesTrend { get; set; } = new();
    public List<decimal> PurchasesTrend { get; set; } = new();
    public List<decimal> ExpensesTrend { get; set; } = new();

    // Operational streams
    public List<Sale> RecentSales { get; set; } = new();
    public List<Purchase> RecentPurchases { get; set; } = new();
    public List<ProductStockAlertItem> LowStockProducts { get; set; } = new();
    public List<BatchExpiryAlertItem> NearExpiryBatches { get; set; } = new();
    public List<BatchExpiryAlertItem> ExpiredBatches { get; set; } = new();
    public List<AuditLog> RecentActivity { get; set; } = new();
}

public class ProductStockAlertItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public int AvailableQuantity { get; set; }
    public int MinimumStockLevel { get; set; }
}

public class BatchExpiryAlertItem
{
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int QuantityOnHand { get; set; }
    public int RemainingDays { get; set; }
}
