namespace PharmacySystem.Authorization;

/// <summary>
/// Centralized authorization policy definitions for the application.
/// </summary>
public static class AppPolicies
{
    public const string RequireDeveloper = "RequireDeveloper";
    public const string RequireAdmin = "RequireAdmin";
    public const string RequirePharmacistOrAdmin = "RequirePharmacistOrAdmin";
}
