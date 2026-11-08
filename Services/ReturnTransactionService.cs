using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Exceptions;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public class ReturnTransactionService : IReturnTransactionService
{
    private readonly PharmacyDbContext _dbContext;

    public ReturnTransactionService(PharmacyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SalesReturn> ProcessSalesReturnAsync(CreateSalesReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidBusinessOperationException("At least one item must be returned.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var sale = await _dbContext.Sales
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.BatchAllocations)
                .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

            if (sale == null)
            {
                throw new InvalidBusinessOperationException($"Sale with ID {request.SaleId} does not exist.");
            }

            var user = await _dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
            if (user == null || !user.IsActive)
            {
                throw new InvalidBusinessOperationException($"Active user with ID {request.UserId} was not found.");
            }

            var salesReturn = new SalesReturn
            {
                SaleId = request.SaleId,
                UserId = request.UserId,
                ReturnDate = DateTime.UtcNow,
                Reason = request.Reason,
                TotalAmount = 0m
            };

            decimal totalRefund = 0m;

            foreach (var itemInput in request.Items)
            {
                if (itemInput.Quantity <= 0)
                {
                    throw new InvalidBusinessOperationException("Returned quantity must be greater than zero.");
                }

                var saleItem = sale.SaleItems.FirstOrDefault(si => si.Id == itemInput.SaleItemId);
                if (saleItem == null)
                {
                    throw new InvalidBusinessOperationException($"Sale item with ID {itemInput.SaleItemId} does not belong to sale {request.SaleId}.");
                }

                // Verify that return quantity does not exceed original sold quantity minus previous returns
                var alreadyReturned = await _dbContext.SalesReturnItems
                    .Where(sri => sri.SaleItemId == itemInput.SaleItemId)
                    .SumAsync(sri => (int?)sri.Quantity, cancellationToken) ?? 0;

                var maxEligible = saleItem.Quantity - alreadyReturned;
                if (itemInput.Quantity > maxEligible)
                {
                    throw new InvalidBusinessOperationException($"Cannot return {itemInput.Quantity} units. Only {maxEligible} units remain eligible for return.");
                }

                var batch = await _dbContext.Batches.FindAsync(new object[] { itemInput.BatchId }, cancellationToken);
                if (batch == null)
                {
                    throw new InvalidBusinessOperationException($"Batch with ID {itemInput.BatchId} does not exist.");
                }

                // Calculate base quantity conversion factor from original sale allocations
                var totalAllocatedBase = saleItem.BatchAllocations.Sum(a => a.Quantity);
                var conversionFactor = saleItem.Quantity > 0 && totalAllocatedBase > 0
                    ? Math.Max(1, totalAllocatedBase / saleItem.Quantity)
                    : 1;
                var equivalentBaseQtyToRestore = itemInput.Quantity * conversionFactor;

                // Rule: If item is returned to saleable stock, ensure it is non-expired and explicitly approved
                var today = DateOnly.FromDateTime(DateTime.Today);
                if (itemInput.ReturnToStock)
                {
                    if (batch.ExpiryDate < today)
                    {
                        throw new InvalidBusinessOperationException($"Cannot return expired batch '{batch.BatchNumber}' to saleable inventory.");
                    }

                    // Restore exact base stock back into batch
                    batch.QuantityOnHand += equivalentBaseQtyToRestore;
                }

                var returnItem = new SalesReturnItem
                {
                    SaleItemId = itemInput.SaleItemId,
                    BatchId = itemInput.BatchId,
                    Quantity = itemInput.Quantity
                };

                salesReturn.ReturnItems.Add(returnItem);
                totalRefund += itemInput.Quantity * saleItem.UnitPrice;
            }

            salesReturn.TotalAmount = totalRefund;
            _dbContext.SalesReturns.Add(salesReturn);

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = request.UserId,
                Action = "ProcessSalesReturn",
                EntityName = nameof(SalesReturn),
                EntityId = request.SaleId.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return salesReturn;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PurchaseReturn> ProcessPurchaseReturnAsync(CreatePurchaseReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidBusinessOperationException("At least one item must be returned.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var purchase = await _dbContext.Purchases
                .Include(p => p.PurchaseItems)
                .FirstOrDefaultAsync(p => p.Id == request.PurchaseId, cancellationToken);

            if (purchase == null)
            {
                throw new InvalidBusinessOperationException($"Purchase with ID {request.PurchaseId} does not exist.");
            }

            var user = await _dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
            if (user == null || !user.IsActive)
            {
                throw new InvalidBusinessOperationException($"Active user with ID {request.UserId} was not found.");
            }

            var purchaseReturn = new PurchaseReturn
            {
                PurchaseId = request.PurchaseId,
                UserId = request.UserId,
                ReturnDate = DateTime.UtcNow,
                Reason = request.Reason,
                TotalAmount = 0m
            };

            decimal totalReturnAmount = 0m;

            foreach (var itemInput in request.Items)
            {
                if (itemInput.Quantity <= 0)
                {
                    throw new InvalidBusinessOperationException("Returned quantity must be greater than zero.");
                }

                var purchaseItem = purchase.PurchaseItems.FirstOrDefault(pi => pi.Id == itemInput.PurchaseItemId);
                if (purchaseItem == null)
                {
                    throw new InvalidBusinessOperationException($"Purchase item with ID {itemInput.PurchaseItemId} does not belong to purchase {request.PurchaseId}.");
                }

                // Verify return quantity does not exceed original purchased quantity minus previous returns
                var alreadyReturned = await _dbContext.PurchaseReturnItems
                    .Where(pri => pri.PurchaseItemId == itemInput.PurchaseItemId)
                    .SumAsync(pri => (int?)pri.Quantity, cancellationToken) ?? 0;

                var maxEligible = purchaseItem.Quantity - alreadyReturned;
                if (itemInput.Quantity > maxEligible)
                {
                    throw new InvalidBusinessOperationException($"Cannot return {itemInput.Quantity} units to supplier. Only {maxEligible} units remain eligible for return.");
                }

                var batch = await _dbContext.Batches.FindAsync(new object[] { itemInput.BatchId }, cancellationToken);
                if (batch == null)
                {
                    throw new InvalidBusinessOperationException($"Batch with ID {itemInput.BatchId} does not exist.");
                }

                if (batch.QuantityOnHand < itemInput.Quantity)
                {
                    throw new InvalidBusinessOperationException($"Cannot return {itemInput.Quantity} units from batch {batch.BatchNumber}. Only {batch.QuantityOnHand} available.");
                }

                // Reduce stock from batch
                batch.QuantityOnHand -= itemInput.Quantity;

                var returnItem = new PurchaseReturnItem
                {
                    PurchaseItemId = itemInput.PurchaseItemId,
                    BatchId = itemInput.BatchId,
                    Quantity = itemInput.Quantity
                };

                purchaseReturn.ReturnItems.Add(returnItem);
                totalReturnAmount += itemInput.Quantity * purchaseItem.UnitPurchasePrice;
            }

            purchaseReturn.TotalAmount = totalReturnAmount;
            _dbContext.PurchaseReturns.Add(purchaseReturn);

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = request.UserId,
                Action = "ProcessPurchaseReturn",
                EntityName = nameof(PurchaseReturn),
                EntityId = request.PurchaseId.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return purchaseReturn;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
