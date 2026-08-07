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

    [MaxLength(100)]
    public string? TradeLicenseNumber { get; set; }

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

    /// <summary>Selected from the same Emirates list as <see cref="Province"/>.</summary>
    public int? CityId { get; set; }
    public LookupValue? City { get; set; }

    [MaxLength(150)]
    public string? State { get; set; }

    public int? ProvinceId { get; set; }
    public LookupValue? Province { get; set; }

    public int? CountryId { get; set; }
    public LookupValue? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(256)]
    public string? RemittanceEmail { get; set; }

    // -- Contact --

    public int? PrefixId { get; set; }
    public LookupValue? Prefix { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

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

    // -- Business Unit & Site Assignment --

    /// <summary>Selected from the same Business Unit list as Procurement/Client/Bill-to BU.</summary>
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

    public bool AllowInternationalPayments { get; set; }

    // -- Status / ownership --

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
