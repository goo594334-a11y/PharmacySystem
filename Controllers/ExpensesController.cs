using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.ViewModels.Expenses;
using PharmacySystem.Resources;
using PharmacySystem.Services.Auth;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequirePharmacistOrAdmin)]
public class ExpensesController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _loc;

    public ExpensesController(
        PharmacyDbContext dbContext,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        ExpenseCategory? category,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Expenses
            .AsNoTracking()
            .Include(e => e.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(e => e.Description != null && e.Description.Contains(trimmed));
        }

        if (category.HasValue)
        {
            query = query.Where(e => e.Category == category.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(e => e.ExpenseDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.ExpenseDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var projected = query.OrderByDescending(e => e.ExpenseDate).Select(e => new ExpenseListItemViewModel
        {
            Id = e.Id,
            ExpenseDate = e.ExpenseDate,
            Category = e.Category,
            Amount = e.Amount,
            Description = e.Description,
            RecordedByName = e.User.FullName
        });

        var paginated = await PaginatedList<ExpenseListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.TotalExpensesSum = await query.SumAsync(e => e.Amount, cancellationToken);

        return View(paginated);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateExpenseViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var expense = new Expense
        {
            UserId = _currentUserService.UserId.Value,
            ExpenseDate = model.ExpenseDate,
            Category = model.Category,
            Amount = model.Amount,
            Description = model.Description?.Trim()
        };

        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["ExpenseCreatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var expense = await _dbContext.Expenses.FindAsync(new object[] { id }, cancellationToken);
        if (expense == null)
        {
            return NotFound();
        }

        var model = new EditExpenseViewModel
        {
            Id = expense.Id,
            ExpenseDate = expense.ExpenseDate,
            Category = expense.Category,
            Amount = expense.Amount,
            Description = expense.Description
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var expense = await _dbContext.Expenses.FindAsync(new object[] { model.Id }, cancellationToken);
        if (expense == null)
        {
            return NotFound();
        }

        expense.ExpenseDate = model.ExpenseDate;
        expense.Category = model.Category;
        expense.Amount = model.Amount;
        expense.Description = model.Description?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["ExpenseUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var expense = await _dbContext.Expenses.FindAsync(new object[] { id }, cancellationToken);
        if (expense == null)
        {
            return NotFound();
        }

        _dbContext.Expenses.Remove(expense);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["ExpenseDeletedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }
}
