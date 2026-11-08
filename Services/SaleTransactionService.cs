using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Exceptions;
using PharmacySystem.Models.Entities;
using PharmacySystem.Services.Packaging;

namespace PharmacySystem.Services;

public class SaleTransactionService : ISaleTransactionService
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IFefoAllocationService _fefoAllocationService;
    private readonly IProductPackagingService _packagingService;

    public SaleTransactionService(
        PharmacyDbContext dbContext,
        IFefoAllocationService fefoAllocationService,
        IProductPackagingService packagingService)
    {
        _dbContext = dbContext;
        _fefoAllocationService = fefoAllocationService;
        _packagingService = packagingService;
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
                TotalAmount = 0m,
                PharmacyId = user.PharmacyId ?? 1
            };

            decimal calculatedTotal = 0m;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var itemAuditSummaries = new List<string>();

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

                // 1. Calculate base unit equivalent and line total via packaging service
                var (equivalentBaseQty, unitPrice, lineTotal) = _packagingService.CalculateAllocation(
                    product,
                    itemReq.UnitName,
                    itemReq.Quantity);

                // Override unit price if custom price snapshot provided
                if (itemReq.UnitPrice.HasValue && itemReq.UnitPrice.Value > 0)
                {
                    unitPrice = itemReq.UnitPrice.Value;
                    lineTotal = itemReq.Quantity * unitPrice;
                }

                // 2. FEFO allocation of the equivalent base quantity across valid, non-expired batches
                var allocations = _fefoAllocationService.CalculateAllocations(
                    product,
                    product.Batches,
                    equivalentBaseQty,
                    today);

                var saleItem = new SaleItem
                {
                    Product = product,
                    ProductId = product.Id,
                    Quantity = itemReq.Quantity, // Quantity in the selected unit
                    UnitPrice = unitPrice        // Unit price snapshot at time of sale
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
                calculatedTotal += lineTotal;

                var unitLabel = string.IsNullOrWhiteSpace(itemReq.UnitName) ? "Unit" : itemReq.UnitName;
                itemAuditSummaries.Add($"{product.Name}: {itemReq.Quantity} {unitLabel} (EqBase: {equivalentBaseQty}) @ {unitPrice:N2}");
            }

            // 3. Split payment validation
            if (request.PaymentMethod == PaymentMethod.Split)
            {
                var cash = request.CashAmount ?? 0m;
                var card = request.CardAmount ?? 0m;
                var vfCash = request.VodafoneCashAmount ?? 0m;
                var totalSplitPaid = cash + card + vfCash;

                if (Math.Abs(totalSplitPaid - calculatedTotal) > 0.01m)
                {
                    throw new InvalidBusinessOperationException(
                        $"Split payment total ({totalSplitPaid:N2} EGP) does not match invoice total ({calculatedTotal:N2} EGP). [Cash: {cash:N2}, Card: {card:N2}, Vodafone Cash: {vfCash:N2}]");
                }
            }

            sale.TotalAmount = calculatedTotal;
            _dbContext.Sales.Add(sale);

            // 4. Audit Log recording
            var paymentDetails = request.PaymentMethod == PaymentMethod.Split
                ? $"Split(Cash:{request.CashAmount ?? 0:N2}, Card:{request.CardAmount ?? 0:N2}, VF:{request.VodafoneCashAmount ?? 0:N2})"
                : request.PaymentMethod.ToString();

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = request.UserId,
                PharmacyId = sale.PharmacyId,
                Action = "SaleCompleted",
                EntityName = nameof(Sale),
                EntityId = sale.InvoiceNumber,
                Timestamp = DateTime.UtcNow
            });

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
