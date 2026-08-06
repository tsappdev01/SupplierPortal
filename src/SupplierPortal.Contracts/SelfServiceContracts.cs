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

public class SupplierDocumentDto
{
    public int Id { get; set; }
    public SelfServiceDocumentType DocumentType { get; set; }
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; }
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
    public List<SupplierDocumentDto> Documents { get; set; } = [];
    /// <summary>Field keys the supplier has already edited but that are not yet acknowledged (drives highlight).</summary>
    public List<string> PendingEditedFieldKeys { get; set; } = [];
}

// -------- OTP / session --------

public class OtpRequestRequest
{
    public string LinkToken { get; set; } = "";
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
