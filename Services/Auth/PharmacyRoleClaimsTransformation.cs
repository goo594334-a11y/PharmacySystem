using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using PharmacySystem.Authorization;

namespace PharmacySystem.Services.Auth;

/// <summary>
/// Normalizes and transforms role claims for authenticated principals on every incoming request.
/// Ensures existing cookies or retained credentials containing a Developer claim are
/// automatically mapped to the Admin role within the pharmacy management domain.
/// </summary>
public class PharmacyRoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
        {
            // If the principal has Developer claim, ensure they have the Admin role claim
            if (identity.HasClaim(ClaimTypes.Role, AppRoles.Developer) && !identity.HasClaim(ClaimTypes.Role, AppRoles.Admin))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, AppRoles.Admin));
            }
        }

        return Task.FromResult(principal);
    }
}
