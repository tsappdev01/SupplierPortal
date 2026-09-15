using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class BusinessUnit
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Business unit's public self-service host / web domain, e.g. "supplier.dipark.com" (scheme
    /// optional). When set, links emailed to this unit's suppliers/customers are built on this host so each
    /// business unit can have its own DMZ URL; otherwise the global SelfService:PortalUrl (then the request
    /// base) is used. Optional.</summary>
    [MaxLength(200)]
    public string? Domain { get; set; }

    /// <summary>Absolute URL of the unit's logo hosted on a public server, e.g.
    /// "https://cdn.dipark.com/logo.png". When set it is used as the brand image in emails and other
    /// external links (a hosted URL renders more reliably in mail clients than an embedded image), taking
    /// precedence over the uploaded <see cref="Logo"/>/<see cref="Thumbnail"/>. Optional.</summary>
    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    /// <summary>The original uploaded logo, stored as-is.</summary>
    public byte[]? Logo { get; set; }

    [MaxLength(100)]
    public string? LogoContentType { get; set; }

    /// <summary>Blob storage key when the logo lives in Azure Blob Storage; then <see cref="Logo"/> is null.</summary>
    [MaxLength(400)]
    public string? LogoBlobKey { get; set; }

    /// <summary>Blob storage key when the thumbnail lives in Azure Blob Storage; then <see cref="Thumbnail"/> is null.</summary>
    [MaxLength(400)]
    public string? ThumbnailBlobKey { get; set; }

    /// <summary>A 640×640 PNG thumbnail derived from <see cref="Logo"/> on upload (aspect ratio preserved,
    /// padded onto a transparent square). Kept alongside the original for list/avatar rendering.</summary>
    public byte[]? Thumbnail { get; set; }

    [MaxLength(100)]
    public string? ThumbnailContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<User> Users { get; set; } = [];

    public List<Supplier> Suppliers { get; set; } = [];
}
