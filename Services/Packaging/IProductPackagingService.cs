using PharmacySystem.Models.Entities;
using PharmacySystem.Models.Packaging;

namespace PharmacySystem.Services.Packaging;

public interface IProductPackagingService
{
    /// <summary>
    /// Retrieves the configured packaging hierarchy for a product, with safe fallbacks to defaults.
    /// </summary>
    ProductPackagingConfig GetPackaging(Product product);

    /// <summary>
    /// Updates the packaging configuration metadata on a product.
    /// </summary>
    void SetPackaging(Product product, ProductPackagingConfig config);

    /// <summary>
    /// Gets all permitted selling units for a product.
    /// </summary>
    List<ProductUnitDto> GetPermittedUnits(Product product);

    /// <summary>
    /// Finds a specific unit by name or returns the default base unit.
    /// </summary>
    ProductUnitDto GetUnitOrDefault(Product product, string? unitName);

    /// <summary>
    /// Finds a unit by unit-specific barcode if configured.
    /// </summary>
    ProductUnitDto? FindUnitByBarcode(Product product, string barcode);

    /// <summary>
    /// Calculates the equivalent base stock quantity to deduct from batches and the total line amount.
    /// </summary>
    (int EquivalentBaseQuantity, decimal UnitPrice, decimal LineTotal) CalculateAllocation(Product product, string? unitName, int quantity);

    (int EquivalentBaseQuantity, decimal UnitPrice, int ConversionFactor) CalculateAllocation(ProductPackagingConfig config, string? unitName, int quantity, decimal fallbackPrice = 0m);

    /// <summary>
    /// Formats the available stock into human-readable packaging breakdown (e.g. "10 علب و 2 شريط").
    /// </summary>
    string FormatAvailableStock(Product product, int totalBaseQuantity);

    ProductPackagingConfig BuildPackagingConfig(string baseUnitName, decimal basePrice, string? baseBarcode, List<ProductUnitDto> units, string? dosageForm = null, bool sellFullPackageOnly = false);
    string EmbedPackaging(string? description, ProductPackagingConfig config);
    ProductPackagingConfig GetPackagingConfig(string? description, decimal fallbackPrice, string? fallbackBarcode);
    string? ExtractCleanDescription(string? description);
    string FormatStockBreakdown(int totalBaseQuantity, ProductPackagingConfig config);
}
