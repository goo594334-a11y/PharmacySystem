namespace PharmacySystem.Models.ViewModels.Notification;

public enum NotificationSeverity
{
    Info,
    Warning,
    Danger
}

public class NotificationItemViewModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }

    public string BadgeClass => Severity switch
    {
        NotificationSeverity.Danger => "bg-danger",
        NotificationSeverity.Warning => "bg-warning text-dark",
        _ => "bg-info text-dark"
    };

    public string IconName => Severity switch
    {
        NotificationSeverity.Danger => "exclamation-circle",
        NotificationSeverity.Warning => "exclamation-triangle",
        _ => "info-circle"
    };
}
