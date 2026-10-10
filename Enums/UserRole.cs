namespace PharmacySystem.Enums;

/// <summary>
/// System roles for users.
/// Note: There is no separate Cashier role. Pharmacists also perform cashier/sales responsibilities.
/// </summary>
public enum UserRole
{
    Admin = 1,
    Pharmacist = 2
}
