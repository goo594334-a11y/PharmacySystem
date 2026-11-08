namespace PharmacySystem.Enums;

/// <summary>
/// System roles for users.
/// Note: There is no separate Cashier role. Pharmacists also perform cashier/sales responsibilities.
/// Developer role is dedicated to platform administration, pharmacy onboarding approval, and cross-tenant management.
/// </summary>
public enum UserRole
{
    Developer = 0,
    Admin = 1,
    Pharmacist = 2
}
