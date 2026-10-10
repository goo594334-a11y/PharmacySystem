using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public record ProductStockSummary(
    int ProductId,
    string ProductName,
    int TotalQuantityOnHand,
    int AvailableNonExpiredQuantity,
    int MinimumStockLevel,
    bool IsLowStock)
{
    public int AvailableQuantity => AvailableNonExpiredQuantity;
}

public interface IInventoryService
{
    /// <summary>
    /// Gets the current stock summary for a specific product derived from its active non-expired batches.
    /// </summary>
    Task<ProductStockSummary?> GetProductStockSummaryAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all products where available non-expired quantity is at or below the minimum stock level.
    /// </summary>
    Task<List<ProductStockSummary>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets batches nearing expiry within the specified days threshold (e.g. 90 days), excluding already expired batches.
    /// </summary>
    Task<List<Batch>> GetNearExpiryBatchesAsync(int daysThreshold = 90, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all currently expired batches that still have quantity on hand.
    /// </summary>
    Task<List<Batch>> GetExpiredBatchesWithStockAsync(CancellationToken cancellationToken = default);
}
