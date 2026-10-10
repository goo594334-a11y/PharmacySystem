using PharmacySystem.Enums;

namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a purchase order / incoming receipt from a supplier.
/// Stock increases only when Purchase.Status transitions to Confirmed.
/// </summary>
public class Purchase
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public int UserId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

    // Navigation properties
    public Supplier Supplier { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
    public ICollection<PurchaseReturn> PurchaseReturns { get; set; } = new List<PurchaseReturn>();
}
