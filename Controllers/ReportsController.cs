using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Models.ViewModels.Reports;
using PharmacySystem.Resources;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class ReportsController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _loc;

    public ReportsController(PharmacyDbContext dbContext, IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _loc = loc;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Sales(DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        var start = fromDate?.Date ?? DateTime.Today.AddDays(-30);
        var end = toDate?.Date ?? DateTime.Today;
        var endInclusive = end.AddDays(1).AddTicks(-1);

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.SaleDate >= start && s.SaleDate <= endInclusive)
            .Include(s => s.Customer)
            .Include(s => s.User)
            .Include(s => s.SaleItems)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync(cancellationToken);

        var model = new SalesReportViewModel
        {
            FromDate = start,
            ToDate = end,
            TotalInvoices = sales.Count,
            TotalRevenue = sales.Sum(s => s.TotalAmount),
            TotalItemsSold = sales.Sum(s => s.SaleItems.Sum(si => si.Quantity)),
            Invoices = sales.Select(s => new SalesReportItemViewModel
            {
                SaleDate = s.SaleDate,
                InvoiceNumber = s.InvoiceNumber,
                CustomerName = s.Customer != null ? s.Customer.Name : _loc["WalkInCustomer"].Value,
                CashierName = s.User.FullName,
                TotalAmount = s.TotalAmount,
                ItemsCount = s.SaleItems.Count
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Purchases(DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        var start = fromDate?.Date ?? DateTime.Today.AddDays(-30);
        var end = toDate?.Date ?? DateTime.Today;
        var endInclusive = end.AddDays(1).AddTicks(-1);

        var purchases = await _dbContext.Purchases
            .AsNoTracking()
            .Where(p => p.PurchaseDate >= start && p.PurchaseDate <= endInclusive)
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync(cancellationToken);

        var model = new PurchaseReportViewModel
        {
            FromDate = start,
            ToDate = end,
            TotalPurchases = purchases.Count,
            TotalExpenditure = purchases.Where(p => p.Status == PurchaseStatus.Confirmed).Sum(p => p.TotalAmount),
            Purchases = purchases.Select(p => new PurchaseReportItemViewModel
            {
                PurchaseDate = p.PurchaseDate,
                InvoiceNumber = p.InvoiceNumber,
                SupplierName = p.Supplier.Name,
                TotalAmount = p.TotalAmount,
                Status = p.Status.ToString()
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Inventory(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Batches)
            .ToListAsync(cancellationToken);

        var allBatches = await _dbContext.Batches
            .AsNoTracking()
            .Where(b => b.QuantityOnHand > 0)
            .ToListAsync(cancellationToken);

        var model = new InventoryReportViewModel
        {
            TotalActiveProducts = products.Count,
            TotalBatchesCount = allBatches.Count,
            TotalStockUnits = allBatches.Sum(b => b.QuantityOnHand),
            TotalInventoryValuation = allBatches.Sum(b => b.QuantityOnHand * b.PurchasePrice),
            LowStockProductsCount = products.Count(p => p.IsLowStock(p.Batches.Where(b => b.ExpiryDate >= today).Sum(b => b.QuantityOnHand))),
            ExpiredBatchesCount = allBatches.Count(b => b.ExpiryDate < today),
            CategoryBreakdown = products
                .GroupBy(p => p.Category.Name)
                .Select(g => new CategoryStockBreakdownViewModel
                {
                    CategoryName = g.Key,
                    ProductsCount = g.Count(),
                    TotalUnits = g.Sum(p => p.Batches.Sum(b => b.QuantityOnHand))
                })
                .OrderByDescending(c => c.TotalUnits)
                .ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Expiry(int days = 90, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var thresholdDate = today.AddDays(days);

        var riskBatches = await _dbContext.Batches
            .AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Location)
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate <= thresholdDate)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);

        var model = new ExpiryReportViewModel
        {
            DaysThreshold = days,
            TotalBatchesAtRisk = riskBatches.Count,
            TotalUnitsAtRisk = riskBatches.Sum(b => b.QuantityOnHand),
            TotalCostAtRisk = riskBatches.Sum(b => b.QuantityOnHand * b.PurchasePrice),
            Batches = riskBatches.Select(b => new ExpiryReportItemViewModel
            {
                BatchNumber = b.BatchNumber,
                ProductName = b.Product.Name,
                LocationCode = b.Location.Code,
                ExpiryDate = b.ExpiryDate,
                DaysRemaining = b.ExpiryDate.DayNumber - today.DayNumber,
                Quantity = b.QuantityOnHand,
                PurchasePrice = b.PurchasePrice,
                IsExpired = b.ExpiryDate < today
            }).ToList()
        };

        return View(model);
    }
}
