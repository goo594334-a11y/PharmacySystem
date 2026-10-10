using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Models.Common;
using PharmacySystem.Models.Entities;
using PharmacySystem.Resources;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequireAdmin)]
public class AuditLogsController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IStringLocalizer<SharedResource> _loc;

    public AuditLogsController(PharmacyDbContext dbContext, IStringLocalizer<SharedResource> loc)
    {
        _dbContext = dbContext;
        _loc = loc;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? actionFilter,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(a => a.Action.Contains(trimmed) || a.EntityName.Contains(trimmed) || a.EntityId.Contains(trimmed) || (a.User != null && (a.User.Email.Contains(trimmed) || a.User.FullName.Contains(trimmed))));
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var projected = query.OrderByDescending(a => a.Timestamp);

        var paginated = await PaginatedList<AuditLog>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Search = search;
        ViewBag.ActionFilter = actionFilter;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.ActionsList = await _dbContext.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().ToListAsync(cancellationToken);

        return View(paginated);
    }
}
