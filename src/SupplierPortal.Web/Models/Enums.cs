namespace SupplierPortal.Web.Models;

public enum UserType
{
    Administrator = 1,
    NormalUser = 2
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
    SupplierStatus = 13
}

/// <summary>Well-known workflow status names (rows seeded into LookupValues under
/// <see cref="LookupCategory.SupplierStatus"/>). Kept as a single source of truth so the
/// status-transition code and the seed data can't drift apart. The display text is editable in the
/// LookupValues table after seeding without breaking transitions, which resolve by these names.</summary>
public static class SupplierStatuses
{
    // Constant names are kept stable (New/Complete) so transition code doesn't churn; only the
    // display text stored in LookupValues and shown in the UI is "Not Submitted"/"Completes".
    public const string New = "Not Submitted";
    public const string AwaitingSupplierResponse = "Awaiting Supplier Response";
    public const string ForReview = "For Review";
    public const string Complete = "Completes";

    public static readonly string[] All = [New, AwaitingSupplierResponse, ForReview, Complete];
}
