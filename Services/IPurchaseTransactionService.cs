using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services;

public record PurchaseItemInput(
    int ProductId,
    int Quantity,
    decimal UnitPurchasePrice,
    string BatchNumber,
    DateOnly ExpiryDate,
    int LocationId,
    string? UnitName = null,
    int ConversionFactor = 1);

public record CreatePurchaseRequest(
    int SupplierId,
    int UserId,
    string InvoiceNumber,
    string? Notes,
    List<PurchaseItemInput> Items);

public interface IPurchaseTransactionService
{
    /// <summary>
    /// Creates a purchase order in Draft status.
    /// Does not alter inventory until confirmed.
    /// </summary>
    Task<Purchase> CreateDraftPurchaseAsync(CreatePurchaseRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms an existing draft purchase atomically:
    /// 1. Verifies purchase is in Draft status.
    /// 2. For each PurchaseItem, creates or increments the physical Batch record at the assigned Location.
    /// 3. Updates Purchase status to Confirmed.
    /// 4. Commits changes within an atomic database transaction.
    /// </summary>
    Task<Purchase> ConfirmPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default);
}
