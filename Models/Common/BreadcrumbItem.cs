namespace PharmacySystem.Models.Common;

/// <summary>
/// Represents a single breadcrumb item in the navigation trail.
/// </summary>
public class BreadcrumbItem
{
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsActive { get; set; }

    public BreadcrumbItem()
    {
    }

    public BreadcrumbItem(string title, string? url = null, bool isActive = false)
    {
        Title = title;
        Url = url;
        IsActive = isActive;
    }
}
