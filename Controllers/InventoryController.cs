using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.Entities;
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
    public async Task<IActionResult> Expired(
        string? search,
        int? locationId,
        int page = 1,
        int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _dbContext.Batches
            .AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Location)
            .Where(b => b.ExpiryDate < today)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(b => b.BatchNumber.Contains(trimmed) || b.Product.Name.Contains(trimmed) || (b.Product.Barcode != null && b.Product.Barcode.Contains(trimmed)));
        }

        if (locationId.HasValue)
        {
            query = query.Where(b => b.LocationId == locationId.Value);
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
            IsExpired = true,
            IsNearExpiry = false
        });

        var paginated = await PaginatedList<BatchListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Locations = new SelectList(await _dbContext.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Code).ToListAsync(cancellationToken), "Id", "Code", locationId);
        ViewBag.Search = search;
        ViewBag.LocationId = locationId;

        return View(paginated);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisposeExpiredBatch(
        int batchId,
        int quantityToDispose,
        string disposalReason,
        CancellationToken cancellationToken = default)
    {
        if (quantityToDispose <= 0)
        {
            TempData.SetError(_loc["OperationFailed"], _loc["DisposalQuantityMustBePositive"]);
            return RedirectToAction(nameof(Expired));
        }

        if (string.IsNullOrWhiteSpace(disposalReason))
        {
            TempData.SetError(_loc["OperationFailed"], _loc["DisposalReasonRequired"]);
            return RedirectToAction(nameof(Expired));
        }

        var batch = await _dbContext.Batches
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch == null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (batch.ExpiryDate >= today)
        {
            TempData.SetError(_loc["OperationFailed"], _loc["CannotDisposeNonExpiredBatch"]);
            return RedirectToAction(nameof(Expired));
        }

        if (quantityToDispose > batch.QuantityOnHand)
        {
            TempData.SetError(_loc["OperationFailed"], _loc["DisposalQuantityExceedsAvailable"]);
            return RedirectToAction(nameof(Expired));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            batch.QuantityOnHand -= quantityToDispose;

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var currentUserName = User.FindFirst("DisplayName")?.Value ?? User.Identity?.Name ?? "System";

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = int.TryParse(currentUserId, out var uid) ? uid : null,
                Action = "DisposeExpiredStock",
                EntityName = nameof(Batch),
                EntityId = batch.Id.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            TempData.SetSuccess(_loc["OperationSuccess"], _loc["ExpiredStockDisposedSuccessfully"]);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData.SetError(_loc["OperationFailed"], _loc["ErrorOccurredDuringDisposal"]);
        }

        return RedirectToAction(nameof(Expired));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(
        int batchId,
        int newQuantity,
        string reason,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        if (newQuantity < 0)
        {
            TempData.SetError(_loc["OperationFailed"], _loc["StockQuantityCannotBeNegative"]);
            return Redirect(returnUrl ?? Url.Action(nameof(Batches))!);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData.SetError(_loc["OperationFailed"], _loc["StockAdjustmentReasonRequired"]);
            return Redirect(returnUrl ?? Url.Action(nameof(Batches))!);
        }

        var batch = await _dbContext.Batches
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch == null)
        {
            return NotFound();
        }

        var oldQty = batch.QuantityOnHand;
        if (oldQty == newQuantity)
        {
            TempData.SetInfo(_loc["Notice"], _loc["StockQuantityUnchanged"]);
            return Redirect(returnUrl ?? Url.Action(nameof(Batches))!);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            batch.QuantityOnHand = newQuantity;

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = int.TryParse(currentUserId, out var uid) ? uid : null,
                PharmacyId = batch.Product.PharmacyId,
                Action = "StockAdjustment",
                EntityName = nameof(Batch),
                EntityId = batch.Id.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            TempData.SetSuccess(_loc["OperationSuccess"], _loc["StockAdjustedSuccessfully"]);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData.SetError(_loc["OperationFailed"], _loc["ErrorOccurredDuringStockAdjustment"]);
        }

        return Redirect(returnUrl ?? Url.Action(nameof(Batches))!);
    }
}
