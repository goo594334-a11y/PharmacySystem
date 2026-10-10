using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Models.ViewModels.Dashboard;
using PharmacySystem.Services.Auth;

namespace PharmacySystem.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardDataAsync(CancellationToken cancellationToken = default);
}

public class DashboardService : IDashboardService
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IInventoryService _inventoryService;
    private readonly ICurrentUserService _currentUserService;

    public DashboardService(
        PharmacyDbContext dbContext,
        IInventoryService inventoryService,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
        _currentUserService = currentUserService;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync(CancellationToken cancellationToken = default)
    {
        var model = new DashboardViewModel
        {
            IsAdmin = _currentUserService.IsAdmin
        };

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var todayDateOnly = DateOnly.FromDateTime(DateTime.Today);

        // 1. Sales today
        var todaySalesQuery = _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);

        model.TodaySalesCount = await todaySalesQuery.CountAsync(cancellationToken);
        model.TodaySalesAmount = await todaySalesQuery.SumAsync(s => (decimal?)s.TotalAmount, cancellationToken) ?? 0m;

        // 2. Purchases & Expenses today (Admin only)
        if (model.IsAdmin)
        {
            var todayPurchasesQuery = _dbContext.Purchases
                .AsNoTracking()
                .Where(p => p.PurchaseDate >= today && p.PurchaseDate < tomorrow);

            model.TodayPurchasesCount = await todayPurchasesQuery.CountAsync(cancellationToken);
            model.TodayPurchasesAmount = await todayPurchasesQuery.SumAsync(p => (decimal?)p.TotalAmount, cancellationToken) ?? 0m;

            var todayExpensesQuery = _dbContext.Expenses
                .AsNoTracking()
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow);

            model.TodayExpensesCount = await todayExpensesQuery.CountAsync(cancellationToken);
            model.TodayExpensesAmount = await todayExpensesQuery.SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

            // Calculate actual gross profit earned from sold items today (Selling Price - Cost of Batches)
            var todayProfitQuery = await _dbContext.SaleItemBatchAllocations
                .AsNoTracking()
                .Where(a => a.SaleItem.Sale.SaleDate >= today && a.SaleItem.Sale.SaleDate < tomorrow)
                .Select(a => (decimal?)(a.Quantity * (a.SaleItem.UnitPrice - a.UnitCost)))
                .SumAsync(cancellationToken) ?? 0m;

            // In case of any edge cases, ensure it never displays a negative value in the dashboard
            model.TodayProfit = Math.Max(0m, todayProfitQuery);

            // Recent Purchases (latest 5)
            model.RecentPurchases = await _dbContext.Purchases
                .AsNoTracking()
                .Include(p => p.Supplier)
                .Include(p => p.User)
                .OrderByDescending(p => p.PurchaseDate)
                .Take(5)
                .ToListAsync(cancellationToken);

            // 7-day trend calculations
            for (int i = 6; i >= 0; i--)
            {
                var dayStart = today.AddDays(-i);
                var dayEnd = dayStart.AddDays(1);

                var sAmt = await _dbContext.Sales.AsNoTracking()
                    .Where(s => s.SaleDate >= dayStart && s.SaleDate < dayEnd)
                    .SumAsync(s => (decimal?)s.TotalAmount, cancellationToken) ?? 0m;

                var pAmt = await _dbContext.Purchases.AsNoTracking()
                    .Where(p => p.PurchaseDate >= dayStart && p.PurchaseDate < dayEnd)
                    .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken) ?? 0m;

                var eAmt = await _dbContext.Expenses.AsNoTracking()
                    .Where(e => e.ExpenseDate >= dayStart && e.ExpenseDate < dayEnd)
                    .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

                model.TrendDates.Add(dayStart.ToString("MM/dd"));
                model.SalesTrend.Add(sAmt);
                model.PurchasesTrend.Add(pAmt);
                model.ExpensesTrend.Add(eAmt);
            }
        }

        // 3. Products & Inventory metrics
        model.TotalProductsCount = await _dbContext.Products
            .AsNoTracking()
            .CountAsync(p => p.IsActive, cancellationToken);

        var activeBatches = await _dbContext.Batches
            .AsNoTracking()
            .Where(b => b.QuantityOnHand > 0)
            .ToListAsync(cancellationToken);

        model.TotalActiveBatchesCount = activeBatches.Count;
        model.TotalInventoryQuantity = activeBatches.Sum(b => b.QuantityOnHand);
        model.TotalInventoryValue = activeBatches.Sum(b => b.QuantityOnHand * b.PurchasePrice);

        // 4. Low stock alerts
        var lowStockList = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        model.LowStockCount = lowStockList.Count;
        model.LowStockProducts = lowStockList
            .Take(5)
            .Select(s => new ProductStockAlertItem
            {
                ProductId = s.ProductId,
                ProductName = s.ProductName,
                AvailableQuantity = s.AvailableNonExpiredQuantity,
                MinimumStockLevel = s.MinimumStockLevel
            })
            .ToList();

        // 5. Near expiry batches (within 90 days)
        var nearExpiryList = await _inventoryService.GetNearExpiryBatchesAsync(90, cancellationToken);
        model.NearExpiryCount = nearExpiryList.Count;
        model.NearExpiryBatches = nearExpiryList
            .Take(5)
            .Select(b => new BatchExpiryAlertItem
            {
                BatchId = b.Id,
                BatchNumber = b.BatchNumber,
                ProductName = b.Product?.Name ?? string.Empty,
                LocationCode = b.Location?.Code ?? string.Empty,
                ExpiryDate = b.ExpiryDate,
                QuantityOnHand = b.QuantityOnHand,
                RemainingDays = b.GetRemainingDays(todayDateOnly)
            })
            .ToList();

        // 6. Expired batches
        var expiredList = await _inventoryService.GetExpiredBatchesWithStockAsync(cancellationToken);
        model.ExpiredBatchesCount = expiredList.Count;
        model.ExpiredBatches = expiredList
            .Take(5)
            .Select(b => new BatchExpiryAlertItem
            {
                BatchId = b.Id,
                BatchNumber = b.BatchNumber,
                ProductName = b.Product?.Name ?? string.Empty,
                LocationCode = b.Location?.Code ?? string.Empty,
                ExpiryDate = b.ExpiryDate,
                QuantityOnHand = b.QuantityOnHand,
                RemainingDays = b.GetRemainingDays(todayDateOnly)
            })
            .ToList();

        // 7. Recent sales (latest 5)
        model.RecentSales = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.User)
            .OrderByDescending(s => s.SaleDate)
            .Take(5)
            .ToListAsync(cancellationToken);

        // 8. Recent audit activity (Admin only)
        if (model.IsAdmin)
        {
            model.RecentActivity = await _dbContext.AuditLogs
                .AsNoTracking()
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync(cancellationToken);
        }

        return model;
    }
}
