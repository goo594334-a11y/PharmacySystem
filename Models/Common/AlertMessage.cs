namespace PharmacySystem.Models.Common;

public enum AlertType
{
    Success,
    Danger,
    Warning,
    Info
}

/// <summary>
/// Flash notification message stored in TempData across redirects.
/// </summary>
public class AlertMessage
{
    public AlertType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool Dismissible { get; set; } = true;

    public string CssClass => Type switch
    {
        AlertType.Success => "alert-success",
        AlertType.Danger => "alert-danger",
        AlertType.Warning => "alert-warning",
        AlertType.Info => "alert-info",
        _ => "alert-primary"
    };

    public string IconName => Type switch
    {
        AlertType.Success => "check-circle",
        AlertType.Danger => "x-circle",
        AlertType.Warning => "exclamation-triangle",
        AlertType.Info => "info-circle",
        _ => "bell"
    };
}
