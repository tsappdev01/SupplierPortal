using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>A named group of portal users (e.g. "ALL_USERS") used as a notification recipient list.
/// Membership is a many-to-many with <see cref="User"/> via the NotificationGroupUsers join table.</summary>
public class NotificationGroup
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>When false, the group is kept but excluded from notifications.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The users that belong to this group.</summary>
    public List<User> Users { get; set; } = [];
}
