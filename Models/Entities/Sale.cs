using PharmacySystem.Enums;

namespace PharmacySystem.Models.Entities;

/// <summary>
/// Represents a sales transaction at the pharmacy counter.
/// Customer is optional (for walk-in patients/customers).
/// </summary>
public class Sale
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int UserId { get; set; }
    public DateTime SaleDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal TotalAmount { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }
    public User User { get; set; } = null!;
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<SalesReturn> SalesReturns { get; set; } = new List<SalesReturn>();
}
