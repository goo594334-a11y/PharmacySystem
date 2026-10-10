using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.ViewModels.Suppliers;
using PharmacySystem.Resources;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class SuppliersController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _loc;

    public SuppliersController(PharmacyDbContext dbContext, IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Suppliers
            .AsNoTracking()
            .Include(s => s.Purchases)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(s => s.Name.Contains(trimmed) || (s.Phone != null && s.Phone.Contains(trimmed)) || (s.Email != null && s.Email.Contains(trimmed)));
        }

        var projected = query.OrderBy(s => s.Name).Select(s => new SupplierListItemViewModel
        {
            Id = s.Id,
            Name = s.Name,
            Phone = s.Phone,
            Email = s.Email,
            PurchasesCount = s.Purchases.Count,
            IsActive = s.IsActive
        });

        var paginated = await PaginatedList<SupplierListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);
        ViewBag.Search = search;

        return View(paginated);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateSupplierViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSupplierViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var supplier = new Supplier
        {
            Name = model.Name.Trim(),
            Phone = model.Phone?.Trim(),
            Email = model.Email?.Trim(),
            Address = model.Address?.Trim(),
            Notes = model.Notes?.Trim(),
            IsActive = model.IsActive
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["SupplierCreatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { id }, cancellationToken);
        if (supplier == null)
        {
            return NotFound();
        }

        var model = new EditSupplierViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditSupplierViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { model.Id }, cancellationToken);
        if (supplier == null)
        {
            return NotFound();
        }

        supplier.Name = model.Name.Trim();
        supplier.Phone = model.Phone?.Trim();
        supplier.Email = model.Email?.Trim();
        supplier.Address = model.Address?.Trim();
        supplier.Notes = model.Notes?.Trim();
        supplier.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["SupplierUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .Include(s => s.Purchases)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier == null)
        {
            return NotFound();
        }

        var model = new SupplierDetailsViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            TotalPurchasesCount = supplier.Purchases.Count,
            TotalPurchasesAmount = supplier.Purchases.Sum(p => p.TotalAmount),
            RecentPurchases = supplier.Purchases
                .OrderByDescending(p => p.PurchaseDate)
                .Take(5)
                .Select(p => new SupplierPurchaseItemViewModel
                {
                    Id = p.Id,
                    InvoiceNumber = p.InvoiceNumber,
                    PurchaseDate = p.PurchaseDate,
                    TotalAmount = p.TotalAmount,
                    Status = p.Status.ToString()
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .Include(s => s.Purchases)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier == null)
        {
            return NotFound();
        }

        if (supplier.Purchases.Any())
        {
            TempData.SetError(_loc["OperationFailed"], _loc["CannotDeleteSupplierWithPurchasesNotice"]);
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Suppliers.Remove(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["SupplierDeletedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }
}
