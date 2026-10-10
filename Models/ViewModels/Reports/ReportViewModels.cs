namespace PharmacySystem.Models.ViewModels.Reports;

public class SalesReportItemViewModel
{
    public DateTime SaleDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemsCount { get; set; }
}

public class SalesReportViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalInvoices { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalItemsSold { get; set; }
    public List<SalesReportItemViewModel> Invoices { get; set; } = new();
}

public class PurchaseReportItemViewModel
{
    public DateTime PurchaseDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PurchaseReportViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalPurchases { get; set; }
    public decimal TotalExpenditure { get; set; }
    public List<PurchaseReportItemViewModel> Purchases { get; set; } = new();
}

public class CategoryStockBreakdownViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductsCount { get; set; }
    public int TotalUnits { get; set; }
}

public class InventoryReportViewModel
{
    public int TotalActiveProducts { get; set; }
    public int TotalBatchesCount { get; set; }
    public int TotalStockUnits { get; set; }
    public decimal TotalInventoryValuation { get; set; }
    public int LowStockProductsCount { get; set; }
    public int ExpiredBatchesCount { get; set; }
    public List<CategoryStockBreakdownViewModel> CategoryBreakdown { get; set; } = new();
}

public class ExpiryReportItemViewModel
{
    public string BatchNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int DaysRemaining { get; set; }
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal TotalCost => Quantity * PurchasePrice;
    public bool IsExpired { get; set; }
}

public class ExpiryReportViewModel
{
    public int DaysThreshold { get; set; }
    public int TotalBatchesAtRisk { get; set; }
    public int TotalUnitsAtRisk { get; set; }
    public decimal TotalCostAtRisk { get; set; }
    public List<ExpiryReportItemViewModel> Batches { get; set; } = new();
}
