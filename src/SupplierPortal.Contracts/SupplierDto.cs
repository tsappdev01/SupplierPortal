namespace SupplierPortal.Contracts;

/// <summary>
/// The full editable supplier payload exchanged between the DMZ self-service portal and the internal
/// REST API. Mirrors the internal <c>SupplierFormModel</c> field-for-field, but is a plain,
/// dependency-free DTO so the DMZ portal never needs the EF entities or the data layer.
/// Nullable reference-id fields map to lookup rows; the portal only ever holds ids + free text.
/// </summary>
public class SupplierDto
{
    public int Id { get; set; }
    public string? SupplierNumber { get; set; }

    // -- Step 1: Business / Organization --
    public string SupplierName { get; set; } = "";
    public int? PrefixId { get; set; }
    public int? TaxOrganizationTypeId { get; set; }
    public string FirstName { get; set; } = "";
    public string Email { get; set; } = "";
    public int? SupplierTypeId { get; set; }
    public int? TaxpayerCountryId { get; set; }
    public string TaxRegistrationNumber { get; set; } = "";
    // Configurable Business/Organization fields (shown/mandatory via field settings). Names match
    // SupplierFormModel so DtoMapper's by-name copy carries them through the API automatically.
    public bool? VatRegistered { get; set; }
    public int? VatRegistrationTypeId { get; set; }
    public int? NumberOfVatGroupEntities { get; set; }
    public string? VatGroupEntityNames { get; set; }
    public string? SupplierTin { get; set; }
    public int? BusinessPartnerTypeId { get; set; }
    public int? BusinessLocationTypeId { get; set; }
    public int? EInvoicingWaveId { get; set; }
    public string TradeLicenseNumber { get; set; } = "";
    public DateTime? TradeLicenseIssueDate { get; set; }
    public DateTime? TradeLicenseExpiryDate { get; set; }
    public int? TradeLicenseIssuingBodyId { get; set; }
    public string? IssuingBodyIfOthers { get; set; }
    public int? VatCodeId { get; set; }
    public int? BusinessRelationshipId { get; set; }
    public bool IsOneTimeSupplier { get; set; }
    public string? DunsNumber { get; set; }
    public string? CorporateWebSite { get; set; }

    // -- Step 2: Address --
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public int? CityId { get; set; }
    public int? CountryId { get; set; }
    public string? PostalCode { get; set; }
    public int? CountryNameId { get; set; }
    public string? AddressName { get; set; }
    public string? AdministrativeContact { get; set; }
    public int? PhoneCountryCodeId { get; set; }
    public string? PhoneAreaCode { get; set; }
    public string? State { get; set; }
    public string? BusinessPhone { get; set; }
    public bool AddressPurposeOrdering { get; set; }
    public bool AddressPurposeRemitTo { get; set; }
    public bool AddressPurposeRfqBidding { get; set; }

    // Alternate "Address as per VAT Certificate" — captured only when the main address is NOT the same as
    // the Trade License / VAT Certificate. Names/types mirror SupplierFormModel so DtoMapper maps them.
    // Structured VAT Group entities (when VAT Registration Type is a VAT Group). Mapped explicitly by
    // DtoMapper (the reflection map skips it — the element type differs from SupplierFormModel's).
    public List<VatGroupEntityDto> VatGroupEntities { get; set; } = [];

    public bool? AddressSameAsTradeLicense { get; set; }
    public string? VatAddressLine1 { get; set; }
    public string? VatAddressLine2 { get; set; }
    public int? VatCityId { get; set; }
    public string? VatCityText { get; set; }
    public int? VatCountryId { get; set; }
    public string? VatPostalCode { get; set; }

    // -- Step 3: Contact person --
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public int? MobileCountryCodeId { get; set; }
    public string? MobileAreaCode { get; set; }
    public string? Mobile { get; set; }

    // -- Step 3: Payment / Banking --
    public int? BankId { get; set; }
    public int? BankBranchId { get; set; }
    public int? AccountCountryCodeId { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public int? AccountCurrencyCodeId { get; set; }
    public string? SwiftCode { get; set; }
    public bool AllowInternationalPayments { get; set; }
    public DateTime? AccountStartDate { get; set; }
    public string? Iban { get; set; }
    public string? PaymentMethodCode { get; set; }
    public string? RemitAdviceEmail { get; set; }

    // -- Step 4: Site / Business Unit --
    public string? RemittanceEmail { get; set; }
    public bool IsRfqOrBidding { get; set; }
    public bool IsOrdering { get; set; }
    public bool IsPay { get; set; }
    public string? Phone { get; set; }
    public int? SupplierSiteBuId { get; set; }
    public bool IsSourcingOnly { get; set; }
    public bool IsPurchasing { get; set; }
    public bool IsPrimaryPay { get; set; }
    public int? CommunicationMethodId { get; set; }
    public string? CommunicationEmail { get; set; }
    public string ContactEmail { get; set; } = "";
    public int? PaymentMethodId { get; set; }
    public int? PaymentTermsId { get; set; }
    public int? ProcurementBuId { get; set; }
    public int? ClientBuId { get; set; }
    public int? BillToBuId { get; set; }
}
