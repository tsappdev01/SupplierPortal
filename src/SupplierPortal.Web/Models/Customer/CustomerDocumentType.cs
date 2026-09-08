using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models.Customer;

/// <summary>Master list of KYC document types a customer may be asked to upload (Emirates ID, Passport,
/// Proof of Address, …). Each carries whether it is mandatory and a short requirement note. Seeded from the
/// UAE KYC checklist; the mandatory flag can be adjusted per business need.</summary>
public class CustomerDocumentType
{
    public int Id { get; set; }

    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>Short requirement note shown as a hint (e.g. "Utility bill, tenancy contract, …").</summary>
    [MaxLength(300)] public string? Requirement { get; set; }

    /// <summary>When true the document must be uploaded before the registration can be submitted.</summary>
    public bool IsRequired { get; set; }

    public int Sequence { get; set; }

    public bool IsActive { get; set; } = true;
}
