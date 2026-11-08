namespace PharmacySystem.Models.Packaging;

/// <summary>
/// Represents a specific packaging or selling unit for a product (e.g. Box, Strip, Tablet, Ampoule).
/// </summary>
public class ProductUnitDto
{
    public string UnitName { get; set; } = string.Empty;

    /// <summary>
    /// Conversion factor to base stock units.
    /// Example: If base stock unit is 'قرص / Tablet', then 1 Box = 30 tablets (Factor = 30), 1 Strip = 10 tablets (Factor = 10).
    /// If base unit is 'علبة / Box', then Box Factor = 1.
    /// </summary>
    public int ConversionFactor { get; set; } = 1;

    /// <summary>
    /// Selling price per this unit (in EGP).
    /// </summary>
    public decimal SellingPrice { get; set; }

    /// <summary>
    /// Optional unit-specific barcode (e.g. barcode printed on strip vs barcode on box).
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Indicates if this is the fundamental base stock unit in which batch quantities are tracked.
    /// </summary>
    public bool IsBaseUnit { get; set; }

    /// <summary>
    /// Indicates whether partial selling (e.g. selling by strip or tablet) is permitted.
    /// </summary>
    public bool AllowPartialSale { get; set; } = true;

    /// <summary>
    /// Indicates whether purchasing is permitted in this unit (e.g. buying in boxes or cartons).
    /// </summary>
    public bool AllowPurchase { get; set; } = true;

    /// <summary>
    /// Indicates whether selling is permitted in this unit.
    /// </summary>
    public bool AllowSale { get; set; } = true;

    /// <summary>
    /// Packaging hierarchy helper: Quantity contained of the child unit (e.g. 3 strips or 10 tablets).
    /// </summary>
    public int? ContainsQuantity { get; set; }

    /// <summary>
    /// Packaging hierarchy helper: Name of the child unit contained (e.g. 'شريط / Strip' or 'قرص / Tablet').
    /// </summary>
    public string? ContainsUnitName { get; set; }
}

/// <summary>
/// Encapsulates the complete packaging hierarchy and permitted units for a product.
/// </summary>
public class ProductPackagingConfig
{
    public string BaseUnitName { get; set; } = "علبة / Box";

    /// <summary>
    /// Physical pharmaceutical form (e.g. Tablets, Syrup, Injection, Cream).
    /// </summary>
    public string? DosageForm { get; set; }

    /// <summary>
    /// When true, only the full package can be sold at POS; partial units are hidden from cashier.
    /// </summary>
    public bool SellFullPackageOnly { get; set; }

    public List<ProductUnitDto> PermittedUnits { get; set; } = new();
}
