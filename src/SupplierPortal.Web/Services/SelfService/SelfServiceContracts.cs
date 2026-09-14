using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services.SelfService;

// Request/response shapes for the internal Self-Service API and its DMZ HTTP-client gateways.
// Kept deliberately small and free of EF navigation graphs so they serialize cleanly over HTTP.

public record SsTokenRequest(string Token);
public record SsOtpVerifyRequest(string Token, string Code);
public record SsSessionRequest(string Token, string Session);

public record SsResult(bool Success, string? Error);
public record SsVerifyResult(bool Success, string? Error, string? SessionToken);
/// <summary>Save result carrying the reloaded record so the form can sync database-generated child ids.</summary>
public record SsSaveResult(bool Success, string? Error, Supplier? Saved);

/// <summary>Everything the self-service form needs to render, returned in one call so the DMZ makes a
/// single round-trip instead of ~20. Lists mirror the fields the shared SupplierForm loads.</summary>
public class SsFormData
{
    public Supplier Supplier { get; set; } = default!;
    public string? LogoSrc { get; set; }

    public List<LookupValue> TaxOrgTypes { get; set; } = [];
    public List<LookupValue> SupplierTypes { get; set; } = [];
    public List<LookupValue> Countries { get; set; } = [];
    public List<LookupValue> BusinessRelationships { get; set; } = [];
    public List<LookupValue> Provinces { get; set; } = [];
    public List<LookupValue> VatCodes { get; set; } = [];
    public List<LookupValue> PaymentMethods { get; set; } = [];
    public List<LookupValue> PaymentTerms { get; set; } = [];
    public List<LookupValue> Prefixes { get; set; } = [];
    public List<LookupValue> CommunicationMethods { get; set; } = [];
    public List<LookupValue> Currencies { get; set; } = [];
    public List<LookupValue> VatRegistrationTypes { get; set; } = [];
    public List<LookupValue> BusinessPartnerTypes { get; set; } = [];
    public List<LookupValue> BusinessLocationTypes { get; set; } = [];
    public List<LookupValue> EInvoicingWaves { get; set; } = [];
    public List<LookupValue> LicensingAuthorities { get; set; } = [];

    public List<Bank> Banks { get; set; } = [];
    public List<BusinessUnit> BusinessUnits { get; set; } = [];
    public List<SupplierFieldSetting> FieldSettings { get; set; } = [];
    public List<SupplierTabSetting> TabSettings { get; set; } = [];
    public List<SupplierDocument> Documents { get; set; } = [];
    public List<string> ChangedFieldKeys { get; set; } = [];
}

public record SsUploadRequest(string Token, string Session, int DocumentType, string FileName, string ContentType, string BytesBase64);
public record SsUploadResult(bool Success, string? Error, string? ExtractedNumber, string? DunsNumber);
public record SsDocumentRequest(string Token, string Session, int DocumentId);
public record SsFileContent(string BytesBase64, string ContentType, string FileName);
public record SsSubmitRequest(string Token, string Session, Supplier Supplier, bool Complete);
