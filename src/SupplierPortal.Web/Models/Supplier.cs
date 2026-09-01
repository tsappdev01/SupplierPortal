using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class Supplier
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string SupplierNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    public int? TaxOrganizationTypeId { get; set; }
    public LookupValue? TaxOrganizationType { get; set; }

    public int? SupplierTypeId { get; set; }
    public LookupValue? SupplierType { get; set; }

    public int? TaxpayerCountryId { get; set; }
    public LookupValue? TaxpayerCountry { get; set; }

    public int? CountryNameId { get; set; }
    public LookupValue? CountryName { get; set; }

    [Required, MaxLength(100)]
    public string TaxRegistrationNumber { get; set; } = string.Empty;

    // -- VAT details --

    /// <summary>Whether the supplier is VAT registered.</summary>
    public bool? VatRegistered { get; set; }

    /// <summary>VAT structure (LookupCategory.VatRegistrationType): "VAT Group" / "Individual VAT".</summary>
    public int? VatRegistrationTypeId { get; set; }
    public LookupValue? VatRegistrationType { get; set; }

    /// <summary>Number of entities when the VAT Registration Type is "VAT Group".</summary>
    public int? NumberOfVatGroupEntities { get; set; }

    /// <summary>Legal names of all entities under the VAT Group (free text, one per line).</summary>
    [MaxLength(2000)]
    public string? VatGroupEntityNames { get; set; }

    /// <summary>Supplier Tax Identification Number (distinct from the Tax Registration Number).</summary>
    [MaxLength(100)]
    public string? SupplierTin { get; set; }

    // -- Classification --

    /// <summary>Business Partner Type (LookupCategory.BusinessPartnerType): B2B / B2G.</summary>
    public int? BusinessPartnerTypeId { get; set; }
    public LookupValue? BusinessPartnerType { get; set; }

    /// <summary>Business Location Type (LookupCategory.BusinessLocationType): Mainland / Free Zone.</summary>
    public int? BusinessLocationTypeId { get; set; }
    public LookupValue? BusinessLocationType { get; set; }

    /// <summary>eInvoicing Wave Applicability (LookupCategory.EInvoicingWave): Wave 1 / Wave 2.</summary>
    public int? EInvoicingWaveId { get; set; }
    public LookupValue? EInvoicingWave { get; set; }

    [MaxLength(100)]
    public string? TradeLicenseNumber { get; set; }

    public DateTime? TradeLicenseIssueDate { get; set; }

    public DateTime? TradeLicenseExpiryDate { get; set; }

    /// <summary>Trade License Issuing Body / Licensing Authority (LookupCategory.LicensingAuthority).</summary>
    public int? TradeLicenseIssuingBodyId { get; set; }
    public LookupValue? TradeLicenseIssuingBody { get; set; }

    /// <summary>Free-text issuing body used when the Trade License Issuing Body is "Others".</summary>
    [MaxLength(200)]
    public string? IssuingBodyIfOthers { get; set; }

    /// <summary>Legal type / license type as printed on the trade license (e.g. "Limited Liability
    /// Company (LLC)"), captured from the license document.</summary>
    [MaxLength(150)]
    public string? LicenseType { get; set; }

    [MaxLength(50)]
    public string? DunsNumber { get; set; }

    [MaxLength(300)]
    public string? CorporateWebSite { get; set; }

    public int? BusinessRelationshipId { get; set; }
    public LookupValue? BusinessRelationship { get; set; }

    public bool IsOneTimeSupplier { get; set; }

    // -- Address --

    [MaxLength(200)]
    public string? AddressName { get; set; }

    [MaxLength(300)]
    public string? AddressLine1 { get; set; }

    [MaxLength(300)]
    public string? AddressLine2 { get; set; }

    /// <summary>The Emirate (LOV) when the country is the UAE — selected from the Emirates list.</summary>
    public int? CityId { get; set; }
    public LookupValue? City { get; set; }

    /// <summary>Free-text city, used when the country is not the UAE (the Emirate LOV then does not apply).</summary>
    [MaxLength(150)]
    public string? CityText { get; set; }

    [MaxLength(150)]
    public string? State { get; set; }

    public int? CountryId { get; set; }
    public LookupValue? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    // Address purpose (what this address is used for).
    public bool AddressPurposeOrdering { get; set; }
    public bool AddressPurposeRemitTo { get; set; }
    public bool AddressPurposeRfqBidding { get; set; }

    [MaxLength(256)]
    public string? RemittanceEmail { get; set; }

    // Business-level contact phone (captured on the Address tab), separate from the personal
    // contact phone in the Contact section.
    public int? BusinessPhoneCountryCodeId { get; set; }
    public LookupValue? BusinessPhoneCountryCode { get; set; }

    [MaxLength(10)]
    public string? BusinessPhoneAreaCode { get; set; }

    [MaxLength(50)]
    public string? BusinessPhone { get; set; }

    // -- Contact --

    public int? PrefixId { get; set; }
    public LookupValue? Prefix { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(200)]
    public string? AdministrativeContact { get; set; }

    [MaxLength(256)]
    public string? ContactEmail { get; set; }

    public int? PhoneCountryCodeId { get; set; }
    public LookupValue? PhoneCountryCode { get; set; }

    [MaxLength(10)]
    public string? PhoneAreaCode { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    // Mobile (contact) — country dialing code + area code + number.
    public int? MobileCountryCodeId { get; set; }
    public LookupValue? MobileCountryCode { get; set; }

    [MaxLength(10)]
    public string? MobileAreaCode { get; set; }

    [MaxLength(50)]
    public string? Mobile { get; set; }

    /// <summary>Additional contact people beyond the primary contact captured in the flat columns above.</summary>
    public List<SupplierContact> Contacts { get; set; } = new();

    /// <summary>Additional bank accounts beyond the primary account captured in the flat columns below.</summary>
    public List<SupplierBankAccount> BankAccounts { get; set; } = new();

    /// <summary>Legal entities within the supplier's VAT group (when VAT Registration Type = VAT Group).</summary>
    public List<SupplierVatGroupEntity> VatGroupEntities { get; set; } = new();

    // -- Business Unit & Site Assignment --

    /// <summary>Free-text supplier site name; defaults to the Address Name but the user may overwrite it.</summary>
    [MaxLength(200)]
    public string? SupplierSite { get; set; }

    /// <summary>Legacy Business-Unit reference for the supplier site (superseded by the free-text
    /// <see cref="SupplierSite"/>; retained so historical data is preserved).</summary>
    public int? SupplierSiteBuId { get; set; }
    public BusinessUnit? SupplierSiteBu { get; set; }

    public int? ProcurementBuId { get; set; }
    public BusinessUnit? ProcurementBu { get; set; }

    public int? ClientBuId { get; set; }
    public BusinessUnit? ClientBu { get; set; }

    public int? BillToBuId { get; set; }
    public BusinessUnit? BillToBu { get; set; }

    public int? CommunicationMethodId { get; set; }
    public LookupValue? CommunicationMethod { get; set; }

    [MaxLength(256)]
    public string? CommunicationEmail { get; set; }

    public bool IsRfqOrBidding { get; set; }
    public bool IsOrdering { get; set; }
    public bool IsSourcingOnly { get; set; }
    public bool IsPurchasing { get; set; }
    public bool IsPay { get; set; }
    public bool IsPrimaryPay { get; set; }

    // -- Payment / Banking --

    public int? PaymentMethodId { get; set; }
    public LookupValue? PaymentMethod { get; set; }

    public int? PaymentTermsId { get; set; }
    public LookupValue? PaymentTerms { get; set; }

    [MaxLength(50)]
    public string? PaymentMethodCode { get; set; }

    [MaxLength(256)]
    public string? RemitAdviceEmail { get; set; }

    public int? VatCodeId { get; set; }
    public LookupValue? VatCode { get; set; }

    public int? CurrencyId { get; set; }
    public LookupValue? Currency { get; set; }

    public int? BankId { get; set; }
    public Bank? Bank { get; set; }

    public int? BankBranchId { get; set; }
    public BankBranch? BankBranch { get; set; }

    public int? AccountCountryCodeId { get; set; }
    public LookupValue? AccountCountryCode { get; set; }

    [MaxLength(200)]
    public string? AccountName { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    public int? AccountCurrencyCodeId { get; set; }
    public LookupValue? AccountCurrencyCode { get; set; }

    [MaxLength(50)]
    public string? SwiftCode { get; set; }

    [MaxLength(50)]
    public string? Iban { get; set; }

    public DateTime? AccountStartDate { get; set; }

    /// <summary>Date the bank account is closed/ended. Visible to administrators only.</summary>
    public DateTime? AccountEndDate { get; set; }

    public bool AllowInternationalPayments { get; set; }

    // -- Intermediary bank account (used for international payments routed through a correspondent bank) --

    public bool IntermediaryAccountApplicable { get; set; }

    /// <summary>Intermediary bank country (LookupCategory.Country).</summary>
    public int? IntermediaryCountryId { get; set; }
    public LookupValue? IntermediaryCountry { get; set; }

    [MaxLength(200)]
    public string? IntermediaryBankName { get; set; }

    [MaxLength(50)]
    public string? IntermediaryAccountNumber { get; set; }

    [MaxLength(50)]
    public string? IntermediaryBranchNumber { get; set; }

    [MaxLength(50)]
    public string? IntermediaryIban { get; set; }

    [MaxLength(50)]
    public string? IntermediaryBicSwiftCode { get; set; }

    // -- Status / ownership --

    /// <summary>Workflow status (LookupValue in the SupplierStatus category): New →
    /// Awaiting Supplier Response → For Review → Complete. See <see cref="SupplierStatuses"/>.</summary>
    public int? StatusId { get; set; }
    public LookupValue? Status { get; set; }

    /// <summary>MDM review comments/remarks captured when a completed supplier is rejected and sent
    /// back for correction. Shown to the internal owner so they know what to fix before resubmitting.</summary>
    [MaxLength(2000)]
    public string? ReviewRemarks { get; set; }

    // -- Self-service disclaimer acknowledgement --

    /// <summary>Whether the supplier acknowledged the "all information is correct" disclaimer when
    /// submitting via the self-service portal. Recorded with the submission for audit.</summary>
    public bool DisclaimerAcknowledged { get; set; }

    /// <summary>When the disclaimer was acknowledged (UTC).</summary>
    public DateTime? DisclaimerAcknowledgedAt { get; set; }

    /// <summary>Who acknowledged the disclaimer (the self-service actor label / contact email).</summary>
    [MaxLength(256)]
    public string? DisclaimerAcknowledgedBy { get; set; }

    /// <summary>The exact disclaimer text the supplier agreed to (stored so the wording is preserved
    /// even if the on-screen text changes later).</summary>
    [MaxLength(4000)]
    public string? DisclaimerText { get; set; }

    /// <summary>The full name the supplier typed as their digital signature on the declaration.</summary>
    [MaxLength(200)]
    public string? DisclaimerSignature { get; set; }

    /// <summary>True while the registration wizard is still in progress (not yet Submitted for Approval).</summary>
    public bool IsDraft { get; set; } = true;

    public bool IsActive { get; set; } = true;

    [Required]
    public int BusinessUnitId { get; set; }
    public BusinessUnit? BusinessUnit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
