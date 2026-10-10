namespace PharmacySystem.Exceptions;

public class InsufficientStockException : Exception
{
    public int ProductId { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }

    public InsufficientStockException(int productId, int requestedQuantity, int availableQuantity)
        : base($"Insufficient stock for product ID {productId}. Requested: {requestedQuantity}, Available non-expired: {availableQuantity}.")
    {
        ProductId = productId;
        RequestedQuantity = requestedQuantity;
        AvailableQuantity = availableQuantity;
    }
}
