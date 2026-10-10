using Microsoft.AspNetCore.Mvc;
using PharmacySystem.Services.Notifications;

namespace PharmacySystem.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notificationService;

    public NotificationBellViewComponent(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var notifications = await _notificationService.GetActiveNotificationsAsync();
        return View(notifications);
    }
}
