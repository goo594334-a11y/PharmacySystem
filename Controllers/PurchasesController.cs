using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.ViewModels.Purchases;
using PharmacySystem.Resources;
using PharmacySystem.Services;
using PharmacySystem.Services.Auth;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class PurchasesController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IPurchaseTransactionService _purchaseTransactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _loc;

    public PurchasesController(
        PharmacyDbContext dbContext,
        IPurchaseTransactionService purchaseTransactionService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _purchaseTransactionService = purchaseTransactionService;
        _currentUserService = currentUserService;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? supplierId,
        PurchaseStatus? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Purchases
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(p => p.InvoiceNumber.Contains(trimmed) || p.Supplier.Name.Contains(trimmed));
        }

        if (supplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == supplierId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var projected = query.OrderByDescending(p => p.PurchaseDate).Select(p => new PurchaseListItemViewModel
        {
            Id = p.Id,
            InvoiceNumber = p.InvoiceNumber,
            SupplierName = p.Supplier.Name,
            PurchaseDate = p.PurchaseDate,
            TotalAmount = p.TotalAmount,
            Status = p.Status,
            ItemsCount = p.PurchaseItems.Count
        });

        var paginated = await PaginatedList<PurchaseListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Suppliers = new SelectList(await _dbContext.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken), "Id", "Name", supplierId);
        ViewBag.Search = search;
        ViewBag.SupplierId = supplierId;
        ViewBag.Status = status;

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateDropdownsAsync(cancellationToken);

        var model = new CreatePurchaseViewModel
        {
            InvoiceNumber = $"PUR-{DateTime.UtcNow:yyyyMMddHHmmss}"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseViewModel model, CancellationToken cancellationToken)
    {
        if (model == null || model.Items == null || model.Items.Count == 0)
        {
            return BadRequest(new { success = false, message = _loc["PurchaseMustContainAtLeastOneItem"].Value });
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var request = new CreatePurchaseRequest(
                model.SupplierId,
                _currentUserService.UserId.Value,
                model.InvoiceNumber,
                model.Notes,
                model.Items.Select(i => new PurchaseItemInput(
                    i.ProductId,
                    i.Quantity,
                    i.UnitPurchasePrice,
                    i.BatchNumber,
                    i.ExpiryDate,
                    i.LocationId
                )).ToList()
            );

            var purchase = await _purchaseTransactionService.CreateDraftPurchaseAsync(request, cancellationToken);
            return Ok(new { success = true, purchaseId = purchase.Id, message = _loc["PurchaseCreatedSuccessfully"].Value });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var purchase = await _dbContext.Purchases
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.User)
            .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
            .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Location)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (purchase == null)
        {
            return NotFound();
        }

        var model = new PurchaseDetailsViewModel
        {
            Id = purchase.Id,
            InvoiceNumber = purchase.InvoiceNumber,
            SupplierName = purchase.Supplier.Name,
            SupplierPhone = purchase.Supplier.Phone,
            PurchaseDate = purchase.PurchaseDate,
            Status = purchase.Status,
            TotalAmount = purchase.TotalAmount,
            Notes = purchase.Notes,
            ReceivedByName = purchase.User.FullName,
            Items = purchase.PurchaseItems.Select(pi => new PurchaseDetailsItemViewModel
            {
                Id = pi.Id,
                ProductId = pi.ProductId,
                ProductName = pi.Product.Name,
                BatchNumber = pi.BatchNumber,
                ExpiryDate = pi.ExpiryDate,
                LocationCode = pi.Location.Code,
                LocationName = pi.Location.Name,
                Quantity = pi.Quantity,
                UnitPurchasePrice = pi.UnitPurchasePrice
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        try
        {
            var confirmed = await _purchaseTransactionService.ConfirmPurchaseAsync(id, cancellationToken);
            TempData.SetSuccess(_loc["OperationSuccess"], _loc["PurchaseConfirmedAndStockAdded"]);
        }
        catch (Exception ex)
        {
            TempData.SetError(_loc["OperationFailed"], ex.Message);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateDropdownsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Suppliers = await _dbContext.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        ViewBag.Products = await _dbContext.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);
        ViewBag.Locations = await _dbContext.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Code).ToListAsync(cancellationToken);
    }
}
