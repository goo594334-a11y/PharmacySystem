namespace PharmacySystem.Enums;

/// <summary>
/// Status of a purchase transaction.
/// Stock increases only when a purchase is confirmed.
/// </summary>
public enum PurchaseStatus
{
    Draft = 1,
    Confirmed = 2,
    Cancelled = 3
}
