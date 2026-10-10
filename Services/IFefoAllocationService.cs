using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public record BatchAllocationPlan(Batch Batch, int AllocatedQuantity, decimal UnitCost);

public interface IFefoAllocationService
{
    /// <summary>
    /// Computes FEFO allocations for a given requested product quantity.
    /// Selects non-expired batches sorted by earliest ExpiryDate first.
    /// Throws InsufficientStockException if available non-expired quantity is less than requested.
    /// </summary>
    List<BatchAllocationPlan> CalculateAllocations(
        Product product,
        IEnumerable<Batch> availableBatches,
        int requestedQuantity,
        DateOnly? currentDate = null);
}
