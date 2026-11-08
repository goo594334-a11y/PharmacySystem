using PharmacySystem.Enums;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public record SaleItemRequest(
    int ProductId,
    int Quantity,
    string? UnitName = null,
    int? ConversionFactor = null,
    decimal? UnitPrice = null);

public record CreateSaleRequest(
    int? CustomerId,
    int UserId,
    string InvoiceNumber,
    PaymentMethod PaymentMethod,
    List<SaleItemRequest> Items,
    decimal? CashAmount = null,
    decimal? CardAmount = null,
    decimal? VodafoneCashAmount = null,
    string? PaymentReference = null);

public interface ISaleTransactionService
{
    /// <summary>
    /// Executes a sale transaction atomically:
    /// 1. Validates products exist and are active.
    /// 2. Validates requested quantities in their configured selling units.
    /// 3. Converts selling unit to equivalent base quantity and applies FEFO allocation.
    /// 4. Stores SaleItem with the snapshot selling price and snapshot unit.
    /// 5. Stores SaleItemBatchAllocation with batch unit cost and consumed base quantity.
    /// 6. Decrements Batch.QuantityOnHand.
    /// 7. Validates split payment allocations if PaymentMethod is Split.
    /// 8. Logs detailed audit log with transaction and unit breakdown.
    /// 9. Persists all records atomically in a database transaction.
    /// </summary>
    Task<Sale> ProcessSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default);
}
