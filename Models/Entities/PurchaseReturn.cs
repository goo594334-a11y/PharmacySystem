namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a return of products back to a supplier from an existing purchase.
/// </summary>
public class PurchaseReturn
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int UserId { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Navigation properties
    public Purchase Purchase { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<PurchaseReturnItem> ReturnItems { get; set; } = new List<PurchaseReturnItem>();
}
