using System.ComponentModel.DataAnnotations;
using PharmacySystem.Models.Packaging;
using PharmacySystem.Validation;

namespace PharmacySystem.Models.ViewModels.Products;

public class ProductListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public int MinimumStockLevel { get; set; }
    public int TotalStock { get; set; }
    public int NonExpiredStock { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
    public string? BaseUnitName { get; set; }
    public int UnitsCount { get; set; } = 1;
}

public class CreateProductViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Barcode")]
    public string? Barcode { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [PositiveDecimal(AllowZero = false, ErrorMessage = "PositiveDecimal")]
    [Display(Name = "SellingPrice")]
    public decimal SellingPrice { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [PositiveQuantity(AllowZero = true, ErrorMessage = "PositiveQuantity")]
    [Display(Name = "MinimumStockLevel")]
    public int MinimumStockLevel { get; set; } = 5;

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;

    // Flexible Packaging & Units
    [Display(Name = "DosageForm")]
    public string? DosageForm { get; set; }

    [Display(Name = "SellFullPackageOnly")]
    public bool SellFullPackageOnly { get; set; }

    [Display(Name = "BaseUnit")]
    public string BaseUnitName { get; set; } = "علبة / Box";

    public string? UnitsJson { get; set; }
}

public class EditProductViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Barcode")]
    public string? Barcode { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [PositiveDecimal(AllowZero = false, ErrorMessage = "PositiveDecimal")]
    [Display(Name = "SellingPrice")]
    public decimal SellingPrice { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [PositiveQuantity(AllowZero = true, ErrorMessage = "PositiveQuantity")]
    [Display(Name = "MinimumStockLevel")]
    public int MinimumStockLevel { get; set; }

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; }

    // Flexible Packaging & Units
    [Display(Name = "DosageForm")]
    public string? DosageForm { get; set; }

    [Display(Name = "SellFullPackageOnly")]
    public bool SellFullPackageOnly { get; set; }

    [Display(Name = "BaseUnit")]
    public string BaseUnitName { get; set; } = "علبة / Box";

    public string? UnitsJson { get; set; }
}

public class ProductBatchItemViewModel
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int QuantityOnHand { get; set; }
    public decimal PurchasePrice { get; set; }
    public bool IsExpired { get; set; }
    public int RemainingDays { get; set; }
    public int DaysRemaining { get => RemainingDays; set => RemainingDays = value; }
}

public class ProductDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public int MinimumStockLevel { get; set; }
    public int TotalStock { get; set; }
    public int NonExpiredStock { get; set; }
    public bool IsLowStock { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? DosageForm { get; set; }
    public bool SellFullPackageOnly { get; set; }
    public string BaseUnitName { get; set; } = "علبة / Box";
    public List<ProductUnitDto> PermittedUnits { get; set; } = new();
    public List<ProductBatchItemViewModel> Batches { get; set; } = new();
}

public class ProductBarcodeLookupResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SellingPrice { get; set; }
    public int AvailableStock { get; set; }
    public string? PhysicalLocation { get; set; }
    public DateOnly? NearestExpiry { get; set; }
    public string BaseUnitName { get; set; } = "علبة / Box";
    public string? DosageForm { get; set; }
    public bool SellFullPackageOnly { get; set; }
    public string? SelectedUnitName { get; set; }
    public string? StockFormatted { get; set; }
    public List<ProductUnitDto> Units { get; set; } = new();
}
