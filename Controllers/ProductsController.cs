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
using PharmacySystem.Models.ViewModels.Products;
using PharmacySystem.Resources;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class ProductsController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _loc;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        PharmacyDbContext dbContext,
        IStringLocalizer<SharedResource> loc,
        ILogger<ProductsController> logger)
    {
        _dbContext = dbContext;
        _loc = loc;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        bool? lowStockOnly,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = _dbContext.Products
            .AsNoTracking()
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

        var projected = query.OrderBy(p => p.Name).Select(p => new ProductListItemViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Barcode = p.Barcode,
            CategoryName = p.Category.Name,
            SellingPrice = p.SellingPrice,
            MinimumStockLevel = p.MinimumStockLevel,
            TotalStock = p.Batches.Sum(b => b.QuantityOnHand),
            NonExpiredStock = p.Batches.Where(b => b.ExpiryDate >= today).Sum(b => b.QuantityOnHand),
            IsLowStock = p.Batches.Where(b => b.ExpiryDate >= today).Sum(b => b.QuantityOnHand) <= p.MinimumStockLevel,
            IsActive = p.IsActive
        });

        if (lowStockOnly == true)
        {
            projected = projected.Where(p => p.IsLowStock);
        }

        var paginated = await PaginatedList<ProductListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Categories = new SelectList(await _dbContext.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken), "Id", "Name", categoryId);
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.LowStockOnly = lowStockOnly;

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateCategoriesDropDownListAsync(null, cancellationToken);
        return View(new CreateProductViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(model.Barcode))
        {
            var trimmedBarcode = model.Barcode.Trim();
            var barcodeExists = await _dbContext.Products.AnyAsync(p => p.Barcode == trimmedBarcode, cancellationToken);
            if (barcodeExists)
            {
                ModelState.AddModelError(nameof(model.Barcode), _loc["BarcodeAlreadyExists"]);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesDropDownListAsync(model.CategoryId, cancellationToken);
            return View(model);
        }

        var product = new Product
        {
            Name = model.Name.Trim(),
            Barcode = string.IsNullOrWhiteSpace(model.Barcode) ? null : model.Barcode.Trim(),
            CategoryId = model.CategoryId,
            SellingPrice = model.SellingPrice,
            MinimumStockLevel = model.MinimumStockLevel,
            Description = model.Description?.Trim(),
            IsActive = model.IsActive
        };

        _dbContext.Products.Add(product);
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "CreateProduct",
            EntityName = nameof(Product),
            EntityId = product.Name,
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["ProductCreatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        var model = new EditProductViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            CategoryId = product.CategoryId,
            SellingPrice = product.SellingPrice,
            MinimumStockLevel = product.MinimumStockLevel,
            Description = product.Description,
            IsActive = product.IsActive
        };

        await PopulateCategoriesDropDownListAsync(product.CategoryId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProductViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(model.Barcode))
        {
            var trimmedBarcode = model.Barcode.Trim();
            var barcodeExists = await _dbContext.Products.AnyAsync(p => p.Barcode == trimmedBarcode && p.Id != model.Id, cancellationToken);
            if (barcodeExists)
            {
                ModelState.AddModelError(nameof(model.Barcode), _loc["BarcodeAlreadyExists"]);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesDropDownListAsync(model.CategoryId, cancellationToken);
            return View(model);
        }

        var product = await _dbContext.Products.FindAsync(new object[] { model.Id }, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        product.Name = model.Name.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(model.Barcode) ? null : model.Barcode.Trim();
        product.CategoryId = model.CategoryId;
        product.SellingPrice = model.SellingPrice;
        product.MinimumStockLevel = model.MinimumStockLevel;
        product.Description = model.Description?.Trim();
        product.IsActive = model.IsActive;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateProduct",
            EntityName = nameof(Product),
            EntityId = product.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["ProductUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Batches)
                .ThenInclude(b => b.Location)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null)
        {
            return NotFound();
        }

        var nonExpiredStock = product.Batches.Where(b => b.ExpiryDate >= today).Sum(b => b.QuantityOnHand);
        var totalStock = product.Batches.Sum(b => b.QuantityOnHand);

        var model = new ProductDetailsViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            CategoryName = product.Category.Name,
            SellingPrice = product.SellingPrice,
            MinimumStockLevel = product.MinimumStockLevel,
            TotalStock = totalStock,
            NonExpiredStock = nonExpiredStock,
            IsLowStock = product.IsLowStock(nonExpiredStock),
            Description = product.Description,
            IsActive = product.IsActive,
            Batches = product.Batches
                .OrderBy(b => b.ExpiryDate)
                .Select(b => new ProductBatchItemViewModel
                {
                    Id = b.Id,
                    BatchNumber = b.BatchNumber,
                    LocationCode = b.Location.Code,
                    LocationName = b.Location.Name,
                    ExpiryDate = b.ExpiryDate,
                    DaysRemaining = b.GetRemainingDays(today),
                    QuantityOnHand = b.QuantityOnHand,
                    PurchasePrice = b.PurchasePrice,
                    IsExpired = b.IsExpired(today)
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(p => p.Batches)
            .Include(p => p.SaleItems)
            .Include(p => p.PurchaseItems)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null)
        {
            return NotFound();
        }

        // Restrict delete if product has existing batches or transaction history
        if (product.Batches.Any() || product.SaleItems.Any() || product.PurchaseItems.Any())
        {
            TempData.SetError(_loc["OperationFailed"], _loc["CannotDeleteProductWithHistoryNotice"]);
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Products.Remove(product);
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "DeleteProduct",
            EntityName = nameof(Product),
            EntityId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["ProductDeletedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // BARCODE SCANNER / POS LOOKUP ENDPOINT
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> LookupByBarcode(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Json(new { found = false });
        }

        var trimmed = query.Trim();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Search by exact barcode first, then fallback to name match
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Batches)
                .ThenInclude(b => b.Location)
            .Where(p => p.IsActive)
            .FirstOrDefaultAsync(p => p.Barcode == trimmed || p.Name == trimmed, cancellationToken);

        if (product == null)
        {
            return Json(new { found = false });
        }

        // Valid non-expired batches ordered by FEFO
        var validBatches = product.Batches
            .Where(b => b.ExpiryDate >= today && b.QuantityOnHand > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToList();

        var availableQty = validBatches.Sum(b => b.QuantityOnHand);
        var primaryBatch = validBatches.FirstOrDefault();

        var result = new ProductBarcodeLookupResult
        {
            Id = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            SellingPrice = product.SellingPrice,
            AvailableStock = availableQty,
            PhysicalLocation = primaryBatch?.Location.Code != null ? $"{primaryBatch.Location.Code} ({primaryBatch.Location.Name})" : null,
            NearestExpiry = primaryBatch?.ExpiryDate
        };

        return Json(new { found = true, data = result });
    }

    private async Task PopulateCategoriesDropDownListAsync(int? selectedCategoryId, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedCategoryId);
    }
}
