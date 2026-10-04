using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>A partner/owner row read from a supplier's Trade License "Partners" (ملحق الشركاء)
/// annex. Captured automatically when a Trade License PDF is uploaded and kept for reference
/// only - it is intentionally never surfaced anywhere in the UI.</summary>
public class SupplierPartner
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Nationality { get; set; }

    /// <summary>The partner's serial / person number ("مسلسل الشخص") from the license.</summary>
    [MaxLength(50)]
    public string? SerialNo { get; set; }

    /// <summary>Ownership share exactly as printed on the license, e.g. "99.0000000".</summary>
    [MaxLength(30)]
    public string? Share { get; set; }

    /// <summary>The partner's role on the license, e.g. "Shares Owner", "Manager", "Partner".</summary>
    [MaxLength(80)]
    public string? Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
