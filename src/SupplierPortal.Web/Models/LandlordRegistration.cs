using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupplierPortal.Web.Models;

public enum AgreementType
{
    Corporate = 1,
    Individual = 2
}

public enum SigningRule
{
    AllMustSign = 1,
    AnyOneCanSign = 2
}

/// <summary>Where a signatory row originated. Primary/Secondary rows mirror the contact steps and
/// are locked (edited only in their source step); Added rows are created directly on the
/// signatories step.</summary>
public enum SignatorySource
{
    Primary = 1,
    Secondary = 2,
    Added = 3
}

public enum LandlordRegistrationStatus
{
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4
}

/// <summary>A completed landlord registration for a Dubai Investments Park plot, captured through
/// the public multi-step wizard (agreement type, property code, primary/secondary contacts,
/// authorised signatories, declaration and UAE Pass verification).</summary>
public class LandlordRegistration
{
    public int Id { get; set; }

    /// <summary>System-generated on submission as DIP-LR-{year}-{6-digit sequence}.</summary>
    [Required, MaxLength(30)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public AgreementType AgreementType { get; set; }

    // Property code is assembled from a plot picked off the DIP master list and a free-typed unit
    // number; together they form the "{Plot}-{Unit}" code held on the lease.
    [Required, MaxLength(10)]
    public string Plot { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string Unit { get; set; } = string.Empty;

    // Primary contact — the person DIP deals with on the tenancy. Email must match the UAE Pass
    // account used to sign the submission.
    [Required, MaxLength(200)]
    public string PrimaryName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string PrimaryMobile { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string PrimaryEmail { get; set; } = string.Empty;

    // Secondary contact — optional fallback reached when the primary is unavailable.
    public bool HasSecondaryContact { get; set; }

    [MaxLength(200)]
    public string? SecondaryName { get; set; }

    [MaxLength(30)]
    public string? SecondaryMobile { get; set; }

    [MaxLength(150)]
    public string? SecondaryEmail { get; set; }

    public SigningRule SigningRule { get; set; } = SigningRule.AllMustSign;

    public bool DeclarationAccepted { get; set; }

    // UAE Pass verification. In this prototype the handshake is simulated (as the source design
    // labels it), but the resulting identity is persisted alongside the submission.
    [MaxLength(200)]
    public string? VerifiedName { get; set; }

    [MaxLength(150)]
    public string? VerifiedEmail { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public LandlordRegistrationStatus Status { get; set; } = LandlordRegistrationStatus.Submitted;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public List<LandlordSignatory> Signatories { get; set; } = [];

    public List<LandlordDocument> Documents { get; set; } = [];

    [NotMapped]
    public string PropertyCode => string.IsNullOrEmpty(Plot) || string.IsNullOrEmpty(Unit)
        ? string.Empty
        : $"{Plot}-{Unit}";
}

/// <summary>An authorised signatory attached to a registration, in signing order.</summary>
public class LandlordSignatory
{
    public int Id { get; set; }

    public int LandlordRegistrationId { get; set; }
    public LandlordRegistration? Registration { get; set; }

    /// <summary>1-based position; only meaningful when the signing rule is "All must sign".</summary>
    public int Sequence { get; set; }

    public SignatorySource Source { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Mobile { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }
}

/// <summary>Metadata for a document uploaded during registration (Emirates ID, power of
/// attorney / MOA, authorisation letters). The physical file lives outside wwwroot under the app's
/// content root at doc/landlord/{RegistrationId}/{StoredFileName}, so it is never directly
/// web-servable — downloads only go through the authenticated /landlord/documents/{id} endpoint.</summary>
public class LandlordDocument
{
    public int Id { get; set; }

    public int LandlordRegistrationId { get; set; }
    public LandlordRegistration? Registration { get; set; }

    /// <summary>Identifies which upload this is: "primary-eid", "primary-poa", "secondary-eid",
    /// "secondary-poa", or "signatory:{index}:eid" / "signatory:{index}:poa".</summary>
    [Required, MaxLength(60)]
    public string Slot { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
