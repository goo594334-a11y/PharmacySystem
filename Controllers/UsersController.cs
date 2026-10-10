using System.Security.Cryptography;
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
using PharmacySystem.Models.ViewModels.Users;
using PharmacySystem.Resources;
using PharmacySystem.Services.Auth;

namespace PharmacySystem.Controllers;

[Authorize(Policy = AppPolicies.RequireAdmin)]
public class UsersController : Controller
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IStringLocalizer<SharedResource> _loc;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        PharmacyDbContext dbContext,
        IPasswordHasherService passwordHasher,
        IStringLocalizer<SharedResource> loc,
        ILogger<UsersController> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _loc = loc;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        UserRole? role,
        bool? isActive,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(u => u.Email.Contains(trimmed) || u.FullName.Contains(trimmed));
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        query = query.OrderBy(u => u.FullName);

        var projected = query.Select(u => new UserListItemViewModel
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role,
            IsActive = u.IsActive,
            SalesCount = u.Sales.Count,
            PurchasesCount = u.Purchases.Count,
            CreatedAt = u.CreatedAt
        });

        var paginatedUsers = await PaginatedList<UserListItemViewModel>.CreateAsync(projected, page, pageSize, cancellationToken);

        ViewBag.Search = search;
        ViewBag.Role = role;
        ViewBag.IsActive = isActive;

        return View(paginatedUsers);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var exists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Email), _loc["EmailAlreadyExists"]);
            return View(model);
        }

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            Role = model.Role,
            IsActive = model.IsActive,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _dbContext.Users.Add(user);
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "CreateUser",
            EntityName = nameof(User),
            EntityId = normalizedEmail,
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["UserCreatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var model = new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _dbContext.Users.FindAsync(new object[] { model.Id }, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        // LAST ADMIN PROTECTION: Prevent removing/deactivating the last active Admin
        if (user.Role == UserRole.Admin && user.IsActive)
        {
            var isChangingToNonAdmin = model.Role != UserRole.Admin;
            var isDeactivating = !model.IsActive;

            if (isChangingToNonAdmin || isDeactivating)
            {
                var activeAdminCount = await _dbContext.Users
                    .CountAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, cancellationToken);

                if (activeAdminCount == 0)
                {
                    ModelState.AddModelError(string.Empty, _loc["CannotDeactivateLastAdmin"]);
                    return View(model);
                }
            }
        }

        user.FullName = model.FullName.Trim();
        user.Role = model.Role;
        user.IsActive = model.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateUser",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["UserUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        // LAST ADMIN PROTECTION
        if (user.Role == UserRole.Admin && user.IsActive)
        {
            var otherActiveAdmins = await _dbContext.Users
                .CountAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, cancellationToken);

            if (otherActiveAdmins == 0)
            {
                TempData.SetError(_loc["OperationFailed"], _loc["CannotDeactivateLastAdmin"]);
                return RedirectToAction(nameof(Index));
            }
        }

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = user.IsActive ? "ActivateUser" : "DeactivateUser",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["UserUpdatedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var tempPassword = GenerateTemporaryPassword();
        user.PasswordHash = _passwordHasher.HashPassword(user, tempPassword);
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "AdminResetUserPassword",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["TempPassword"] = tempPassword;
        TempData["CreatedUserEmail"] = user.Email;
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["PasswordResetSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Sales)
            .Include(u => u.Purchases)
            .Include(u => u.SalesReturns)
            .Include(u => u.PurchaseReturns)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            return NotFound();
        }

        // LAST ADMIN PROTECTION
        if (user.Role == UserRole.Admin && user.IsActive)
        {
            var otherActiveAdmins = await _dbContext.Users
                .CountAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, cancellationToken);

            if (otherActiveAdmins == 0)
            {
                TempData.SetError(_loc["OperationFailed"], _loc["CannotDeactivateLastAdmin"]);
                return RedirectToAction(nameof(Index));
            }
        }

        // SAFE USER DELETION: Protect transactional history
        var hasHistory = user.Sales.Any() || user.Purchases.Any() || user.SalesReturns.Any() || user.PurchaseReturns.Any();
        if (hasHistory)
        {
            TempData.SetError(_loc["OperationFailed"], _loc["CannotDeleteUserWithTransactionsNotice"]);
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Users.Remove(user);
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "DeleteUser",
            EntityName = nameof(User),
            EntityId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["UserDeletedSuccessfully"]);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Sales)
            .Include(u => u.Purchases)
            .Include(u => u.AuditLogs)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            return NotFound();
        }

        var model = new UserDetailsViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            TotalSalesCount = user.Sales.Count,
            TotalSalesAmount = user.Sales.Sum(s => s.TotalAmount),
            TotalPurchasesCount = user.Purchases.Count,
            TotalAuditLogsCount = user.AuditLogs.Count
        };

        return View(model);
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "@#$!%*";

        var password = new char[12];
        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        const string all = upper + lower + digits + special;
        for (int i = 4; i < password.Length; i++)
        {
            password[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Cryptographic shuffle
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }
}
