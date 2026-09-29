namespace SupplierPortal.Contracts;

// ---------------------------------------------------------------------------------------------------------
// Landlord self-service wire contracts (mirror the customer contracts). The landlord form's primary-contact
// fields are admin-configurable (Landlord Field Manager), so their render metadata travels as field configs,
// but the contact/signatory values are a fixed shape and travel as typed DTOs. OTP request/verify + link-info
// reuse the shared self-service contracts.
// ---------------------------------------------------------------------------------------------------------

/// <summary>One landlord field's render configuration (mirrors LandlordFieldConfig; LandlordControl drives
/// editability in self-service).</summary>
public class LandlordFieldConfigDto
{
    public string FieldKey { get; set; } = "";
    public string DisplayLabel { get; set; } = "";
    public string? HelpText { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string Section { get; set; } = "";
    public string GroupName { get; set; } = "";
    public int Sequence { get; set; }
    public string FieldKind { get; set; } = "text";
    public string LandlordControl { get; set; } = "Editable";
    public bool IsVisible { get; set; } = true;
    public bool IsRequired { get; set; }
    public string? ControllingFieldKey { get; set; }
    public string? ControllingValue { get; set; }
}

public class LandlordTabDto
{
    public string Section { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string Title { get; set; } = "";
    public int Sequence { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>A landlord contact (primary or secondary).</summary>
public class LandlordContactDto
{
    public int? Id { get; set; }
    public bool IsPrimary { get; set; }
    public string ContactType { get; set; } = "Owner";
    public string FullName { get; set; } = "";
    public string? Designation { get; set; }
    public string? Mobile { get; set; }
    public string Email { get; set; } = "";
    public string? EmiratesId { get; set; }
    public string? EmiratesIdName { get; set; }
    public DateTime? EmiratesIdExpiry { get; set; }
}

/// <summary>An additional authorised signatory (contacts are auto-included as signatories server-side).</summary>
public class LandlordSignatoryDto
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Mobile { get; set; }
}

public class LandlordDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; }
    public long FileSizeBytes { get; set; }
}

/// <summary>A document type the landlord may upload (enum key + code + display name + mandatory flag).</summary>
public class LandlordDocumentTypeDto
{
    public int Key { get; set; }
    /// <summary>The stable enum name (e.g. "EmiratesId") — this is what a stored document's DocumentType
    /// carries and what the upload endpoint parses, so the form matches and uploads by this, not the label.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsRequired { get; set; }
    public int Sequence { get; set; }
}

/// <summary>Everything the landlord self-service form needs to render, in one round-trip.</summary>
public class LandlordFormBootstrapDto
{
    public int LandlordId { get; set; }
    public string? PropertyCode { get; set; }
    public string? CompanyName { get; set; }
    public string? Status { get; set; }
    public string SigningRule { get; set; } = "All";
    public List<LandlordContactDto> Contacts { get; set; } = [];
    public List<LandlordSignatoryDto> Signatories { get; set; } = [];
    public List<LandlordFieldConfigDto> FieldConfigs { get; set; } = [];
    public List<LandlordTabDto> Tabs { get; set; } = [];
    public List<LandlordDocumentDto> Documents { get; set; } = [];
    public List<LandlordDocumentTypeDto> DocumentTypes { get; set; } = [];
    public string? LogoUrl { get; set; }
    /// <summary>An open reviewer correction comment, when the record was returned for correction.</summary>
    public string? CorrectionComment { get; set; }
}

public class SaveLandlordRequest
{
    public string SigningRule { get; set; } = "All";
    public List<LandlordContactDto> Contacts { get; set; } = [];
    public List<LandlordSignatoryDto> Signatories { get; set; } = [];
}

public class SaveLandlordResponse
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class LandlordOtpVerifyResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SessionToken { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? LandlordName { get; set; }
    public string? ContactEmail { get; set; }
}

/// <summary>Final submit: records the declaration acknowledgement and moves the registration to review
/// (which triggers the initiator + landlord notifications and the summary PDF, server-side).</summary>
public class SubmitLandlordRequest
{
    public bool Acknowledged { get; set; }
}

public class SubmitLandlordResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class LandlordDocumentUploadResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<LandlordDocumentDto> Documents { get; set; } = [];
}
