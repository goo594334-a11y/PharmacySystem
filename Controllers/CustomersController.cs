using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.ViewModels.Customers;
using PharmacySystem.Resources;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class CustomersController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _loc;

    public CustomersController(PharmacyDbContext dbContext, IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Customers
            .AsNoTracking()
            .Include(c => c.Sales)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(c => c.Name.Contains(trimmed) || (c.Phone != null && c.Phone.Contains(trimmed)));
        }

        var projected = query.OrderBy(c => c.Name).Select(c => new CustomerListItemViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Phone = c.Phone,
            Address = c.Address,
            SalesCount = c.Sales.Count,
            IsActive = c.IsActive
        });

        var paginated = await PaginatedList<CustomerListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);
        ViewBag.Search = search;

        return View(paginated);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateCustomerViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCustomerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customer = new Customer
        {
            Name = model.Name.Trim(),
            Phone = model.Phone?.Trim(),
            Address = model.Address?.Trim(),
            Notes = model.Notes?.Trim(),
            IsActive = model.IsActive
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["CustomerCreatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers.FindAsync(new object[] { id }, cancellationToken);
        if (customer == null)
        {
            return NotFound();
        }

        var model = new EditCustomerViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Address = customer.Address,
            Notes = customer.Notes,
            IsActive = customer.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCustomerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customer = await _dbContext.Customers.FindAsync(new object[] { model.Id }, cancellationToken);
        if (customer == null)
        {
            return NotFound();
        }

        customer.Name = model.Name.Trim();
        customer.Phone = model.Phone?.Trim();
        customer.Address = model.Address?.Trim();
        customer.Notes = model.Notes?.Trim();
        customer.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["CustomerUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Include(c => c.Sales)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer == null)
        {
            return NotFound();
        }

        var model = new CustomerDetailsViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Address = customer.Address,
            Notes = customer.Notes,
            IsActive = customer.IsActive,
            TotalSalesCount = customer.Sales.Count,
            TotalPurchasedAmount = customer.Sales.Sum(s => s.TotalAmount),
            RecentSales = customer.Sales
                .OrderByDescending(s => s.SaleDate)
                .Take(5)
                .Select(s => new CustomerSaleItemViewModel
                {
                    Id = s.Id,
                    InvoiceNumber = s.InvoiceNumber,
                    SaleDate = s.SaleDate,
                    TotalAmount = s.TotalAmount,
                    PaymentMethod = s.PaymentMethod.ToString()
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .Include(c => c.Sales)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer == null)
        {
            return NotFound();
        }

        if (customer.Sales.Any())
        {
            TempData.SetError(_loc["OperationFailed"], _loc["CannotDeleteCustomerWithSalesNotice"]);
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Customers.Remove(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["CustomerDeletedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }
}
