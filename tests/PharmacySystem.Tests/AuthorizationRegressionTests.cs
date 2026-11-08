using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PharmacySystem.Authorization;
using PharmacySystem.Enums;
using PharmacySystem.Models.Entities;
using PharmacySystem.Services.Auth;
using Xunit;

namespace PharmacySystem.Tests;

public class AuthorizationRegressionTests
{
    [Fact]
    public async Task ExistingCookieWithDeveloperRole_IsTransformedToIncludeAdminRole()
    {
        // Arrange: Existing browser cookie with legacy Developer claim
        var transformation = new PharmacyRoleClaimsTransformation();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "6"),
            new(ClaimTypes.Email, "goo594334@gmail.com"),
            new(ClaimTypes.Role, AppRoles.Developer)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var transformed = await transformation.TransformAsync(principal);

        // Assert: Transformed principal must now possess Admin role
        Assert.True(transformed.IsInRole(AppRoles.Admin));
        Assert.True(transformed.IsInRole(AppRoles.Developer));
        Assert.False(transformed.IsInRole(AppRoles.Pharmacist));
    }

    [Fact]
    public async Task PharmacistPrincipal_IsNotGrantedAdminRoleByTransformation()
    {
        // Arrange: Standard Pharmacist principal
        var transformation = new PharmacyRoleClaimsTransformation();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "15"),
            new(ClaimTypes.Email, "staff@pharmacy.local"),
            new(ClaimTypes.Role, AppRoles.Pharmacist)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var transformed = await transformation.TransformAsync(principal);

        // Assert: Must strictly remain Pharmacist and NOT receive Admin
        Assert.True(transformed.IsInRole(AppRoles.Pharmacist));
        Assert.False(transformed.IsInRole(AppRoles.Admin));
        Assert.False(transformed.IsInRole(AppRoles.Developer));
    }

    [Fact]
    public async Task UnauthenticatedPrincipal_IsIgnoredByTransformation()
    {
        // Arrange
        var transformation = new PharmacyRoleClaimsTransformation();
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // IsAuthenticated = false

        // Act
        var transformed = await transformation.TransformAsync(principal);

        // Assert
        Assert.False(transformed.Identity?.IsAuthenticated);
        Assert.False(transformed.IsInRole(AppRoles.Admin));
    }

    [Fact]
    public async Task AuthorizationPolicies_AdminRole_SatisfiesAllPharmacyPolicies()
    {
        // Arrange: Configure authorization service matching Program.cs exactly
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.RequireAdmin, policy =>
                policy.RequireRole(AppRoles.Admin));

            options.AddPolicy(AppPolicies.RequirePharmacistOrAdmin, policy =>
                policy.RequireRole(AppRoles.Admin, AppRoles.Pharmacist));
        });
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        // Admin User (such as Yousef Ali after transformation, or System Administrator)
        var adminClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "6"),
            new(ClaimTypes.Role, AppRoles.Admin)
        };
        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(adminClaims, "Cookie"));

        // Act & Assert
        var adminAccessResult = await authService.AuthorizeAsync(adminPrincipal, AppPolicies.RequireAdmin);
        var pharmacistOrAdminAccessResult = await authService.AuthorizeAsync(adminPrincipal, AppPolicies.RequirePharmacistOrAdmin);

        Assert.True(adminAccessResult.Succeeded, "Admin must be authorized for RequireAdmin policy.");
        Assert.True(pharmacistOrAdminAccessResult.Succeeded, "Admin must be authorized for RequirePharmacistOrAdmin policy.");
    }

    [Fact]
    public async Task AuthorizationPolicies_PharmacistRole_DeniedAccessToAdminOnlyPolicy()
    {
        // Arrange: Configure authorization service matching Program.cs exactly
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.RequireAdmin, policy =>
                policy.RequireRole(AppRoles.Admin));

            options.AddPolicy(AppPolicies.RequirePharmacistOrAdmin, policy =>
                policy.RequireRole(AppRoles.Admin, AppRoles.Pharmacist));
        });
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        // Pharmacist User
        var pharmacistClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "10"),
            new(ClaimTypes.Role, AppRoles.Pharmacist)
        };
        var pharmacistPrincipal = new ClaimsPrincipal(new ClaimsIdentity(pharmacistClaims, "Cookie"));

        // Act & Assert
        var adminAccessResult = await authService.AuthorizeAsync(pharmacistPrincipal, AppPolicies.RequireAdmin);
        var posAndOperationsResult = await authService.AuthorizeAsync(pharmacistPrincipal, AppPolicies.RequirePharmacistOrAdmin);

        Assert.False(adminAccessResult.Succeeded, "Pharmacist MUST be denied access to RequireAdmin policy (Users, AuditLogs).");
        Assert.True(posAndOperationsResult.Succeeded, "Pharmacist MUST be permitted access to POS, Products, and standard operations.");
    }

    [Fact]
    public async Task TransformedDeveloperPrincipal_IsAllowedAcrossAllPharmacyEndpoints()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.RequireAdmin, policy =>
                policy.RequireRole(AppRoles.Admin));

            options.AddPolicy(AppPolicies.RequirePharmacistOrAdmin, policy =>
                policy.RequireRole(AppRoles.Admin, AppRoles.Pharmacist));
        });
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        // Existing cookie of Yousef Ali
        var existingClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "6"),
            new(ClaimTypes.Role, AppRoles.Developer)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(existingClaims, "Cookie"));

        // Run claims transformation
        var transformation = new PharmacyRoleClaimsTransformation();
        var transformedPrincipal = await transformation.TransformAsync(principal);

        // Act
        var adminResult = await authService.AuthorizeAsync(transformedPrincipal, AppPolicies.RequireAdmin);
        var posResult = await authService.AuthorizeAsync(transformedPrincipal, AppPolicies.RequirePharmacistOrAdmin);

        // Assert: 403 Forbidden is eliminated
        Assert.True(adminResult.Succeeded, "Transformed developer user must be allowed on Admin pages without 403.");
        Assert.True(posResult.Succeeded, "Transformed developer user must be allowed on POS and operational pages without 403.");
    }
}
