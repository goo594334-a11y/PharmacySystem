using System.Security.Claims;
using PharmacySystem.Authorization;
using PharmacySystem.Enums;

namespace PharmacySystem.Services.Auth;

public interface ICurrentUserService
{
    int? UserId { get; }
    int? PharmacyId { get; }
    string? PharmacyName { get; }
    string? Email { get; }
    string? DisplayName { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    bool IsDeveloper { get; }
    bool IsAdmin { get; }
    bool IsPharmacist { get; }
    bool MustChangePassword { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idClaim, out var id))
            {
                return id;
            }
            return null;
        }
    }

    public int? PharmacyId
    {
        get
        {
            var idClaim = User?.FindFirst("PharmacyId")?.Value;
            if (int.TryParse(idClaim, out var id))
            {
                return id;
            }
            return null;
        }
    }

    public string? PharmacyName => User?.FindFirst("PharmacyName")?.Value;

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public string? DisplayName => User?.FindFirst("DisplayName")?.Value ?? Email;

    public UserRole? Role
    {
        get
        {
            var roleClaim = User?.FindFirst(ClaimTypes.Role)?.Value;
            if (Enum.TryParse<UserRole>(roleClaim, out var role))
            {
                return role;
            }
            return null;
        }
    }

    public bool IsDeveloper => (User?.IsInRole(AppRoles.Developer) ?? false) || (Role == UserRole.Developer);
    public bool IsAdmin => (User?.IsInRole(AppRoles.Admin) ?? false) || IsDeveloper;
    public bool IsPharmacist => (User?.IsInRole(AppRoles.Pharmacist) ?? false) || IsDeveloper;
    public bool MustChangePassword
    {
        get
        {
            var claim = User?.FindFirst("MustChangePassword")?.Value;
            return bool.TryParse(claim, out var must) && must;
        }
    }
}
