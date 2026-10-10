using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.ViewModels.Inventory;
using PharmacySystem.Resources;
using PharmacySystem.Services;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class InventoryController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IInventoryService _inventoryService;
    private readonly IStringLocalizer<SharedResource> _loc;

    public InventoryController(
        PharmacyDbContext dbContext,
        IInventoryService inventoryService,
        IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        ProductInventoryStatus? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Batches)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(p => p.Name.Contains(trimmed) || (p.Barcode != null && p.Barcode.Contains(trimmed)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var projected = query.OrderBy(p => p.Name).Select(p => new InventoryProductListItemViewModel
        {
            ProductId = p.Id,
            ProductName = p.Name,
            Barcode = p.Barcode,
            CategoryName = p.Category.Name,
            SellingPrice = p.SellingPrice,
            MinimumStockLevel = p.MinimumStockLevel,
            TotalStock = p.Batches.Sum(b => b.QuantityOnHand),
            NonExpiredStock = p.Batches.Where(b => b.ExpiryDate >= today).Sum(b => b.QuantityOnHand),
            ExpiredStock = p.Batches.Where(b => b.ExpiryDate < today).Sum(b => b.QuantityOnHand),
            Status = p.Batches.Where(b => b.ExpiryDate >= today).Sum(b => b.QuantityOnHand) == 0
                ? ProductInventoryStatus.OutOfStock
                : p.Batches.Where(b => b.ExpiryDate >= today).Sum(b => b.QuantityOnHand) <= p.MinimumStockLevel
                    ? ProductInventoryStatus.LowStock
                    : ProductInventoryStatus.Normal
        });

        if (status.HasValue)
        {
            projected = projected.Where(p => p.Status == status.Value);
        }

        var paginated = await PaginatedList<InventoryProductListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Categories = new SelectList(await _dbContext.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken), "Id", "Name", categoryId);
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.Status = status;

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> Batches(
        string? search,
        int? locationId,
        bool? expiredOnly,
        int page = 1,
        int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryThreshold = today.AddDays(90);

        var query = _dbContext.Batches
            .AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Location)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(b => b.BatchNumber.Contains(trimmed) || b.Product.Name.Contains(trimmed));
        }

        if (locationId.HasValue)
        {
            query = query.Where(b => b.LocationId == locationId.Value);
        }

        if (expiredOnly == true)
        {
            query = query.Where(b => b.ExpiryDate < today);
        }

        var projected = query.OrderBy(b => b.ExpiryDate).Select(b => new BatchListItemViewModel
        {
            Id = b.Id,
            ProductId = b.ProductId,
            ProductName = b.Product.Name,
            BatchNumber = b.BatchNumber,
            LocationCode = b.Location.Code,
            LocationName = b.Location.Name,
            ExpiryDate = b.ExpiryDate,
            DaysRemaining = b.ExpiryDate.DayNumber - today.DayNumber,
            QuantityOnHand = b.QuantityOnHand,
            PurchasePrice = b.PurchasePrice,
            IsExpired = b.ExpiryDate < today,
            IsNearExpiry = b.ExpiryDate >= today && b.ExpiryDate <= nearExpiryThreshold
        });

        var paginated = await PaginatedList<BatchListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Locations = new SelectList(await _dbContext.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Code).ToListAsync(cancellationToken), "Id", "Code", locationId);
        ViewBag.Search = search;
        ViewBag.LocationId = locationId;
        ViewBag.ExpiredOnly = expiredOnly;

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(CancellationToken cancellationToken)
    {
        var lowStockList = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        return View(lowStockList);
    }

    [HttpGet]
    public async Task<IActionResult> Expiry(int days = 90, CancellationToken cancellationToken = default)
    {
        var nearExpiryBatches = await _inventoryService.GetNearExpiryBatchesAsync(days, cancellationToken);
        var expiredBatches = await _inventoryService.GetExpiredBatchesWithStockAsync(cancellationToken);

        ViewBag.DaysThreshold = days;
        ViewBag.NearExpiryBatches = nearExpiryBatches;
        ViewBag.ExpiredBatches = expiredBatches;

        return View();
    }

    [HttpGet]
    public IActionResult NearExpiry(int days = 90) => RedirectToAction(nameof(Expiry), new { days });

    [HttpGet]
    public IActionResult Expired() => RedirectToAction(nameof(Expiry));
}
