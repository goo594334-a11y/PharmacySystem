using PharmacySystem.Enums;

namespace PharmacySystem.Authorization;

/// <summary>
/// Authoritative system role names.
/// Note: There is NO separate Cashier role. Pharmacist includes cashier duties.
/// </summary>
public static class AppRoles
{
    public const string Developer = nameof(UserRole.Developer);
    public const string Admin = nameof(UserRole.Admin);
    public const string Pharmacist = nameof(UserRole.Pharmacist);
}
