using Microsoft.Extensions.Localization;
using PharmacySystem.Models.ViewModels.Notification;
using PharmacySystem.Resources;

namespace PharmacySystem.Services.Notifications;

public interface INotificationService
{
    Task<List<NotificationItemViewModel>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
}

public class NotificationService : INotificationService
{
    private readonly IInventoryService _inventoryService;
    private readonly IStringLocalizer<SharedResource> _loc;

    public NotificationService(IInventoryService inventoryService, IStringLocalizer<SharedResource> loc)
    {
        _inventoryService = inventoryService;
        _loc = loc;
    }

    public async Task<List<NotificationItemViewModel>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var notifications = new List<NotificationItemViewModel>();

        // 1. Low stock alerts
        var lowStockProducts = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        if (lowStockProducts.Count > 0)
        {
            notifications.Add(new NotificationItemViewModel
            {
                Title = _loc["LowStockAlerts"],
                Message = string.Format(_loc["LowStockAlertNotification"], lowStockProducts.Count),
                ActionUrl = "/Inventory/LowStock",
                Severity = NotificationSeverity.Warning,
                Timestamp = DateTime.UtcNow
            });
        }

        // 2. Near expiry alerts (90 days)
        var nearExpiryBatches = await _inventoryService.GetNearExpiryBatchesAsync(90, cancellationToken);
        if (nearExpiryBatches.Count > 0)
        {
            notifications.Add(new NotificationItemViewModel
            {
                Title = _loc["NearExpiryAlerts"],
                Message = string.Format(_loc["NearExpiryAlertNotification"], nearExpiryBatches.Count),
                ActionUrl = "/Inventory/NearExpiry",
                Severity = NotificationSeverity.Warning,
                Timestamp = DateTime.UtcNow
            });
        }

        // 3. Expired batches with stock
        var expiredBatches = await _inventoryService.GetExpiredBatchesWithStockAsync(cancellationToken);
        if (expiredBatches.Count > 0)
        {
            notifications.Add(new NotificationItemViewModel
            {
                Title = _loc["ExpiredBatches"],
                Message = string.Format(_loc["ExpiredBatchAlertNotification"], expiredBatches.Count),
                ActionUrl = "/Inventory/Expired",
                Severity = NotificationSeverity.Danger,
                Timestamp = DateTime.UtcNow
            });
        }

        return notifications;
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveNotificationsAsync(cancellationToken);
        return active.Count;
    }
}
