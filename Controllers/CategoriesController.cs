using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.ViewModels.Categories;
using PharmacySystem.Resources;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class CategoriesController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _loc;

    public CategoriesController(PharmacyDbContext dbContext, IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories
            .AsNoTracking()
            .Include(c => c.Products)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(c => c.Name.Contains(trimmed) || (c.Description != null && c.Description.Contains(trimmed)));
        }

        var projected = query.OrderBy(c => c.Name).Select(c => new CategoryListItemViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            ProductsCount = c.Products.Count,
            IsActive = c.IsActive
        });

        var paginated = await PaginatedList<CategoryListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);
        ViewBag.Search = search;

        return View(paginated);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateCategoryViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCategoryViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = new Category
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            IsActive = model.IsActive
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["CategoryCreatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickCreate([FromBody] QuickCreateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = _loc["RequiredField"].Value });
        }

        var trimmedName = request.Name.Trim();
        var existing = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (existing != null)
        {
            return Ok(new { success = true, id = existing.Id, name = existing.Name, alreadyExisted = true });
        }

        var category = new Category
        {
            Name = trimmedName,
            Description = request.Description?.Trim(),
            IsActive = true
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, id = category.Id, name = category.Name, alreadyExisted = false });
    }

    public class QuickCreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories.FindAsync(new object[] { id }, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        var model = new EditCategoryViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCategoryViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = await _dbContext.Categories.FindAsync(new object[] { model.Id }, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        category.Name = model.Name.Trim();
        category.Description = model.Description?.Trim();
        category.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["CategoryUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            return NotFound();
        }

        // Safe delete: do not allow deletion if category has products
        if (category.Products.Any())
        {
            TempData.SetError(_loc["OperationFailed"], _loc["CannotDeleteCategoryWithProductsNotice"]);
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["CategoryDeletedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }
}
