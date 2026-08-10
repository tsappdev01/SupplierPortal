using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class BusinessUnit
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Business unit's web/email domain, e.g. "dipark.com" (no scheme). Optional.</summary>
    [MaxLength(200)]
    public string? Domain { get; set; }

    /// <summary>The original uploaded logo, stored as-is.</summary>
    public byte[]? Logo { get; set; }

    [MaxLength(100)]
    public string? LogoContentType { get; set; }

    /// <summary>A 640×640 PNG thumbnail derived from <see cref="Logo"/> on upload (aspect ratio preserved,
    /// padded onto a transparent square). Kept alongside the original for list/avatar rendering.</summary>
    public byte[]? Thumbnail { get; set; }

    [MaxLength(100)]
    public string? ThumbnailContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<User> Users { get; set; } = [];

    public List<Supplier> Suppliers { get; set; } = [];
}
