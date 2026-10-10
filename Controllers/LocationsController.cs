using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.ViewModels.Locations;
using PharmacySystem.Resources;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class LocationsController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _loc;

    public LocationsController(PharmacyDbContext dbContext, IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Locations
            .AsNoTracking()
            .Include(l => l.Batches)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(l => l.Code.Contains(trimmed) || l.Name.Contains(trimmed));
        }

        var projected = query.OrderBy(l => l.Code).Select(l => new LocationListItemViewModel
        {
            Id = l.Id,
            Code = l.Code,
            Name = l.Name,
            BatchesCount = l.Batches.Count,
            IsActive = l.IsActive
        });

        var paginated = await PaginatedList<LocationListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);
        ViewBag.Search = search;

        return View(paginated);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateLocationViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateLocationViewModel model, CancellationToken cancellationToken)
    {
        var normalizedCode = model.Code.Trim();
        var exists = await _dbContext.Locations.AnyAsync(l => l.Code == normalizedCode, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Code), _loc["LocationCodeAlreadyExists"]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var location = new Location
        {
            Code = normalizedCode,
            Name = model.Name.Trim(),
            IsActive = model.IsActive
        };

        _dbContext.Locations.Add(location);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["LocationCreatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var location = await _dbContext.Locations.FindAsync(new object[] { id }, cancellationToken);
        if (location == null)
        {
            return NotFound();
        }

        var model = new EditLocationViewModel
        {
            Id = location.Id,
            Code = location.Code,
            Name = location.Name,
            IsActive = location.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditLocationViewModel model, CancellationToken cancellationToken)
    {
        var normalizedCode = model.Code.Trim();
        var exists = await _dbContext.Locations.AnyAsync(l => l.Code == normalizedCode && l.Id != model.Id, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Code), _loc["LocationCodeAlreadyExists"]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var location = await _dbContext.Locations.FindAsync(new object[] { model.Id }, cancellationToken);
        if (location == null)
        {
            return NotFound();
        }

        location.Code = normalizedCode;
        location.Name = model.Name.Trim();
        location.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["LocationUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var location = await _dbContext.Locations
            .Include(l => l.Batches)
            .Include(l => l.PurchaseItems)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (location == null)
        {
            return NotFound();
        }

        if (location.Batches.Any() || location.PurchaseItems.Any())
        {
            TempData.SetError(_loc["OperationFailed"], _loc["CannotDeleteLocationWithBatchesNotice"]);
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Locations.Remove(location);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["LocationDeletedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }
}
