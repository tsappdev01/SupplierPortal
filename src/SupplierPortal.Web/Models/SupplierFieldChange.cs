using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>One row per changed field on a Supplier, recorded by <see cref="Services.SupplierService.UpdateAsync"/>
/// for every update (admin-made or supplier self-service), so an administrator can see exactly what changed,
/// by whom, and acknowledge review of it.</summary>
public class SupplierFieldChange
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Set only when the change came from a supplier self-service submission; null for admin edits.</summary>
    public int? SupplierAccessTokenId { get; set; }
    public SupplierAccessToken? SupplierAccessToken { get; set; }

    [Required, MaxLength(100)]
    public string FieldKey { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string DisplayLabel { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? OldValue { get; set; }

    [MaxLength(500)]
    public string? NewValue { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(150)]
    public string ChangedBy { get; set; } = string.Empty;

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }
}
