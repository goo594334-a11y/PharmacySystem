using System.Text.Json;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.Packaging;

namespace PharmacySystem.Services.Packaging;

public class ProductPackagingService : IProductPackagingService
{
    private const string TagPrefix = "[PACKAGING:";
    private const string TagSuffix = "]";

    public ProductPackagingConfig GetPackaging(Product product)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));

        if (!string.IsNullOrWhiteSpace(product.Description))
        {
            var startIndex = product.Description.IndexOf(TagPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                var endIndex = product.Description.LastIndexOf(TagSuffix, StringComparison.OrdinalIgnoreCase);
                if (endIndex > startIndex)
                {
                    var jsonStart = startIndex + TagPrefix.Length;
                    var jsonLength = endIndex - jsonStart;
                    if (jsonLength > 0)
                    {
                        var json = product.Description.Substring(jsonStart, jsonLength);
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<ProductPackagingConfig>(json);
                            if (parsed != null && parsed.PermittedUnits.Count > 0)
                            {
                                return parsed;
                            }
                        }
                        catch
                        {
                            // In case of any parsing exception, fallback to default unit
                        }
                    }
                }
            }
        }

        // Default: Product has 1 unit ("علبة / Box") with factor 1 and product's main SellingPrice
        return new ProductPackagingConfig
        {
            BaseUnitName = "علبة / Box",
            PermittedUnits = new List<ProductUnitDto>
            {
                new ProductUnitDto
                {
                    UnitName = "علبة / Box",
                    ConversionFactor = 1,
                    SellingPrice = product.SellingPrice,
                    Barcode = product.Barcode,
                    IsBaseUnit = true,
                    AllowPartialSale = true
                }
            }
        };
    }

    public void SetPackaging(Product product, ProductPackagingConfig config)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        if (config == null) throw new ArgumentNullException(nameof(config));

        // Ensure at least one unit exists
        if (config.PermittedUnits.Count == 0)
        {
            config.PermittedUnits.Add(new ProductUnitDto
            {
                UnitName = config.BaseUnitName,
                ConversionFactor = 1,
                SellingPrice = product.SellingPrice,
                Barcode = product.Barcode,
                IsBaseUnit = true,
                AllowPartialSale = true
            });
        }

        // Clean user description text (strip old tag)
        var userDesc = StripPackagingTag(product.Description);

        var json = JsonSerializer.Serialize(config);
        var combined = string.IsNullOrWhiteSpace(userDesc)
            ? $"{TagPrefix}{json}{TagSuffix}"
            : $"{userDesc.Trim()}\n{TagPrefix}{json}{TagSuffix}";

        // Check if combined length exceeds 1000 chars (nvarchar(1000))
        if (combined.Length > 1000)
        {
            // Truncate userDesc to fit
            var allowedDescLen = Math.Max(0, 1000 - (TagPrefix.Length + json.Length + TagSuffix.Length + 2));
            userDesc = userDesc.Length > allowedDescLen ? userDesc.Substring(0, allowedDescLen) : userDesc;
            combined = string.IsNullOrWhiteSpace(userDesc)
                ? $"{TagPrefix}{json}{TagSuffix}"
                : $"{userDesc.Trim()}\n{TagPrefix}{json}{TagSuffix}";
        }

        product.Description = combined;

        // Sync default base unit price with product.SellingPrice
        var baseUnit = config.PermittedUnits.FirstOrDefault(u => u.IsBaseUnit) ?? config.PermittedUnits.First();
        if (baseUnit.SellingPrice > 0)
        {
            product.SellingPrice = baseUnit.SellingPrice;
        }
    }

    public List<ProductUnitDto> GetPermittedUnits(Product product)
    {
        var config = GetPackaging(product);
        if (config.SellFullPackageOnly)
        {
            // If configured for Full Package Only, return only the top-level full package unit
            var fullUnit = config.PermittedUnits
                .Where(u => u.AllowSale)
                .OrderByDescending(u => u.ConversionFactor)
                .FirstOrDefault() 
                ?? config.PermittedUnits.OrderByDescending(u => u.ConversionFactor).FirstOrDefault();

            return fullUnit != null ? new List<ProductUnitDto> { fullUnit } : new List<ProductUnitDto>();
        }

        return config.PermittedUnits.Where(u => u.AllowSale).ToList();
    }

    public ProductUnitDto GetUnitOrDefault(Product product, string? unitName)
    {
        var config = GetPackaging(product);
        if (!string.IsNullOrWhiteSpace(unitName))
        {
            var matched = config.PermittedUnits.FirstOrDefault(u =>
                u.UnitName.Equals(unitName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                return matched;
            }
        }

        return config.PermittedUnits.FirstOrDefault(u => u.IsBaseUnit)
            ?? config.PermittedUnits.FirstOrDefault()
            ?? new ProductUnitDto
            {
                UnitName = "علبة / Box",
                ConversionFactor = 1,
                SellingPrice = product.SellingPrice,
                IsBaseUnit = true
            };
    }

    public ProductUnitDto? FindUnitByBarcode(Product product, string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        var trimmed = barcode.Trim();

        var config = GetPackaging(product);
        return config.PermittedUnits.FirstOrDefault(u =>
            !string.IsNullOrWhiteSpace(u.Barcode) && u.Barcode.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public (int EquivalentBaseQuantity, decimal UnitPrice, decimal LineTotal) CalculateAllocation(
        Product product, string? unitName, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        var unit = GetUnitOrDefault(product, unitName);

        // Equivalent quantity to allocate from batch inventory
        var equivalentBaseQuantity = quantity * unit.ConversionFactor;
        var lineTotal = quantity * unit.SellingPrice;

        return (equivalentBaseQuantity, unit.SellingPrice, lineTotal);
    }

    public (int EquivalentBaseQuantity, decimal UnitPrice, int ConversionFactor) CalculateAllocation(
        ProductPackagingConfig config, string? unitName, int quantity, decimal fallbackPrice = 0m)
    {
        if (config == null || config.PermittedUnits.Count == 0)
        {
            return (quantity, fallbackPrice, 1);
        }

        var selected = config.PermittedUnits.FirstOrDefault(u => string.Equals(u.UnitName, unitName, StringComparison.OrdinalIgnoreCase))
            ?? config.PermittedUnits.FirstOrDefault(u => u.IsBaseUnit)
            ?? config.PermittedUnits.First();

        var factor = selected.ConversionFactor > 0 ? selected.ConversionFactor : 1;
        var unitPrice = selected.SellingPrice > 0 ? selected.SellingPrice : fallbackPrice;
        var equivalentBase = quantity * factor;

        return (equivalentBase, unitPrice, factor);
    }

    public string FormatAvailableStock(Product product, int totalBaseQuantity)
    {
        if (totalBaseQuantity <= 0) return "0";

        var config = GetPackaging(product);
        var baseUnit = config.PermittedUnits.FirstOrDefault(u => u.IsBaseUnit);
        var largerUnits = config.PermittedUnits
            .Where(u => u.ConversionFactor > 1)
            .OrderByDescending(u => u.ConversionFactor)
            .ToList();

        if (largerUnits.Count == 0)
        {
            return $"{totalBaseQuantity} {(baseUnit?.UnitName ?? "وحدة")}";
        }

        var parts = new List<string>();
        var remaining = totalBaseQuantity;

        foreach (var unit in largerUnits)
        {
            var count = remaining / unit.ConversionFactor;
            if (count > 0)
            {
                parts.Add($"{count} {unit.UnitName}");
                remaining %= unit.ConversionFactor;
            }
        }

        if (remaining > 0 || parts.Count == 0)
        {
            parts.Add($"{remaining} {(baseUnit?.UnitName ?? "وحدة")}");
        }

        return string.Join(" و ", parts);
    }

    public ProductPackagingConfig BuildPackagingConfig(
        string baseUnitName, 
        decimal basePrice, 
        string? baseBarcode, 
        List<ProductUnitDto> units,
        string? dosageForm = null,
        bool sellFullPackageOnly = false)
    {
        var config = new ProductPackagingConfig
        {
            BaseUnitName = string.IsNullOrWhiteSpace(baseUnitName) ? "علبة / Box" : baseUnitName.Trim(),
            DosageForm = string.IsNullOrWhiteSpace(dosageForm) ? null : dosageForm.Trim(),
            SellFullPackageOnly = sellFullPackageOnly,
            PermittedUnits = new List<ProductUnitDto>()
        };

        var baseFromUnits = units?.FirstOrDefault(u => u.IsBaseUnit || string.Equals(u.UnitName, config.BaseUnitName, StringComparison.OrdinalIgnoreCase));

        config.PermittedUnits.Add(new ProductUnitDto
        {
            UnitName = config.BaseUnitName,
            ConversionFactor = 1,
            SellingPrice = (baseFromUnits != null && baseFromUnits.SellingPrice > 0) ? baseFromUnits.SellingPrice : basePrice,
            Barcode = (baseFromUnits != null && !string.IsNullOrWhiteSpace(baseFromUnits.Barcode)) ? baseFromUnits.Barcode : baseBarcode,
            IsBaseUnit = true,
            AllowPartialSale = !sellFullPackageOnly,
            AllowPurchase = baseFromUnits != null ? baseFromUnits.AllowPurchase : true,
            AllowSale = baseFromUnits != null ? (!sellFullPackageOnly && baseFromUnits.AllowSale) : (!sellFullPackageOnly || (units == null || units.Count == 0))
        });

        if (units != null)
        {
            foreach (var u in units.Where(u => !u.IsBaseUnit && !string.Equals(u.UnitName, config.BaseUnitName, StringComparison.OrdinalIgnoreCase)))
            {
                config.PermittedUnits.Add(new ProductUnitDto
                {
                    UnitName = u.UnitName.Trim(),
                    ConversionFactor = u.ConversionFactor > 0 ? u.ConversionFactor : 1,
                    SellingPrice = u.SellingPrice > 0 ? u.SellingPrice : basePrice,
                    Barcode = string.IsNullOrWhiteSpace(u.Barcode) ? null : u.Barcode.Trim(),
                    IsBaseUnit = false,
                    AllowPartialSale = sellFullPackageOnly ? false : u.AllowPartialSale,
                    AllowPurchase = u.AllowPurchase,
                    AllowSale = sellFullPackageOnly ? false : u.AllowSale,
                    ContainsQuantity = u.ContainsQuantity,
                    ContainsUnitName = u.ContainsUnitName
                });
            }
        }

        if (sellFullPackageOnly && config.PermittedUnits.Count > 0)
        {
            // The top-level package unit (highest conversion factor) is the only one permitted for sale
            var topUnit = config.PermittedUnits.OrderByDescending(u => u.ConversionFactor).First();
            topUnit.AllowSale = true;
        }

        return config;
    }

    public string EmbedPackaging(string? description, ProductPackagingConfig config)
    {
        var clean = StripPackagingTag(description);
        var json = JsonSerializer.Serialize(config);
        var combined = string.IsNullOrWhiteSpace(clean)
            ? $"{TagPrefix}{json}{TagSuffix}"
            : $"{clean.Trim()}\n{TagPrefix}{json}{TagSuffix}";

        if (combined.Length > 1000)
        {
            var allowed = Math.Max(0, 1000 - (TagPrefix.Length + json.Length + TagSuffix.Length + 2));
            clean = clean.Length > allowed ? clean.Substring(0, allowed) : clean;
            combined = string.IsNullOrWhiteSpace(clean)
                ? $"{TagPrefix}{json}{TagSuffix}"
                : $"{clean.Trim()}\n{TagPrefix}{json}{TagSuffix}";
        }

        return combined;
    }

    public ProductPackagingConfig GetPackagingConfig(string? description, decimal fallbackPrice, string? fallbackBarcode)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            var startIndex = description.IndexOf(TagPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                var endIndex = description.LastIndexOf(TagSuffix, StringComparison.OrdinalIgnoreCase);
                if (endIndex > startIndex)
                {
                    var jsonStart = startIndex + TagPrefix.Length;
                    var jsonLength = endIndex - jsonStart;
                    if (jsonLength > 0)
                    {
                        var json = description.Substring(jsonStart, jsonLength);
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<ProductPackagingConfig>(json);
                            if (parsed != null && parsed.PermittedUnits.Count > 0)
                            {
                                return parsed;
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        return BuildPackagingConfig("علبة / Box", fallbackPrice, fallbackBarcode, new List<ProductUnitDto>());
    }

    public string? ExtractCleanDescription(string? description)
    {
        return StripPackagingTag(description);
    }

    public string FormatStockBreakdown(int totalBaseQuantity, ProductPackagingConfig config)
    {
        if (totalBaseQuantity <= 0) return "0";
        if (config == null || config.PermittedUnits.Count <= 1) return $"{totalBaseQuantity} {config?.BaseUnitName ?? "وحدة"}";

        var sorted = config.PermittedUnits.OrderByDescending(u => u.ConversionFactor).ToList();
        var parts = new List<string>();
        int remainder = totalBaseQuantity;

        foreach (var unit in sorted)
        {
            if (unit.ConversionFactor <= 0) continue;
            int count = remainder / unit.ConversionFactor;
            if (count > 0)
            {
                parts.Add($"{count} {unit.UnitName}");
                remainder %= unit.ConversionFactor;
            }
        }

        if (remainder > 0)
        {
            parts.Add($"{remainder} {config.BaseUnitName}");
        }

        return parts.Count > 0 ? string.Join(" و ", parts) : "0";
    }

    private static string StripPackagingTag(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        var start = description.IndexOf(TagPrefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return description;

        var end = description.LastIndexOf(TagSuffix, StringComparison.OrdinalIgnoreCase);
        if (end > start)
        {
            var before = description.Substring(0, start);
            var after = description.Substring(end + TagSuffix.Length);
            return (before + after).Trim();
        }

        return description;
    }
}
