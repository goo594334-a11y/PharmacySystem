using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public class InventoryService : IInventoryService
{
    private readonly PharmacyDbContext _dbContext;

    public InventoryService(PharmacyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductStockSummary?> GetProductStockSummaryAsync(int productId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Batches)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
            return null;

        var totalQty = product.Batches.Sum(b => b.QuantityOnHand);
        var nonExpiredQty = product.Batches
            .Where(b => b.ExpiryDate >= today)
            .Sum(b => b.QuantityOnHand);

        return new ProductStockSummary(
            product.Id,
            product.Name,
            totalQty,
            nonExpiredQty,
            product.MinimumStockLevel,
            product.IsLowStock(nonExpiredQty)
        );
    }

    public async Task<List<ProductStockSummary>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Batches)
            .ToListAsync(cancellationToken);

        return products
            .Select(p =>
            {
                var nonExpiredQty = p.Batches
                    .Where(b => b.ExpiryDate >= today)
                    .Sum(b => b.QuantityOnHand);
                var totalQty = p.Batches.Sum(b => b.QuantityOnHand);

                return new ProductStockSummary(
                    p.Id,
                    p.Name,
                    totalQty,
                    nonExpiredQty,
                    p.MinimumStockLevel,
                    p.IsLowStock(nonExpiredQty)
                );
            })
            .Where(summary => summary.IsLowStock)
            .ToList();
    }

    public async Task<List<Batch>> GetNearExpiryBatchesAsync(int daysThreshold = 90, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var thresholdDate = today.AddDays(daysThreshold);

        return await _dbContext.Batches
            .AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Location)
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate >= today && b.ExpiryDate <= thresholdDate)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Batch>> GetExpiredBatchesWithStockAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return await _dbContext.Batches
            .AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Location)
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate < today)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);
    }
}
