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

    /// <summary>When true the document must be uploaded before the registration can be submitted. Gated by the
    /// optional conditional rule below — a required document only becomes mandatory when the rule is satisfied.</summary>
    public bool IsRequired { get; set; }

    public int Sequence { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optional conditional rule: the FieldKey of a controlling customer field (a Yes/No or LOV field).
    /// When set, this document is only shown (and, if <see cref="IsRequired"/>, required) while that field's
    /// value equals <see cref="ControllingValue"/>; otherwise it is hidden from the checklist and never required.
    /// Null = always applies (the default).</summary>
    [MaxLength(100)] public string? ControllingFieldKey { get; set; }

    /// <summary>The controlling field's value that makes this document apply: "true"/"false" for a Yes/No field,
    /// or the exact option text for an LOV field.</summary>
    [MaxLength(100)] public string? ControllingValue { get; set; }
}
