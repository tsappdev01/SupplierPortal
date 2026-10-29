using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public enum SupplierDocumentType
{
    TradeLicense = 0,
    VatCertification = 1,
    Other = 2,
    BankConfirmationLetter = 3,
    SignedDeclaration = 4,
    // KYS document types
    EmiratesIdOrPassport = 5,
    ProofOfAddress = 6,
    PriceListOrQuotation = 7,
    ProofOfOrigin = 8
}

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

    /// <summary>The file bytes when stored in the database (blob storage disabled). Null when the file lives
    /// in Azure Blob Storage (see <see cref="BlobKey"/>) or, for legacy records, on local disk.</summary>
    public byte[]? Content { get; set; }

    /// <summary>Blob storage key when the file lives in Azure Blob Storage. Null when the bytes are in
    /// <see cref="Content"/> (database) or, for legacy records, on local disk (via <see cref="StoredFileName"/>).</summary>
    [MaxLength(400)]
    public string? BlobKey { get; set; }

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(150)]
    public string UploadedBy { get; set; } = string.Empty;
}
