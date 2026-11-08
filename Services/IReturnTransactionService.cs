using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public record SalesReturnItemInput(int SaleItemId, int BatchId, int Quantity, bool ReturnToStock = false);

public record CreateSalesReturnRequest(
    int SaleId,
    int UserId,
    string Reason,
    List<SalesReturnItemInput> Items);

public record PurchaseReturnItemInput(int PurchaseItemId, int BatchId, int Quantity);

public record CreatePurchaseReturnRequest(
    int PurchaseId,
    int UserId,
    string Reason,
    List<PurchaseReturnItemInput> Items);

public interface IReturnTransactionService
{
    /// <summary>
    /// Processes a sales return atomically:
    /// 1. Verifies original Sale and items.
    /// 2. Restores returned quantity back to the corresponding Batch.QuantityOnHand.
    /// 3. Creates SalesReturn and SalesReturnItem records.
    /// </summary>
    Task<SalesReturn> ProcessSalesReturnAsync(CreateSalesReturnRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a purchase return back to supplier atomically:
    /// 1. Verifies original Purchase and items.
    /// 2. Verifies batch has enough quantity to return.
    /// 3. Decrements Batch.QuantityOnHand.
    /// 4. Creates PurchaseReturn and PurchaseReturnItem records.
    /// </summary>
    Task<PurchaseReturn> ProcessPurchaseReturnAsync(CreatePurchaseReturnRequest request, CancellationToken cancellationToken = default);
}
