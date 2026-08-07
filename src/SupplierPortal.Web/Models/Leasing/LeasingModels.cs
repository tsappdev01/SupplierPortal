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

public enum LandlordType { Individual = 0, Company = 1 }

/// <summary>Registration lifecycle (FRD §27). Every transition is audited and most notify the landlord.</summary>
public enum LandlordStatus
{
    Draft = 0,                 // registration created by Business Centre, not yet sent
    InvitationSent = 1,        // secure link emailed to the Property View address
    OtpVerified = 2,           // landlord passed OTP at least once
    InProgress = 3,            // landlord has opened / saved a draft
    Submitted = 4,             // landlord submitted for review
    UnderReview = 5,           // Business Centre opened the review
    CorrectionRequired = 6,    // Business Centre requested re-upload / correction
    Rejected = 7,              // Business Centre rejected (reason recorded)
    RegistrationComplete = 8   // Business Centre completed; link deactivated
}

public enum LandlordDocumentType
{
    EmiratesId = 0,
    TradeLicense = 1,
    Moa = 2,
    PowerOfAttorney = 3,
    AuthorisationLetter = 4,
    TitleDeed = 5,
    Other = 6,
    /// <summary>Combined MOA/POA slot — a company provides one document that is either its MOA or a POA.</summary>
    MoaOrPoa = 7
}

/// <summary>Role a contact plays for the landlord (FRD contact-type field).</summary>
public enum ContactType
{
    Owner = 0,
    AuthorisedRepresentative = 1,
    Manager = 2,
    Legal = 3,
    Other = 4
}

/// <summary>Which registration comments are: a correction request or a rejection reason.</summary>
public enum LandlordCommentType { Correction = 0, Rejection = 1, Note = 2 }

/// <summary>How signatures are collected for an ownership.</summary>
public enum SigningRule { All = 0, Any = 1 }

/// <summary>A landlord registration / update request (FRD LandlordRegistration). Initiated by Business
/// Centre from a property: the Property-View fields (Property Code / Company / Building / Email) are
/// snapshotted read-only, and the invitation is always sent to the snapshotted <see cref="Email"/>. The
/// landlord fills in contacts, documents and signatories; Business Centre reviews, requests correction,
/// rejects (with reason) or completes.</summary>
public class Landlord
{
    public int Id { get; set; }

    /// <summary>Scoped to a single business unit.</summary>
    public int BusinessUnitId { get; set; }

    /// <summary>Company vs Individual — controls document / signatory rules. Set at initiation, read-only to the landlord.</summary>
    public LandlordType Type { get; set; }

    // ---- Prefilled, read-only Property Information (snapshot of the dbo.Property view at initiation) ----
    /// <summary>= <c>dbo.Property.Id</c> of the property the registration was initiated from.</summary>
    public int PropertyId { get; set; }
    [MaxLength(80)] public string PropertyCode { get; set; } = string.Empty;
    [MaxLength(200)] public string? CompanyName { get; set; }
    [MaxLength(250)] public string? Building { get; set; }
    /// <summary>The invitation destination — the Property-View Email column. Never changed by the landlord.</summary>
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(50)] public string? LandLordCode { get; set; }

    public SigningRule SigningRule { get; set; } = SigningRule.All;

    public LandlordStatus Status { get; set; } = LandlordStatus.Draft;

    /// <summary>True while the secure link may be used; set false on completion / rejection / restart.</summary>
    public bool LinkActive { get; set; } = true;

    /// <summary>Reference issued on first submission, e.g. <c>DIP-LR-2026-104882</c>.</summary>
    [MaxLength(30)] public string? Reference { get; set; }

    [MaxLength(100)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
    [MaxLength(100)] public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    [MaxLength(100)] public string? CompletedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    [MaxLength(100)] public string? RejectedBy { get; set; }
    [MaxLength(1000)] public string? RejectionReason { get; set; }

    /// <summary>Links this registration to the one it was restarted from (audit chain).</summary>
    public int? RestartedFromId { get; set; }

    /// <summary>If migrated from the legacy <c>dbo.LandlordData</c> table, its source Id — for traceability
    /// and to make the one-time data migration idempotent (re-runs skip already-migrated rows).</summary>
    public int? LegacySourceId { get; set; }

    public List<LandlordPremises> Premises { get; set; } = [];
    public List<LandlordContact> Contacts { get; set; } = [];
    public List<LandlordSignatory> Signatories { get; set; } = [];
    public List<LandlordDocument> Documents { get; set; } = [];
    public List<LandlordRegistrationComment> Comments { get; set; } = [];
}

/// <summary>A primary or secondary contact for the registration (FRD LandlordContact). Landlord-provided.</summary>
public class LandlordContact
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    public Landlord? Landlord { get; set; }

    public bool IsPrimary { get; set; }
    public ContactType ContactType { get; set; }

    [MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [MaxLength(120)] public string? Designation { get; set; }
    [MaxLength(40)] public string? Mobile { get; set; }
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(50)] public string? EmiratesId { get; set; }
    /// <summary>Emirates ID expiry date, captured (or OCR-extracted) alongside the Emirates ID document.</summary>
    public DateTime? EmiratesIdExpiry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<LandlordDocument> Documents { get; set; } = [];
}

/// <summary>A Business-Centre review comment — a correction request or a rejection reason (FRD
/// LandlordRegistrationComment) — recorded against the registration and surfaced to the landlord.</summary>
public class LandlordRegistrationComment
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    public Landlord? Landlord { get; set; }

    public LandlordCommentType CommentType { get; set; }
    [MaxLength(2000)] public string Comment { get; set; } = string.Empty;
    [MaxLength(100)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>False once the landlord has addressed this correction (so the portal shows only the open one).</summary>
    public bool IsResolved { get; set; }
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

    /// <summary>The document content itself, Base64-encoded and stored in the table — no file is written to
    /// the server file system. Mapped as <c>varchar(max)</c>.</summary>
    public string? ContentBase64 { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string UploadedBy { get; set; } = string.Empty;
    /// <summary>Optional contact this document belongs to (e.g. a POA for a specific contact).</summary>
    public int? ContactId { get; set; }
    /// <summary>Optional signatory this document belongs to (e.g. an authorisation letter).</summary>
    public int? SignatoryId { get; set; }

    // Re-upload versioning (FRD): a replaced document is retained with IsCurrent = false for the audit history.
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
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

/// <summary>Immutable registration audit event / history row (FRD LandlordRegistrationHistory): every
/// significant action with the status transition, actor, and originating IP where available.</summary>
public class LandlordEvent
{
    public int Id { get; set; }
    public int LandlordId { get; set; }
    [MaxLength(60)] public string EventType { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Detail { get; set; }
    [MaxLength(40)] public string? OldStatus { get; set; }
    [MaxLength(40)] public string? NewStatus { get; set; }
    [MaxLength(60)] public string? Ip { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(150)] public string? By { get; set; }
    /// <summary>Actor kind — "BusinessCentre", "Landlord" or "System".</summary>
    [MaxLength(30)] public string? ByType { get; set; }
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

    // ---- UAE Pass identity (captured right after OTP, before the form opens) ----
    /// <summary>Random anti-forgery <c>state</c> issued when the UAE Pass round-trip starts; matched on callback.</summary>
    [MaxLength(64)] public string? UaePassState { get; set; }
    public DateTime? UaePassVerifiedAt { get; set; }
    /// <summary>UAE Pass subject / UUID of the authenticated person.</summary>
    [MaxLength(100)] public string? UaePassSub { get; set; }
    [MaxLength(200)] public string? UaePassFullName { get; set; }
    [MaxLength(200)] public string? UaePassEmail { get; set; }
    [MaxLength(40)] public string? UaePassMobile { get; set; }
    /// <summary>Emirates ID number (UAE Pass <c>idn</c>) where the profile scope exposes it.</summary>
    [MaxLength(50)] public string? UaePassEmiratesId { get; set; }
}
