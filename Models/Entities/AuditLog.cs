namespace PharmacySystem.Models.Entities;

/// <summary>
/// Audit trail for critical system actions and events.
/// Note: Initial implementation does not require OldValues/NewValues JSON fields.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    // Navigation properties
    public User? User { get; set; }
}
