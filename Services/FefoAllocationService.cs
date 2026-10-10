using PharmacySystem.Exceptions;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public class FefoAllocationService : IFefoAllocationService
{
    public List<BatchAllocationPlan> CalculateAllocations(
        Product product,
        IEnumerable<Batch> availableBatches,
        int requestedQuantity,
        DateOnly? currentDate = null)
    {
        if (requestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), "Requested quantity must be greater than zero.");
        }

        var today = currentDate ?? DateOnly.FromDateTime(DateTime.Today);

        // 1. Find valid non-expired batches with positive stock on hand
        // 2. Sort by earliest ExpiryDate (FEFO)
        var eligibleBatches = availableBatches
            .Where(b => b.ProductId == product.Id && b.QuantityOnHand > 0 && !b.IsExpired(today))
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.AddedDate)
            .ToList();

        var totalAvailable = eligibleBatches.Sum(b => b.QuantityOnHand);
        if (totalAvailable < requestedQuantity)
        {
            throw new InsufficientStockException(product.Id, requestedQuantity, totalAvailable);
        }

        var plans = new List<BatchAllocationPlan>();
        var remainingNeeded = requestedQuantity;

        foreach (var batch in eligibleBatches)
        {
            if (remainingNeeded <= 0)
                break;

            var take = Math.Min(batch.QuantityOnHand, remainingNeeded);
            plans.Add(new BatchAllocationPlan(batch, take, batch.PurchasePrice));
            remainingNeeded -= take;
        }

        return plans;
    }
}
