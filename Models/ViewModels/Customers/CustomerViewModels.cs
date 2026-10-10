using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Customers;

public class CustomerListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int SalesCount { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCustomerViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "CustomerName")]
    public string Name { get; set; } = string.Empty;

    [Phone(ErrorMessage = "InvalidPhone")]
    [StringLength(30, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [StringLength(200, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}

public class EditCustomerViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "CustomerName")]
    public string Name { get; set; } = string.Empty;

    [Phone(ErrorMessage = "InvalidPhone")]
    [StringLength(30, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [StringLength(200, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; }
}

public class CustomerDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int TotalSalesCount { get; set; }
    public decimal TotalPurchasedAmount { get; set; }
    public List<CustomerSaleItemViewModel> RecentSales { get; set; } = new();
}

public class CustomerSaleItemViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}
