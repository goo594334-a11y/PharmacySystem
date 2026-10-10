using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.ViewModels.Sales;
using PharmacySystem.Resources;
using PharmacySystem.Services;
using PharmacySystem.Services.Auth;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class SalesController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly ISaleTransactionService _saleTransactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _loc;
    private readonly ILogger<SalesController> _logger;

    public SalesController(
        PharmacyDbContext dbContext,
        ISaleTransactionService saleTransactionService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> loc,
        ILogger<SalesController> logger)
    {
        _dbContext = dbContext;
        _saleTransactionService = saleTransactionService;
        _currentUserService = currentUserService;
        _loc = loc;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? customerId,
        PaymentMethod? paymentMethod,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.User)
            .Include(s => s.SaleItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(s => s.InvoiceNumber.Contains(trimmed) || (s.Customer != null && s.Customer.Name.Contains(trimmed)));
        }

        if (customerId.HasValue)
        {
            query = query.Where(s => s.CustomerId == customerId.Value);
        }

        if (paymentMethod.HasValue)
        {
            query = query.Where(s => s.PaymentMethod == paymentMethod.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(s => s.SaleDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(s => s.SaleDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var projected = query.OrderByDescending(s => s.SaleDate).Select(s => new SaleListItemViewModel
        {
            Id = s.Id,
            InvoiceNumber = s.InvoiceNumber,
            CustomerName = s.Customer != null ? s.Customer.Name : "Walk-in Customer",
            SaleDate = s.SaleDate,
            TotalAmount = s.TotalAmount,
            PaymentMethod = s.PaymentMethod,
            ItemsCount = s.SaleItems.Count,
            SoldByName = s.User.FullName
        });

        var paginated = await PaginatedList<SaleListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Customers = await _dbContext.Customers.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.Search = search;
        ViewBag.CustomerId = customerId;
        ViewBag.PaymentMethod = paymentMethod;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> Pos(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        ViewBag.Customers = await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // Fetch products with active stock for quick manual search
        ViewBag.Products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Batches)
                .ThenInclude(b => b.Location)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Barcode,
                p.SellingPrice,
                AvailableStock = p.Batches.Where(b => b.ExpiryDate >= today && b.QuantityOnHand > 0).Sum(b => b.QuantityOnHand),
                Location = p.Batches.Where(b => b.ExpiryDate >= today && b.QuantityOnHand > 0).OrderBy(b => b.ExpiryDate).Select(b => b.Location.Code).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return View();
    }

    // Alias for New Sale
    [HttpGet]
    public IActionResult New() => RedirectToAction(nameof(Pos));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessSale([FromBody] CreateSaleInputViewModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            _logger.LogWarning("ProcessSale model validation failed: {Errors}", errors);
            return BadRequest(new { success = false, message = errors });
        }

        if (input == null || input.Items == null || input.Items.Count == 0)
        {
            return BadRequest(new { success = false, message = _loc["SaleMustContainAtLeastOneItem"].Value });
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

            var request = new CreateSaleRequest(
                input.CustomerId,
                _currentUserService.UserId.Value,
                invoiceNumber,
                input.PaymentMethod,
                input.Items.Select(i => new SaleItemRequest(i.ProductId, i.Quantity)).ToList()
            );

            // Atomic transaction with FEFO batch allocation and inventory reduction
            var sale = await _saleTransactionService.ProcessSaleAsync(request, cancellationToken);

            return Ok(new
            {
                success = true,
                saleId = sale.Id,
                invoiceNumber = sale.InvoiceNumber,
                totalAmount = sale.TotalAmount,
                redirectUrl = Url.Action("Details", "Sales", new { id = sale.Id })
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sale transaction rejected: {Message}", ex.Message);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.User)
            .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
            .Include(s => s.SaleItems)
                .ThenInclude(si => si.BatchAllocations)
                    .ThenInclude(ba => ba.Batch)
                        .ThenInclude(b => b.Location)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (sale == null)
        {
            return NotFound();
        }

        var model = new SaleDetailsViewModel
        {
            Id = sale.Id,
            InvoiceNumber = sale.InvoiceNumber,
            CustomerName = sale.Customer != null ? sale.Customer.Name : _loc["WalkInCustomer"].Value,
            CustomerPhone = sale.Customer?.Phone,
            SaleDate = sale.SaleDate,
            PaymentMethod = sale.PaymentMethod,
            TotalAmount = sale.TotalAmount,
            SoldByName = sale.User.FullName,
            Items = sale.SaleItems.Select(si => new SaleDetailsItemViewModel
            {
                ProductId = si.ProductId,
                ProductName = si.Product.Name,
                UnitPrice = si.UnitPrice,
                Quantity = si.Quantity,
                Allocations = si.BatchAllocations.Select(ba => new SaleItemAllocationViewModel
                {
                    BatchNumber = ba.Batch.BatchNumber,
                    LocationCode = ba.Batch.Location.Code,
                    ExpiryDate = ba.Batch.ExpiryDate,
                    Quantity = ba.Quantity
                }).ToList()
            }).ToList()
        };

        return View(model);
    }
}
