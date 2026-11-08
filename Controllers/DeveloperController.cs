using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Entities;
using PharmacySystem.Resources;
using PharmacySystem.Services.Auth;
using PharmacySystem.Services.Email;

namespace PharmacySystem.Controllers;

public class PharmacyApprovalItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public PharmacyStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AdminFullName { get; set; }
    public string? AdminEmail { get; set; }
    public bool AdminIsActive { get; set; }
    public int UsersCount { get; set; }
    public int ProductsCount { get; set; }
    public int SalesCount { get; set; }
}

[Authorize(Policy = AppPolicies.RequireDeveloper)]
public class DeveloperController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _loc;
    private readonly ILogger<DeveloperController> _logger;

    public DeveloperController(
        PharmacyDbContext dbContext,
        IEmailService emailService,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResource> loc,
        ILogger<DeveloperController> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _currentUser = currentUser;
        _loc = loc;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? search, CancellationToken cancellationToken)
    {
        // Bypass global query filters so developer sees all pharmacies
        var query = _dbContext.Pharmacies
            .IgnoreQueryFilters()
            .Include(p => p.Users)
            .Include(p => p.Products)
            .Include(p => p.Sales)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(p => p.Name.Contains(trimmed) || 
                                     (p.Phone != null && p.Phone.Contains(trimmed)) ||
                                     p.Users.Any(u => u.Email.Contains(trimmed) || u.FullName.Contains(trimmed)));
        }

        var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);

        var viewModels = list.Select(p =>
        {
            var initialAdmin = p.Users.FirstOrDefault(u => u.Role == UserRole.Admin) ?? p.Users.FirstOrDefault();
            return new PharmacyApprovalItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Phone = p.Phone,
                Address = p.Address,
                IsActive = p.IsActive,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                AdminFullName = initialAdmin?.FullName,
                AdminEmail = initialAdmin?.Email,
                AdminIsActive = initialAdmin?.IsActive ?? false,
                UsersCount = p.Users.Count,
                ProductsCount = p.Products.Count,
                SalesCount = p.Sales.Count
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<PharmacyStatus>(status, true, out var parsedStatus))
            {
                viewModels = viewModels.Where(vm => vm.Status == parsedStatus).ToList();
            }
        }

        ViewBag.CurrentStatus = status;
        ViewBag.Search = search;
        ViewBag.PendingCount = list.Count(p => p.Status == PharmacyStatus.PendingApproval);
        ViewBag.ActiveCount = list.Count(p => p.Status == PharmacyStatus.Active);
        ViewBag.SuspendedCount = list.Count(p => p.Status == PharmacyStatus.Suspended);
        ViewBag.RejectedCount = list.Count(p => p.Status == PharmacyStatus.Rejected);

        return View(viewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var pharmacy = await _dbContext.Pharmacies
            .IgnoreQueryFilters()
            .Include(p => p.Users)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (pharmacy == null)
        {
            return NotFound();
        }

        pharmacy.IsActive = true;
        pharmacy.Code = $"PHARM-{pharmacy.Id:D4}";

        // Activate the initial admin user
        var initialAdmin = pharmacy.Users.FirstOrDefault(u => u.Role == UserRole.Admin) ?? pharmacy.Users.FirstOrDefault();
        if (initialAdmin != null)
        {
            initialAdmin.IsActive = true;
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            PharmacyId = pharmacy.Id,
            Action = "PharmacyApproved",
            EntityName = nameof(Pharmacy),
            EntityId = pharmacy.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Attempt notification
        if (initialAdmin != null && !string.IsNullOrWhiteSpace(initialAdmin.Email))
        {
            try
            {
                await _emailService.SendEmailAsync(
                    initialAdmin.Email,
                    "Pharmacy Account Approved",
                    $"Your registration request for '{pharmacy.Name}' has been approved by the platform administrator. You may now log in to the system.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Email delivery failed for approved pharmacy: {Message}", ex.Message);
            }
        }

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["PharmacyApprovedSuccessfully"]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason, CancellationToken cancellationToken)
    {
        var pharmacy = await _dbContext.Pharmacies
            .IgnoreQueryFilters()
            .Include(p => p.Users)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (pharmacy == null)
        {
            return NotFound();
        }

        pharmacy.IsActive = false;
        pharmacy.Code = "STATUS:REJECTED";

        foreach (var u in pharmacy.Users)
        {
            u.IsActive = false;
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            PharmacyId = pharmacy.Id,
            Action = "PharmacyRejected",
            EntityName = nameof(Pharmacy),
            EntityId = pharmacy.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["PharmacyRejectedSuccessfully"]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string? reason, CancellationToken cancellationToken)
    {
        var pharmacy = await _dbContext.Pharmacies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (pharmacy == null)
        {
            return NotFound();
        }

        pharmacy.IsActive = false;
        pharmacy.Code = "STATUS:SUSPENDED";

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            PharmacyId = pharmacy.Id,
            Action = "PharmacySuspended",
            EntityName = nameof(Pharmacy),
            EntityId = pharmacy.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["PharmacySuspendedSuccessfully"]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var pharmacy = await _dbContext.Pharmacies
            .IgnoreQueryFilters()
            .Include(p => p.Users)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (pharmacy == null)
        {
            return NotFound();
        }

        pharmacy.IsActive = true;
        pharmacy.Code = $"PHARM-{pharmacy.Id:D4}";

        var initialAdmin = pharmacy.Users.FirstOrDefault(u => u.Role == UserRole.Admin) ?? pharmacy.Users.FirstOrDefault();
        if (initialAdmin != null)
        {
            initialAdmin.IsActive = true;
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            PharmacyId = pharmacy.Id,
            Action = "PharmacyActivated",
            EntityName = nameof(Pharmacy),
            EntityId = pharmacy.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["PharmacyActivatedSuccessfully"]);
        return RedirectToAction(nameof(Index));
    }
}
