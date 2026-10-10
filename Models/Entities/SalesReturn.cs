namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a return of products from an existing sale.
/// References original Sale and restores stock into batches.
/// </summary>
public class SalesReturn
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int UserId { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Navigation properties
    public Sale Sale { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<SalesReturnItem> ReturnItems { get; set; } = new List<SalesReturnItem>();
}
