using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services.Auth;

public record AuthResult(bool Succeeded, string? ErrorMessage = null, User? User = null);

public interface IAuthService
{
    Task<AuthResult> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);
    Task SignInAsync(HttpContext httpContext, User user, bool isPersistent);
    Task SignOutAsync(HttpContext httpContext);
}

public class AuthService : IAuthService
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        PharmacyDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ILogger<AuthService>? logger = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger ?? NullLogger<AuthService>.Instance;
    }

    public async Task<AuthResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "InvalidCredentials");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // IgnoreQueryFilters: authentication lookup must bypass the tenant query filter
        // because the user is not authenticated yet when ValidateCredentials is called —
        // the filter would return zero rows for every user making login impossible.
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.Pharmacy)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user == null)
        {
            return new AuthResult(false, "InvalidCredentials");
        }

        if (user.Role != UserRole.Developer && user.Pharmacy != null)
        {
            if (user.Pharmacy.IsPendingApproval)
            {
                return new AuthResult(false, "PharmacyPendingApproval");
            }
            if (user.Pharmacy.IsRejected)
            {
                return new AuthResult(false, "PharmacyRegistrationRejected");
            }
            if (user.Pharmacy.IsSuspended || !user.Pharmacy.IsActive)
            {
                return new AuthResult(false, "PharmacySuspended");
            }
        }

        if (user.Role != UserRole.Developer && !user.IsActive)
        {
            return new AuthResult(false, "AccountDeactivated");
        }

        // Check for active lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            return new AuthResult(false, "AccountLockedOut");
        }

        var isValidPassword = _passwordHasher.VerifyPassword(user, user.PasswordHash, password);
        if (!isValidPassword)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                return new AuthResult(false, "AccountLockedOut");
            }

            return new AuthResult(false, "InvalidCredentials");
        }

        // Reset failed count and lockout on successful login
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        // Record Audit log for login
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            PharmacyId = user.PharmacyId ?? 1,
            Action = "Login",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResult(true, null, user);
    }

    public async Task SignInAsync(HttpContext httpContext, User user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("DisplayName", user.FullName),
            new("MustChangePassword", user.MustChangePassword.ToString())
        };

        if (user.PharmacyId.HasValue)
        {
            claims.Add(new Claim("PharmacyId", user.PharmacyId.Value.ToString()));
        }

        if (user.Pharmacy != null && !string.IsNullOrEmpty(user.Pharmacy.Name))
        {
            claims.Add(new Claim("PharmacyName", user.Pharmacy.Name));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = isPersistent
                ? DateTimeOffset.UtcNow.AddDays(14)
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        try
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var pharmacyIdClaim = httpContext.User.FindFirst("PharmacyId")?.Value;
            int? pharmacyId = int.TryParse(pharmacyIdClaim, out var pId) ? pId : null;

            if (int.TryParse(userIdClaim, out var userId) && userId > 0)
            {
                // Verify whether the user actually exists in the database to prevent foreign key violation
                var userExists = await _dbContext.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Id == userId);

                if (userExists)
                {
                    _dbContext.AuditLogs.Add(new AuditLog
                    {
                        UserId = userId,
                        PharmacyId = pharmacyId ?? 1,
                        Action = "Logout",
                        EntityName = nameof(User),
                        EntityId = userId.ToString(),
                        Timestamp = DateTime.UtcNow
                    });
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    // The identity belongs to a deleted user: do NOT insert an invalid foreign key or substitute a fake user!
                    _logger.LogWarning("SignOut requested for non-existent or deleted UserId {UserId}. Recording diagnostic event with null actor.", userId);

                    // UserId in AuditLogs is nullable: record a safe diagnostic audit event without invalid foreign key
                    _dbContext.AuditLogs.Add(new AuditLog
                    {
                        UserId = null,
                        PharmacyId = pharmacyId ?? 1,
                        Action = "LogoutStaleIdentity",
                        EntityName = nameof(User),
                        EntityId = userId.ToString(),
                        Timestamp = DateTime.UtcNow
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            // Non-critical audit logging failure must NEVER abort the user's sign-out!
            _logger.LogError(ex, "Non-critical error occurred while recording Logout audit log. Proceeding with sign-out.");
        }
        finally
        {
            // Guarantee that authentication cookie and session state are invalidated reliably
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
