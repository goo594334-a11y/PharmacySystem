using System.ComponentModel.DataAnnotations;
using PharmacySystem.Enums;
using PharmacySystem.Validation;

namespace PharmacySystem.Models.ViewModels.Purchases;

public class PurchaseListItemViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public PurchaseStatus Status { get; set; }
    public int ItemsCount { get; set; }
}

public class CreatePurchaseItemViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    public int ProductId { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [PositiveQuantity(AllowZero = false, ErrorMessage = "PositiveQuantity")]
    public int Quantity { get; set; } = 1;

    [Required(ErrorMessage = "RequiredField")]
    [PositiveDecimal(AllowZero = false, ErrorMessage = "PositiveDecimal")]
    public decimal UnitPurchasePrice { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(50, ErrorMessage = "StringLengthMax")]
    public string BatchNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    public DateOnly ExpiryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(12));

    [Required(ErrorMessage = "RequiredField")]
    public int LocationId { get; set; }
}

public class CreatePurchaseViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "Supplier")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(50, ErrorMessage = "StringLengthMax")]
    [Display(Name = "InvoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<CreatePurchaseItemViewModel> Items { get; set; } = new();
}

public class PurchaseDetailsItemViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPurchasePrice { get; set; }
    public decimal LineTotal => Quantity * UnitPurchasePrice;
}

public class PurchaseDetailsViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierPhone { get; set; }
    public DateTime PurchaseDate { get; set; }
    public PurchaseStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string ReceivedByName { get; set; } = string.Empty;
    public List<PurchaseDetailsItemViewModel> Items { get; set; } = new();
}
