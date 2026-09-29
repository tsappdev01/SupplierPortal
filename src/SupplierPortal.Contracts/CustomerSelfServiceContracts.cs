namespace SupplierPortal.Contracts;

// ---------------------------------------------------------------------------------------------------------
// Customer self-service wire contracts. These mirror the supplier ones, but the customer form is DYNAMIC
// (driven by the admin field/tab configuration), so field VALUES travel as FieldKey -> value dictionaries
// (one for the Account section, one for the Contact section) instead of a fixed DTO with named properties.
// That keeps the DMZ Partner Connect app free of any EF model: it renders purely from the field config and
// the value dictionaries. The internal API maps the dictionaries to/from the real Customer/Contact entities.
// ---------------------------------------------------------------------------------------------------------

/// <summary>One customer field's render configuration (mirrors the internal CustomerFieldConfig, minus the
/// internal-only BusinessControl / per-user edit list). CustomerControl drives editability in self-service.</summary>
public class CustomerFieldConfigDto
{
    public string FieldKey { get; set; } = "";
    public string DisplayLabel { get; set; } = "";
    /// <summary>Optional short help shown as a tooltip on the form when not blank.</summary>
    public string? HelpText { get; set; }
    /// <summary>"Account" or "Contact".</summary>
    public string Section { get; set; } = "";
    public string GroupName { get; set; } = "";
    public int Sequence { get; set; }
    /// <summary>text | lookup | date | yesno | phone.</summary>
    public string FieldKind { get; set; } = "text";
    /// <summary>Editable | Readonly | Lookup — how the field behaves for the customer.</summary>
    public string CustomerControl { get; set; } = "Editable";
    public bool IsVisible { get; set; } = true;
    public bool IsRequired { get; set; }
    /// <summary>Conditional (depends-on) rule: this field shows only when the controlling field equals the value.</summary>
    public string? ControllingFieldKey { get; set; }
    public string? ControllingValue { get; set; }
}

/// <summary>A customer wizard tab (title/order/visibility). Tabs sharing a Title merge into one step.</summary>
public class CustomerTabDto
{
    public string Section { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string Title { get; set; } = "";
    public int Sequence { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class CustomerDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; }
    public long FileSizeBytes { get; set; }
}

/// <summary>A KYC document type the customer may upload (name + requirement note + mandatory flag).</summary>
public class CustomerDocumentTypeDto
{
    public string Name { get; set; } = "";
    public string? Requirement { get; set; }
    public bool IsRequired { get; set; }
    public int Sequence { get; set; }
}

/// <summary>Everything the customer self-service form needs to render, in one round-trip.</summary>
public class CustomerFormBootstrapDto
{
    public int CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public string? LegalName { get; set; }
    public string? Status { get; set; }
    /// <summary>Account-section field values, keyed by FieldKey.</summary>
    public Dictionary<string, string?> AccountValues { get; set; } = new();
    /// <summary>Contact-section field values, keyed by FieldKey.</summary>
    public Dictionary<string, string?> ContactValues { get; set; } = new();
    public List<CustomerFieldConfigDto> FieldConfigs { get; set; } = [];
    public List<CustomerTabDto> Tabs { get; set; } = [];
    public List<CustomerDocumentDto> Documents { get; set; } = [];
    /// <summary>The active document types the customer may upload.</summary>
    public List<CustomerDocumentTypeDto> DocumentTypes { get; set; } = [];
    /// <summary>Business-unit logo (data URI or hosted URL) for branding the form.</summary>
    public string? LogoUrl { get; set; }
}

public class SaveCustomerRequest
{
    public Dictionary<string, string?> AccountValues { get; set; } = new();
    public Dictionary<string, string?> ContactValues { get; set; } = new();
}

public class SaveCustomerResponse
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>OTP verification result for a customer link (mints the customer session token on success).</summary>
public class CustomerOtpVerifyResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SessionToken { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? CustomerName { get; set; }
    public string? ContactEmail { get; set; }
}

/// <summary>Final submit: records the signed declaration and moves the customer to the review/complete state,
/// which triggers the initiator notification + customer thank-you email and blocks the link (server-side).</summary>
public class SubmitCustomerRequest
{
    public bool Acknowledged { get; set; }
    public string? SignatureName { get; set; }
    /// <summary>Signature image as a base64 PNG (without the data-URI prefix), or null.</summary>
    public string? SignatureImageBase64 { get; set; }
}

public class SubmitCustomerResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class CustomerDocumentUploadResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<CustomerDocumentDto> Documents { get; set; } = [];
}
