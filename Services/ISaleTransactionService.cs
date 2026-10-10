using PharmacySystem.Enums;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public record SaleItemRequest(int ProductId, int Quantity);

public record CreateSaleRequest(
    int? CustomerId,
    int UserId,
    string InvoiceNumber,
    PaymentMethod PaymentMethod,
    List<SaleItemRequest> Items);

public interface ISaleTransactionService
{
    /// <summary>
    /// Executes a sale transaction atomically:
    /// 1. Validates products exist and are active.
    /// 2. Validates requested quantities.
    /// 3. Applies FEFO allocation across valid non-expired batches.
    /// 4. Stores SaleItem with current Product.SellingPrice at time of sale.
    /// 5. Stores SaleItemBatchAllocation with batch unit cost and consumed quantity.
    /// 6. Decrements Batch.QuantityOnHand.
    /// 7. Persists all records atomically in a database transaction.
    /// </summary>
    Task<Sale> ProcessSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default);
}
