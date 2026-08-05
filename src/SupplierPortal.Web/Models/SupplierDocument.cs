using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public enum SupplierDocumentType { TradeLicense, VatCertification, Other, BankConfirmationLetter }

/// <summary>Metadata for a supplier-uploaded document. The physical file lives outside wwwroot
/// (under the app's content root at doc/supplier/{SupplierId}/{StoredFileName}), so it is never
/// directly web-servable - downloads only go through the authenticated
/// /suppliers/documents/{id} endpoint.</summary>
public class SupplierDocument
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public SupplierDocumentType DocumentType { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(150)]
    public string UploadedBy { get; set; } = string.Empty;
}
