using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Exceptions;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public class SaleTransactionService : ISaleTransactionService
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IFefoAllocationService _fefoAllocationService;

    public SaleTransactionService(PharmacyDbContext dbContext, IFefoAllocationService fefoAllocationService)
    {
        _dbContext = dbContext;
        _fefoAllocationService = fefoAllocationService;
    }

    public async Task<Sale> ProcessSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidBusinessOperationException("Sale must contain at least one item.");
        }

        // Execute atomically using a database transaction
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await _dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
            if (user == null || !user.IsActive)
            {
                throw new InvalidBusinessOperationException($"Active user with ID {request.UserId} was not found.");
            }

            if (request.CustomerId.HasValue)
            {
                var customer = await _dbContext.Customers.FindAsync(new object[] { request.CustomerId.Value }, cancellationToken);
                if (customer == null || !customer.IsActive)
                {
                    throw new InvalidBusinessOperationException($"Active customer with ID {request.CustomerId.Value} was not found.");
                }
            }

            var sale = new Sale
            {
                CustomerId = request.CustomerId,
                UserId = request.UserId,
                SaleDate = DateTime.UtcNow,
                InvoiceNumber = request.InvoiceNumber,
                PaymentMethod = request.PaymentMethod,
                TotalAmount = 0m
            };

            decimal calculatedTotal = 0m;
            var today = DateOnly.FromDateTime(DateTime.Today);

            foreach (var itemReq in request.Items)
            {
                if (itemReq.Quantity <= 0)
                {
                    throw new InvalidBusinessOperationException("Item quantity must be greater than zero.");
                }

                var product = await _dbContext.Products
                    .Include(p => p.Batches)
                    .FirstOrDefaultAsync(p => p.Id == itemReq.ProductId, cancellationToken);

                if (product == null)
                {
                    throw new InvalidBusinessOperationException($"Product with ID {itemReq.ProductId} was not found.");
                }

                if (!product.IsActive)
                {
                    throw new InvalidBusinessOperationException($"Product '{product.Name}' is inactive and cannot be sold.");
                }

                // FEFO allocation across valid, non-expired batches
                var allocations = _fefoAllocationService.CalculateAllocations(
                    product,
                    product.Batches,
                    itemReq.Quantity,
                    today);

                var saleItem = new SaleItem
                {
                    Product = product,
                    ProductId = product.Id,
                    Quantity = itemReq.Quantity,
                    UnitPrice = product.SellingPrice // Preserves price at moment of sale
                };

                foreach (var alloc in allocations)
                {
                    // Decrement batch stock
                    alloc.Batch.QuantityOnHand -= alloc.AllocatedQuantity;

                    saleItem.BatchAllocations.Add(new SaleItemBatchAllocation
                    {
                        Batch = alloc.Batch,
                        BatchId = alloc.Batch.Id,
                        Quantity = alloc.AllocatedQuantity,
                        UnitCost = alloc.UnitCost
                    });
                }

                sale.SaleItems.Add(saleItem);
                calculatedTotal += saleItem.Quantity * saleItem.UnitPrice;
            }

            sale.TotalAmount = calculatedTotal;
            _dbContext.Sales.Add(sale);

            // Persist all changes and commit transaction atomically
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return sale;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
