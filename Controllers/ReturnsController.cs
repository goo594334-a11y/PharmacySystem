using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.ViewModels.Returns;
using PharmacySystem.Resources;
using PharmacySystem.Services;
using PharmacySystem.Services.Auth;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class ReturnsController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IReturnTransactionService _returnTransactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _loc;
    private readonly ILogger<ReturnsController> _logger;

    public ReturnsController(
        PharmacyDbContext dbContext,
        IReturnTransactionService returnTransactionService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> loc,
        ILogger<ReturnsController> logger)
    {
        _dbContext = dbContext;
        _returnTransactionService = returnTransactionService;
        _currentUserService = currentUserService;
        _loc = loc;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Sales));

    // ==========================================
    // SALES RETURNS
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Sales(string? search, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SalesReturns
            .AsNoTracking()
            .Include(sr => sr.Sale)
            .Include(sr => sr.User)
            .Include(sr => sr.ReturnItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(sr => sr.Sale.InvoiceNumber.Contains(trimmed) || (sr.Reason != null && sr.Reason.Contains(trimmed)));
        }

        var projected = query.OrderByDescending(sr => sr.ReturnDate).Select(sr => new SalesReturnListItemViewModel
        {
            Id = sr.Id,
            SaleInvoiceNumber = sr.Sale.InvoiceNumber,
            ReturnDate = sr.ReturnDate,
            TotalAmount = sr.TotalAmount,
            Reason = sr.Reason,
            ProcessedByName = sr.User.FullName,
            ItemsCount = sr.ReturnItems.Count
        });

        var paginated = await PaginatedList<SalesReturnListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);
        ViewBag.Search = search;

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> CreateSalesReturn(int saleId, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
            .Include(s => s.SaleItems)
                .ThenInclude(si => si.BatchAllocations)
                    .ThenInclude(ba => ba.Batch)
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

        if (sale == null)
        {
            return NotFound();
        }

        var model = new CreateSalesReturnViewModel
        {
            SaleId = sale.Id,
            SaleInvoiceNumber = sale.InvoiceNumber,
            Items = sale.SaleItems.SelectMany(si => si.BatchAllocations.Select(ba => new SalesReturnItemInputViewModel
            {
                SaleItemId = si.Id,
                ProductName = si.Product.Name,
                BatchId = ba.BatchId,
                BatchNumber = ba.Batch.BatchNumber,
                SoldQuantity = ba.Quantity,
                UnitPrice = si.UnitPrice,
                QuantityToReturn = 0
            })).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSalesReturn(CreateSalesReturnViewModel model, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var returnedItems = model.Items.Where(i => i.QuantityToReturn > 0).ToList();
        if (returnedItems.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _loc["AtLeastOneItemMustBeReturned"]);
            return View(model);
        }

        try
        {
            var request = new CreateSalesReturnRequest(
                model.SaleId,
                _currentUserService.UserId.Value,
                model.Reason,
                returnedItems.Select(i => new SalesReturnItemInput(i.SaleItemId, i.BatchId, i.QuantityToReturn)).ToList()
            );

            await _returnTransactionService.ProcessSalesReturnAsync(request, cancellationToken);
            TempData.SetSuccess(_loc["OperationSuccess"], _loc["SalesReturnProcessedAndStockRestored"]);

            return RedirectToAction(nameof(Sales));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // ==========================================
    // PURCHASE RETURNS
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Purchases(string? search, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PurchaseReturns
            .AsNoTracking()
            .Include(pr => pr.Purchase)
            .Include(pr => pr.User)
            .Include(pr => pr.ReturnItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(pr => pr.Purchase.InvoiceNumber.Contains(trimmed) || (pr.Reason != null && pr.Reason.Contains(trimmed)));
        }

        var projected = query.OrderByDescending(pr => pr.ReturnDate).Select(pr => new PurchaseReturnListItemViewModel
        {
            Id = pr.Id,
            PurchaseInvoiceNumber = pr.Purchase.InvoiceNumber,
            ReturnDate = pr.ReturnDate,
            TotalAmount = pr.TotalAmount,
            Reason = pr.Reason,
            ProcessedByName = pr.User.FullName,
            ItemsCount = pr.ReturnItems.Count
        });

        var paginated = await PaginatedList<PurchaseReturnListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);
        ViewBag.Search = search;

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> CreatePurchaseReturn(int purchaseId, CancellationToken cancellationToken)
    {
        var purchase = await _dbContext.Purchases
            .AsNoTracking()
            .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                    .ThenInclude(prod => prod.Batches)
            .FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken);

        if (purchase == null)
        {
            return NotFound();
        }

        var model = new CreatePurchaseReturnViewModel
        {
            PurchaseId = purchase.Id,
            PurchaseInvoiceNumber = purchase.InvoiceNumber,
            Items = purchase.PurchaseItems.Select(pi =>
            {
                var matchingBatch = pi.Product.Batches.FirstOrDefault(b => b.BatchNumber == pi.BatchNumber && b.LocationId == pi.LocationId);
                return new PurchaseReturnItemInputViewModel
                {
                    PurchaseItemId = pi.Id,
                    ProductName = pi.Product.Name,
                    BatchId = matchingBatch?.Id ?? 0,
                    BatchNumber = pi.BatchNumber,
                    PurchasedQuantity = pi.Quantity,
                    CurrentStock = matchingBatch?.QuantityOnHand ?? 0,
                    UnitPurchasePrice = pi.UnitPurchasePrice,
                    QuantityToReturn = 0
                };
            }).Where(i => i.BatchId > 0 && i.CurrentStock > 0).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePurchaseReturn(CreatePurchaseReturnViewModel model, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var returnedItems = model.Items.Where(i => i.QuantityToReturn > 0).ToList();
        if (returnedItems.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _loc["AtLeastOneItemMustBeReturned"]);
            return View(model);
        }

        try
        {
            var request = new CreatePurchaseReturnRequest(
                model.PurchaseId,
                _currentUserService.UserId.Value,
                model.Reason,
                returnedItems.Select(i => new PurchaseReturnItemInput(i.PurchaseItemId, i.BatchId, i.QuantityToReturn)).ToList()
            );

            await _returnTransactionService.ProcessPurchaseReturnAsync(request, cancellationToken);
            TempData.SetSuccess(_loc["OperationSuccess"], _loc["PurchaseReturnProcessedAndStockDeducted"]);

            return RedirectToAction(nameof(Purchases));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
