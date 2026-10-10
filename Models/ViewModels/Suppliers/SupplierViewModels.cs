using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Suppliers;

public class SupplierListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int PurchasesCount { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSupplierViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "SupplierName")]
    public string Name { get; set; } = string.Empty;

    [Phone(ErrorMessage = "InvalidPhone")]
    [StringLength(30, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "InvalidEmail")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(200, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}

public class EditSupplierViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "SupplierName")]
    public string Name { get; set; } = string.Empty;

    [Phone(ErrorMessage = "InvalidPhone")]
    [StringLength(30, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "InvalidEmail")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(200, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; }
}

public class SupplierDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int TotalPurchasesCount { get; set; }
    public decimal TotalPurchasesAmount { get; set; }
    public List<SupplierPurchaseItemViewModel> RecentPurchases { get; set; } = new();
}

public class SupplierPurchaseItemViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}
