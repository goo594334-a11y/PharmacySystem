using System.ComponentModel.DataAnnotations;
using PharmacySystem.Enums;

namespace PharmacySystem.Models.ViewModels.Sales;

public class SaleListItemViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int ItemsCount { get; set; }
    public string SoldByName { get; set; } = string.Empty;
}

public class PosItemInputViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Range(1, 10000)]
    public int Quantity { get; set; } = 1;
}

public class CreateSaleInputViewModel
{
    public int? CustomerId { get; set; }

    [Required]
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Required]
    public List<PosItemInputViewModel> Items { get; set; } = new();
}

public class SaleItemAllocationViewModel
{
    public string BatchNumber { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int Quantity { get; set; }
}

public class SaleDetailsItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
    public List<SaleItemAllocationViewModel> Allocations { get; set; } = new();
}

public class SaleDetailsViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public DateTime SaleDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public string SoldByName { get; set; } = string.Empty;
    public List<SaleDetailsItemViewModel> Items { get; set; } = new();
}
