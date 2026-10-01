namespace SupplierPortal.Contracts;

/// <summary>Document categories, mirrored from the internal <c>SupplierDocumentType</c> enum so the
/// portal need not reference the data layer. Values must stay numerically aligned with the internal enum.</summary>
public enum SelfServiceDocumentType
{
    TradeLicense = 0,
    VatCertification = 1,
    Other = 2,
    BankConfirmationLetter = 3
}

public class LookupItemDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Code { get; set; }
    public string? PhoneCode { get; set; }
}

public class BankBranchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>One entity in a supplier's VAT Group (structured VAT-group table).</summary>
public class VatGroupEntityDto
{
    public int Id { get; set; }
    public string? BusinessLegalName { get; set; }
    public string? Address { get; set; }
    public int? EInvoicingWaveId { get; set; }
    public string? TrnNumber { get; set; }
    public string? TinNumber { get; set; }
    public int? BusinessLocationTypeId { get; set; }
}

public class BankDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<BankBranchDto> Branches { get; set; } = [];
}

public class BusinessUnitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    /// <summary>The BU's brand logo src (hosted LogoUrl, else uploaded data URI) so the self-service header
    /// shows the correct entity logo even on the form page's own circuit.</summary>
    public string? LogoUrl { get; set; }
}

/// <summary>Supplier-facing field rule (mandatory / visible / step) that drives the wizard's validation
/// and field hiding. The portal renders purely from these — it does not hard-code required fields.</summary>
public class FieldSettingDto
{
    public string FieldKey { get; set; } = "";
    public string DisplayLabel { get; set; } = "";
    public int Step { get; set; }
    public int Sequence { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsVisibleToSupplier { get; set; }
}

/// <summary>A wizard tab (step) as configured by the admin — its title and whether suppliers see it.</summary>
public class TabSettingDto
{
    public int Step { get; set; }
    public string Title { get; set; } = "";
    public bool IsVisible { get; set; }
}

public class SupplierDocumentDto
{
    public int Id { get; set; }
    public SelfServiceDocumentType DocumentType { get; set; }
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; }
    public long FileSizeBytes { get; set; }
}

/// <summary>One-shot payload the portal fetches after OTP verification: everything the wizard needs to
/// render and validate, so the form comes up in a single authenticated round-trip.</summary>
public class SupplierFormBootstrapDto
{
    public SupplierDto Supplier { get; set; } = new();
    public BusinessUnitDto? BusinessUnit { get; set; }
    /// <summary>All business units, for the Site / Procurement / Client / Bill-to dropdowns on step 4.</summary>
    public List<BusinessUnitDto> BusinessUnits { get; set; } = [];
    /// <summary>Lookup lists keyed by <c>LookupCategory</c> name (e.g. "SupplierType", "Country").</summary>
    public Dictionary<string, List<LookupItemDto>> Lookups { get; set; } = [];
    public List<BankDto> Banks { get; set; } = [];
    public List<FieldSettingDto> FieldSettings { get; set; } = [];
    /// <summary>Wizard tabs (title + visibility), so the self-service form shows the same tabs as the portal.</summary>
    public List<TabSettingDto> Tabs { get; set; } = [];
    public List<SupplierDocumentDto> Documents { get; set; } = [];
    /// <summary>Field keys the supplier has already edited but that are not yet acknowledged (drives highlight).</summary>
    public List<string> PendingEditedFieldKeys { get; set; } = [];
}

// -------- OTP / session --------

public class OtpRequestRequest
{
    public string LinkToken { get; set; } = "";

    /// <summary>When false (the default, an automatic send on page load), a still-valid unexpired code is
    /// reused instead of emailing a new one — so a reload does not generate another OTP. When true (the
    /// user clicked "Resend code"), a fresh code is always issued.</summary>
    public bool Resend { get; set; }

    /// <summary>Customer self-service only: the per-recipient link tag ("p" = Primary Email, "c" = contact
    /// Email) carried on the emailed link's <c>?r=</c> query, so the verification code is delivered to the same
    /// inbox that opened the link. Null for supplier/landlord (single recipient) and for legacy links.</summary>
    public string? R { get; set; }
}

public class OtpRequestResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? MaskedEmail { get; set; }
    public int ExpiresInMinutes { get; set; }
    /// <summary>ONLY populated when the API runs in a non-production environment, to allow automated
    /// end-to-end testing without a live mail server. Always null in Production.</summary>
    public string? DevOtp { get; set; }

    /// <summary>The supplier's business-unit logo image URL, so the public front-end can brand its
    /// header with the correct entity's logo instead of a generic one. Null when the unit has none.</summary>
    public string? LogoUrl { get; set; }
}

/// <summary>Non-sensitive branding for a link, resolvable even when the link/session has EXPIRED (so the
/// public front-end can brand an error screen and show a business-unit support email). Carries no OTP and
/// no supplier data beyond the branding contact.</summary>
public class LinkInfoResponse
{
    public bool Success { get; set; }
    public string? BusinessUnitName { get; set; }
    public string? BusinessUnitEmail { get; set; }
    public string? LogoUrl { get; set; }
}

public class OtpVerifyRequest
{
    public string LinkToken { get; set; } = "";
    public string Code { get; set; } = "";
}

public class OtpVerifyResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    /// <summary>Short-lived supplier-scoped session JWT to send as <c>X-Supplier-Session</c> on data calls.</summary>
    public string? SessionToken { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? SupplierName { get; set; }
    public string? ContactEmail { get; set; }
}

// -------- save / submit --------

public class SaveSupplierResponse
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = [];
    public SupplierDto? Supplier { get; set; }
    public List<string> PendingEditedFieldKeys { get; set; } = [];
}

/// <summary>The signed declaration a supplier accepts when submitting: the typed full-name signature, the
/// hand-drawn signature (PNG, base64, no data-URI prefix) and the full declaration wording shown.</summary>
public class SubmitRequest
{
    public string SignatureName { get; set; } = "";
    public string? SignatureImageBase64 { get; set; }
    public string DeclarationText { get; set; } = "";
}

public class SubmitResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class DocumentUploadResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    /// <summary>Number auto-extracted from an uploaded PDF (TRN / trade-license), to prefill the field.</summary>
    public string? ExtractedNumber { get; set; }
    /// <summary>D-U-N-S number auto-extracted from an uploaded Trade License, to prefill the field.</summary>
    public string? DunsNumber { get; set; }
    public List<SupplierDocumentDto> Documents { get; set; } = [];
}

/// <summary>Standard OAuth2 client-credentials token response (subset).</summary>
public class TokenResponse
{
    public string access_token { get; set; } = "";
    public string token_type { get; set; } = "Bearer";
    public int expires_in { get; set; }
}
