using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
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

    public AuthService(PharmacyDbContext dbContext, IPasswordHasherService passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
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

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user == null)
        {
            return new AuthResult(false, "InvalidCredentials");
        }

        if (!user.IsActive)
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
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "Logout",
                EntityName = nameof(User),
                EntityId = userId.ToString(),
                Timestamp = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
