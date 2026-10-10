namespace PharmacySystem.Models.Common;

/// <summary>
/// Reusable ViewModel for empty states across tables, search results, and list views.
/// </summary>
public class EmptyStateViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Message
    {
        get => Description;
        set => Description = value;
    }
    public string? Icon { get; set; } = "inbox";
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
    public string? SecondaryActionText { get; set; }
    public string? SecondaryActionUrl { get; set; }

    public EmptyStateViewModel()
    {
    }

    public EmptyStateViewModel(
        string title,
        string? description = null,
        string? icon = "inbox",
        string? actionText = null,
        string? actionUrl = null)
    {
        Title = title;
        Description = description;
        Icon = icon;
        ActionText = actionText;
        ActionUrl = actionUrl;
    }
}
