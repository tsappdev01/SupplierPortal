using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class AuditLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string ActorUsername { get; set; } = string.Empty;

    public AuditAction Action { get; set; }

    [Required, MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }
}

public enum AuditAction
{
    Login,
    Logout,
    Create,
    Update,
    Activate,
    Deactivate,
    Delete,
    Notify
}
