using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services.SelfService;

// Customer self-service request/response shapes (mirrors the supplier ones; customer document types are
// strings, and lookups are code-based so the bundle carries only the record, field config and documents).

public record SsCustomerLinkInfo(bool Valid, string? CustomerName, int? BusinessUnitId, string? LogoSrc, bool NeedsFreshOtp);

/// <summary>Everything the customer self-service form needs to render, in one round-trip.</summary>
public class SsCustomerFormData
{
    public Customer Customer { get; set; } = default!;
    public string? LogoSrc { get; set; }
    public List<CustomerFieldConfig> Config { get; set; } = [];
    public List<CustomerDocument> Documents { get; set; } = [];
    public List<CustomerDocumentType> DocTypes { get; set; } = [];
}

public record SsCustomerSubmitRequest(string Token, string Session, Customer Customer, bool Complete);
public record SsCustomerSaveResult(bool Success, string? Error, Customer? Saved);
public record SsCustomerUploadRequest(string Token, string Session, string DocumentType, string FileName, string ContentType, string BytesBase64);
