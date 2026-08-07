using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupplierPortal.Web.Models.Leasing;

// ---------------------------------------------------------------------------
// Landlord / Leasing module domain model.
//
// LAND (Premises) is READ-ONLY, sourced from the SQL view [dbo].[Property].
// Everything else is app-owned (leasing.* tables) and written by the portal.
// See docs/landlord-module-notes.md.
// ---------------------------------------------------------------------------

/// <summary>
/// A single row of the read-only land view <c>dbo.Property</c> — the atomic ownable "premises"
/// (a building within a property code). Property↔Building is many-to-many, so this flat premises row
/// is the unit ownership attaches to. Never migrated; mapped with <c>ToView("Property")</c>.
/// </summary>
public class Premises
{
    public int Id { get; set; }
    public string? CategoryName { get; set; }
    public string PropertyCode { get; set; } = string.Empty;
    public string? BuildingName { get; set; }

    // Source-system lease / owner columns exposed by the view (read-only, maintained by the business).
    public string? LeaseNo { get; set; }
    public string? LandLordCode { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactName { get; set; }
    public string? EmailAddress { get; set; }

    /// <summary>Dubai Municipality zone — the segment of the property code before the first dash
    /// (e.g. <c>597-1000</c> → <c>597</c>). Not stored; derived for grouping/filtering.</summary>
    [NotMapped]
    public string Zone
    {
        get
        {
            var code = PropertyCode ?? string.Empty;
            var dash = code.IndexOf('-');
            return dash > 0 ? code[..dash] : code;
        }
    }
}

public enum LandlordType { Individual = 0, Corporate = 1 }

/// <summary>Workflow states. Every transition notifies the landlord.</summary>
public enum LandlordStatus
{
    Draft = 0,
    AwaitingLandlordResponse = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4
}

public enum LandlordDocumentType
{
    EmiratesId = 0,
    TradeLicense = 1,
    Moa = 2,
    PowerOfAttorney = 3,
    AuthorisationLetter = 4,
    TitleDeed = 5,
    Other = 6
}

/// <summary>How signatures are collected for an ownership.</summary>
public enum SigningRule { All = 0, Any = 1 }

/// <summary>The owner/landlord record — the app-owned "owner information" the landlord maintains and
/// Leasing reviews. Scoped to one Business Unit; linked to one or more premises.</summary>
public class Landlord
{
    public int Id { get; set; }

    /// <summary>Owner is scoped to a single business unit (not a globally reusable party).</summary>
    public int BusinessUnitId { get; set; }

    public LandlordType Type { get; set; }

    [MaxLength(250)] public string LegalName { get; set; } = string.Empty;

    // Business-set core contact (shown read-only to the landlord).
    [MaxLength(150)] public string PrimaryContactName { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(40)] public string Mobile { get; set; } = string.Empty;

    // Landlord-updated identity fields.
    [MaxLength(50)] public string? EmiratesId { get; set; }
    [MaxLength(60)] public string? TradeLicenseNo { get; set; }
    [MaxLength(30)] public string? TaxRegistrationNo { get; set; }

    // Optional secondary contact.
    [MaxLength(150)] public string? SecondaryContactName { get; set; }
    [MaxLength(200)] public string? SecondaryContactEmail { get; set; }
    [MaxLength(40)] public string? SecondaryContactMobile { get; set; }

    public SigningRule SigningRule { get; set; } = SigningRule.All;

    public LandlordStatus Status { get; set; } = LandlordStatus.Draft;

    /// <summary>Reference issued on submission, e.g. <c>DIP-LR-2026-104882</c>.</summary>
    [MaxLength(30)] public string? Reference { get; set; }

    [MaxLength(100)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
    [MaxLength(100)] public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(500)] public string? DecisionNote { get; set; }

    public List<LandlordPremises> Premises { get; set; } = [];
    public List<LandlordSignatory> Signatories { get; set; } = [];
    public List<LandlordDocument> Documents { get; set; } = [];
}

/// <summary>Ownership link: a landlord ↔ a premises (a <c>dbo.Property</c> view row, by its Id).
/// No FK to the view — the PropertyCode/BuildingName are snapshotted for display and history.
/// Multiple landlords per premises = co-ownership; multiple premises per landlord = many holdings.</summary>
public class LandlordPremises
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    public Landlord? Landlord { get; set; }

    /// <summary>= <c>dbo.Property.Id</c> (the view's key).</summary>
    public int PremisesId { get; set; }

    [MaxLength(80)] public string PropertyCode { get; set; } = string.Empty;
    [MaxLength(250)] public string? BuildingName { get; set; }
    [MaxLength(10)] public string? Zone { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? LinkedBy { get; set; }
}

public class LandlordSignatory
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    public Landlord? Landlord { get; set; }

    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(40)] public string? Mobile { get; set; }
    public int Order { get; set; }
    /// <summary>True when this signatory is the (business-set) primary contact, auto-linked and not editable.</summary>
    public bool IsPrimaryContact { get; set; }
}

/// <summary>Landlord document metadata. Files stored on the server file system (like supplier docs);
/// all documents are retained for the complete registration trace.</summary>
public class LandlordDocument
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    public Landlord? Landlord { get; set; }

    public LandlordDocumentType DocumentType { get; set; }
    [MaxLength(260)] public string FileName { get; set; } = string.Empty;
    [MaxLength(260)] public string StoredFileName { get; set; } = string.Empty;
    [MaxLength(120)] public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string UploadedBy { get; set; } = string.Empty;
    /// <summary>Optional signatory this document belongs to (e.g. an authorisation letter).</summary>
    public int? SignatoryId { get; set; }
}

/// <summary>One tracked change the landlord made in self-service, for the review highlight + activity feed.</summary>
public class LandlordFieldChange
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    public int? LandlordAccessTokenId { get; set; }
    [MaxLength(80)] public string FieldKey { get; set; } = string.Empty;
    [MaxLength(120)] public string DisplayLabel { get; set; } = string.Empty;
    [MaxLength(1000)] public string? OldValue { get; set; }
    [MaxLength(1000)] public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(150)] public string ChangedBy { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
}

/// <summary>Immutable registration-trace event (created → invited → verified → submitted → decision),
/// giving the "complete trace of registration".</summary>
public class LandlordEvent
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    [MaxLength(60)] public string EventType { get; set; } = string.Empty;
    [MaxLength(500)] public string? Detail { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(150)] public string? By { get; set; }
}

/// <summary>Tokenised self-service access + OTP for a landlord (mirrors the supplier access-token pattern;
/// verification method is pluggable — OTP now, UAE Pass later).</summary>
public class LandlordAccessToken
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    public Landlord? Landlord { get; set; }

    [MaxLength(64)] public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    [MaxLength(100)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UsedAt { get; set; }

    [MaxLength(200)] public string? OtpCodeHash { get; set; }
    public DateTime? OtpGeneratedAt { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public int OtpAttemptCount { get; set; }
    public int OtpSendCount { get; set; }
    public DateTime? OtpVerifiedAt { get; set; }

    [MaxLength(64)] public string? SessionTokenHash { get; set; }
    public DateTime? SessionExpiresAt { get; set; }
}
