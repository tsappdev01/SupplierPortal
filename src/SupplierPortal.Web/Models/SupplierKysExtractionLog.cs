using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>One row per field read from a supplier document during the "Extract &amp; store KYS data from
/// documents" action. Records the field, the value Claude read, the source document type / file name, and
/// whether the value was stored onto the supplier or skipped (because the target field was already filled).
/// Audit / traceability only — never read back into the form.</summary>
public class SupplierKysExtractionLog
{
    public int Id { get; set; }

    /// <summary>The supplier the documents belong to.</summary>
    public int SupplierId { get; set; }

    /// <summary>The source document's id (null if that document was later removed).</summary>
    public int? DocumentId { get; set; }

    /// <summary>The source document type, e.g. "VatCertification", "TradeLicense".</summary>
    [MaxLength(50)] public string DocumentType { get; set; } = string.Empty;

    /// <summary>Friendly source-document name shown to the reviewer, e.g. "VAT Certificate".</summary>
    [MaxLength(100)] public string DocumentLabel { get; set; } = string.Empty;

    /// <summary>The uploaded file the value was read from.</summary>
    [MaxLength(400)] public string? FileName { get; set; }

    /// <summary>The KYS field the value maps to, e.g. "TRN / VAT Registration Number".</summary>
    [MaxLength(150)] public string FieldName { get; set; } = string.Empty;

    /// <summary>The value Claude read from the document.</summary>
    [MaxLength(2000)] public string? FieldValue { get; set; }

    /// <summary>"Stored" (written to an empty field) or "Skipped (already filled)".</summary>
    [MaxLength(40)] public string Outcome { get; set; } = string.Empty;

    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The internal reviewer who ran the extraction.</summary>
    [MaxLength(100)] public string ExtractedBy { get; set; } = string.Empty;
}
