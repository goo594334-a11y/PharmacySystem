using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Returns;

public class SalesReturnListItemViewModel
{
    public int Id { get; set; }
    public string SaleInvoiceNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Reason { get; set; }
    public string ProcessedByName { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
}

public class SalesReturnItemInputViewModel
{
    public int SaleItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int SoldQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int QuantityToReturn { get; set; }
    public bool ReturnToStock { get; set; } = false;
}

public class CreateSalesReturnViewModel
{
    public int SaleId { get; set; }
    public string SaleInvoiceNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(300, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    public List<SalesReturnItemInputViewModel> Items { get; set; } = new();
}

public class PurchaseReturnListItemViewModel
{
    public int Id { get; set; }
    public string PurchaseInvoiceNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Reason { get; set; }
    public string ProcessedByName { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
}

public class PurchaseReturnItemInputViewModel
{
    public int PurchaseItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int PurchasedQuantity { get; set; }
    public int CurrentStock { get; set; }
    public decimal UnitPurchasePrice { get; set; }
    public int QuantityToReturn { get; set; }
}

public class CreatePurchaseReturnViewModel
{
    public int PurchaseId { get; set; }
    public string PurchaseInvoiceNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(300, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    public List<PurchaseReturnItemInputViewModel> Items { get; set; } = new();
}
