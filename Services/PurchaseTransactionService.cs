using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Exceptions;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public class PurchaseTransactionService : IPurchaseTransactionService
{
    private readonly PharmacyDbContext _dbContext;

    public PurchaseTransactionService(PharmacyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Purchase> CreateDraftPurchaseAsync(CreatePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidBusinessOperationException("At least one purchase item is required.");
        }

        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { request.SupplierId }, cancellationToken);
        if (supplier == null || !supplier.IsActive)
        {
            throw new InvalidBusinessOperationException($"Active supplier with ID {request.SupplierId} was not found.");
        }

        var user = await _dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null || !user.IsActive)
        {
            throw new InvalidBusinessOperationException($"Active user with ID {request.UserId} was not found.");
        }

        var purchase = new Purchase
        {
            SupplierId = request.SupplierId,
            UserId = request.UserId,
            PurchaseDate = DateTime.UtcNow,
            InvoiceNumber = request.InvoiceNumber,
            Notes = request.Notes,
            Status = PurchaseStatus.Draft,
            TotalAmount = 0m
        };

        decimal total = 0m;

        foreach (var itemInput in request.Items)
        {
            if (itemInput.Quantity <= 0)
            {
                throw new InvalidBusinessOperationException("Purchase item quantity must be greater than zero.");
            }

            if (itemInput.UnitPurchasePrice < 0)
            {
                throw new InvalidBusinessOperationException("Unit purchase price cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(itemInput.BatchNumber))
            {
                throw new InvalidBusinessOperationException("Batch number is required for every purchase item.");
            }

            var productExists = await _dbContext.Products.AnyAsync(p => p.Id == itemInput.ProductId, cancellationToken);
            if (!productExists)
            {
                throw new InvalidBusinessOperationException($"Product with ID {itemInput.ProductId} does not exist.");
            }

            var locationExists = await _dbContext.Locations.AnyAsync(l => l.Id == itemInput.LocationId && l.IsActive, cancellationToken);
            if (!locationExists)
            {
                throw new InvalidBusinessOperationException($"Active location with ID {itemInput.LocationId} does not exist.");
            }

            var purchaseItem = new PurchaseItem
            {
                ProductId = itemInput.ProductId,
                Quantity = itemInput.Quantity,
                UnitPurchasePrice = itemInput.UnitPurchasePrice,
                BatchNumber = itemInput.BatchNumber.Trim(),
                ExpiryDate = itemInput.ExpiryDate,
                LocationId = itemInput.LocationId
            };

            purchase.PurchaseItems.Add(purchaseItem);
            total += purchaseItem.Quantity * purchaseItem.UnitPurchasePrice;
        }

        purchase.TotalAmount = total;

        _dbContext.Purchases.Add(purchase);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return purchase;
    }

    public async Task<Purchase> ConfirmPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var purchase = await _dbContext.Purchases
                .Include(p => p.PurchaseItems)
                .FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken);

            if (purchase == null)
            {
                throw new InvalidBusinessOperationException($"Purchase with ID {purchaseId} was not found.");
            }

            if (purchase.Status != PurchaseStatus.Draft)
            {
                throw new InvalidBusinessOperationException($"Purchase {purchaseId} cannot be confirmed because its status is {purchase.Status}.");
            }

            if (purchase.PurchaseItems.Count == 0)
            {
                throw new InvalidBusinessOperationException("Cannot confirm a purchase without any items.");
            }

            var now = DateTime.UtcNow;

            // Atomically create or update stock batches for each PurchaseItem
            foreach (var item in purchase.PurchaseItems)
            {
                // Each PurchaseItem represents one received batch/lot.
                // We create a new stock batch record for this arrival, preserving lot identity and cost.
                var newBatch = new Batch
                {
                    ProductId = item.ProductId,
                    LocationId = item.LocationId,
                    BatchNumber = item.BatchNumber,
                    AddedDate = now,
                    ExpiryDate = item.ExpiryDate,
                    QuantityOnHand = item.Quantity,
                    PurchasePrice = item.UnitPurchasePrice
                };

                _dbContext.Batches.Add(newBatch);
            }

            purchase.Status = PurchaseStatus.Confirmed;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return purchase;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
