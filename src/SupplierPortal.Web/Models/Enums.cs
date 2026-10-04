namespace SupplierPortal.Web.Models;

public enum UserType
{
    Administrator = 1,
    NormalUser = 2,
    LeasingManager = 3
}

/// <summary>Which business portals a user is allowed to open. Stored as a bit-flag column on the user and
/// emitted as claims at login; the home launcher, the sidebar and the portal pages all gate on it. This is a
/// separate dimension from <see cref="UserType"/> (the Admin/User role, which governs depth of activity and
/// access to the Settings area). An Administrator only sees the portals they are explicitly granted here.</summary>
[Flags]
public enum PortalAccess
{
    None = 0,
    Supplier = 1,
    Landlord = 2,
    Broker = 4,
    Customer = 8
}

/// <summary>A single business portal, used to resolve the portal-specific sender e-mail for a Business Unit
/// (EmailBrandingService.GetPortalSenderEmailAsync). Unlike the <see cref="PortalAccess"/> bit-flags (which
/// grant access), this names exactly one portal for sender resolution.</summary>
public enum PortalType
{
    Supplier = 1,
    Customer = 2,
    Landlord = 3,
    Broker = 4
}

/// <summary>How a user's credentials are verified at login. Local = password hash in this database
/// (used by the seeded break-glass admin); ActiveDirectory = validated against on-prem AD via LDAP.</summary>
public enum AuthSource
{
    Local = 0,
    ActiveDirectory = 1
}

public enum LookupCategory
{
    TaxOrganizationType = 1,
    SupplierType = 2,
    Country = 3,
    Province = 5,
    VatCode = 6,
    PaymentMethod = 7,
    PaymentTerms = 8,
    Prefix = 9,
    Currency = 10,
    BusinessRelationship = 11,
    CommunicationMethod = 12,
    SupplierStatus = 13,
    /// <summary>VAT structure: "VAT Group" / "Individual VAT" (used by the Supplier VAT Registration Type field).</summary>
    VatRegistrationType = 14,
    /// <summary>Business Partner Type: B2B / B2G.</summary>
    BusinessPartnerType = 15,
    /// <summary>Business Location Type: Mainland / Free Zone.</summary>
    BusinessLocationType = 16,
    /// <summary>eInvoicing Wave Applicability: Wave 1 / Wave 2.</summary>
    EInvoicingWave = 17,
    /// <summary>Trade License Issuing Body / Licensing Authority (DET, ADDED, JAFZA, ... , Others).</summary>
    LicensingAuthority = 18
}

/// <summary>Well-known workflow status names (rows seeded into LookupValues under
/// <see cref="LookupCategory.SupplierStatus"/>). Kept as a single source of truth so the
/// status-transition code and the seed data can't drift apart. The display text is editable in the
/// LookupValues table after seeding without breaking transitions, which resolve by these names.</summary>
public static class SupplierStatuses
{
    // Constant names are kept stable (New/Complete) so transition code doesn't churn; only the
    // display text stored in LookupValues and shown in the UI is "Not Submitted"/"Completed".
    public const string New = "Not Submitted";
    public const string AwaitingSupplierResponse = "Awaiting Supplier Response";
    // "For Review" and "Rejected" are retained only so legacy records and any transition code that
    // still references them resolve. They are no longer part of the active workflow: a supplier
    // submission auto-completes the record (see SupplierService.UpdateAsync) and there is no reject
    // step, so both are excluded from All / filters / stats.
    public const string ForReview = "For Review";
    public const string Complete = "Completed";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [New, AwaitingSupplierResponse, Complete];
}
