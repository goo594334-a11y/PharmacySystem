namespace PharmacySystem.Models.Common;

/// <summary>
/// Reusable ViewModel for confirmation modals (e.g. Delete, Deactivate, Status Change).
/// </summary>
public class ConfirmModalViewModel
{
    public string ModalId { get; set; } = "confirmModal";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ItemIdentifier { get; set; }
    public string? WarningNote { get; set; }
    public string ConfirmButtonText { get; set; } = "Confirm";
    public string CancelButtonText { get; set; } = "Cancel";
    public string FormActionUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "post";
    public bool IsDestructive { get; set; } = true;
    public IDictionary<string, string> HiddenFields { get; set; } = new Dictionary<string, string>();
}
