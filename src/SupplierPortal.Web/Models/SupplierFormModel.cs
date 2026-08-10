using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>
/// View-model for the 4-step supplier registration wizard, carrying the required-field rules from
/// the "Supplier Data" field specification. Deliberately separate from <see cref="Supplier"/>, whose
/// columns stay nullable so historical/bulk-imported data (which is often incomplete) remains valid.
/// </summary>
public class SupplierFormModel
{
    // -- Step 1: Business / Organization Information --
    public string? SupplierNumber { get; set; }

    [Required(ErrorMessage = "Supplier Name is required.")]
    public string SupplierName { get; set; } = "";

    public int? PrefixId { get; set; }

    [Required(ErrorMessage = "Tax Organization Type is required.")]
    public int? TaxOrganizationTypeId { get; set; }

    [Required(ErrorMessage = "First Name is required.")]
    public string FirstName { get; set; } = "";

    public string? MiddleName { get; set; }
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Business E-Mail is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Supplier Type is required.")]
    public int? SupplierTypeId { get; set; }

    public int? TaxpayerCountryId { get; set; }

    /// <summary>Taxpayer Country Name (Country LOV). Persisted via Supplier.CountryNameId.</summary>
    public int? CountryNameId { get; set; }

    [Required(ErrorMessage = "Tax Registration Number is required.")]
    public string TaxRegistrationNumber { get; set; } = "";

    public bool? VatRegistered { get; set; }
    public int? VatRegistrationTypeId { get; set; }
    public int? NumberOfVatGroupEntities { get; set; }
    public string? VatGroupEntityNames { get; set; }
    public string? SupplierTin { get; set; }
    public int? BusinessPartnerTypeId { get; set; }
    public int? BusinessLocationTypeId { get; set; }
    public int? EInvoicingWaveId { get; set; }

    [Required(ErrorMessage = "Trade License Number is required.")]
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

    // -- Step 2: Contact Information --
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }

    [Required(ErrorMessage = "City is required.")]
    public int? CityId { get; set; }

    public int? CountryId { get; set; } = null;
    public string? PostalCode { get; set; }
    public string? AddressName { get; set; }
    public string? AdministrativeContact { get; set; }
    public int? PhoneCountryCodeId { get; set; }
    public string? PhoneAreaCode { get; set; }
    public string? State { get; set; }

    [Required(ErrorMessage = "Province is required.")]
    public int? ProvinceId { get; set; }

    // -- Step 3: Payment / Banking Information --
    [Required(ErrorMessage = "Bank Name is required.")]
    public int? BankId { get; set; }

    [Required(ErrorMessage = "Branch Name is required.")]
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

    [EmailAddress(ErrorMessage = "Enter a valid Remit Advice Email.")]
    public string? RemitAdviceEmail { get; set; }

    // Intermediary bank account
    public bool IntermediaryAccountApplicable { get; set; }
    public int? IntermediaryCountryId { get; set; }
    public string? IntermediaryBankName { get; set; }
    public string? IntermediaryAccountNumber { get; set; }
    public string? IntermediaryBranchNumber { get; set; }
    public string? IntermediaryIban { get; set; }
    public string? IntermediaryBicSwiftCode { get; set; }

    // -- Step 4: Site / Business Unit --
    [EmailAddress(ErrorMessage = "Enter a valid Remittance E-mail.")]
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

    [EmailAddress(ErrorMessage = "Enter a valid Communication Email.")]
    public string? CommunicationEmail { get; set; }

    [Required(ErrorMessage = "Contact Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string ContactEmail { get; set; } = "";

    public int? PaymentMethodId { get; set; }
    public int? PaymentTermsId { get; set; }
    public int? ProcurementBuId { get; set; }
    public int? ClientBuId { get; set; }
    public int? BillToBuId { get; set; }

    public static SupplierFormModel FromSupplier(Supplier s) => new()
    {
        SupplierNumber = s.SupplierNumber,
        SupplierName = s.SupplierName,
        PrefixId = s.PrefixId,
        TaxOrganizationTypeId = s.TaxOrganizationTypeId,
        FirstName = s.FirstName ?? "",
        MiddleName = s.MiddleName,
        LastName = s.LastName,
        Email = s.Email ?? "",
        SupplierTypeId = s.SupplierTypeId,
        TaxpayerCountryId = s.TaxpayerCountryId,
        CountryNameId = s.CountryNameId,
        TaxRegistrationNumber = s.TaxRegistrationNumber,
        VatRegistered = s.VatRegistered,
        VatRegistrationTypeId = s.VatRegistrationTypeId,
        NumberOfVatGroupEntities = s.NumberOfVatGroupEntities,
        VatGroupEntityNames = s.VatGroupEntityNames,
        SupplierTin = s.SupplierTin,
        BusinessPartnerTypeId = s.BusinessPartnerTypeId,
        BusinessLocationTypeId = s.BusinessLocationTypeId,
        EInvoicingWaveId = s.EInvoicingWaveId,
        TradeLicenseNumber = s.TradeLicenseNumber ?? "",
        TradeLicenseIssueDate = s.TradeLicenseIssueDate,
        TradeLicenseExpiryDate = s.TradeLicenseExpiryDate,
        TradeLicenseIssuingBodyId = s.TradeLicenseIssuingBodyId,
        IssuingBodyIfOthers = s.IssuingBodyIfOthers,
        VatCodeId = s.VatCodeId,
        BusinessRelationshipId = s.BusinessRelationshipId,
        IsOneTimeSupplier = s.IsOneTimeSupplier,
        DunsNumber = s.DunsNumber,
        CorporateWebSite = s.CorporateWebSite,

        AddressLine1 = s.AddressLine1,
        AddressLine2 = s.AddressLine2,
        CityId = s.CityId,
        CountryId = s.CountryId,
        PostalCode = s.PostalCode,
        AddressName = s.AddressName,
        AdministrativeContact = s.AdministrativeContact,
        PhoneCountryCodeId = s.PhoneCountryCodeId,
        PhoneAreaCode = s.PhoneAreaCode,
        State = s.State,
        ProvinceId = s.ProvinceId,

        BankId = s.BankId,
        BankBranchId = s.BankBranchId,
        AccountCountryCodeId = s.AccountCountryCodeId,
        AccountName = s.AccountName,
        AccountNumber = s.AccountNumber,
        AccountCurrencyCodeId = s.AccountCurrencyCodeId,
        SwiftCode = s.SwiftCode,
        AllowInternationalPayments = s.AllowInternationalPayments,
        AccountStartDate = s.AccountStartDate,
        Iban = s.Iban,
        PaymentMethodCode = s.PaymentMethodCode,
        RemitAdviceEmail = s.RemitAdviceEmail,
        IntermediaryAccountApplicable = s.IntermediaryAccountApplicable,
        IntermediaryCountryId = s.IntermediaryCountryId,
        IntermediaryBankName = s.IntermediaryBankName,
        IntermediaryAccountNumber = s.IntermediaryAccountNumber,
        IntermediaryBranchNumber = s.IntermediaryBranchNumber,
        IntermediaryIban = s.IntermediaryIban,
        IntermediaryBicSwiftCode = s.IntermediaryBicSwiftCode,

        RemittanceEmail = s.RemittanceEmail,
        IsRfqOrBidding = s.IsRfqOrBidding,
        IsOrdering = s.IsOrdering,
        IsPay = s.IsPay,
        Phone = s.Phone,
        SupplierSiteBuId = s.SupplierSiteBuId,
        IsSourcingOnly = s.IsSourcingOnly,
        IsPurchasing = s.IsPurchasing,
        IsPrimaryPay = s.IsPrimaryPay,
        CommunicationMethodId = s.CommunicationMethodId,
        CommunicationEmail = s.CommunicationEmail,
        ContactEmail = s.ContactEmail ?? "",
        PaymentMethodId = s.PaymentMethodId,
        PaymentTermsId = s.PaymentTermsId,
        ProcurementBuId = s.ProcurementBuId,
        ClientBuId = s.ClientBuId,
        BillToBuId = s.BillToBuId
    };

    public void ApplyTo(Supplier s)
    {
        s.SupplierName = SupplierName;
        s.PrefixId = PrefixId;
        s.TaxOrganizationTypeId = TaxOrganizationTypeId;
        s.FirstName = FirstName;
        s.MiddleName = MiddleName;
        s.LastName = LastName;
        s.Email = Email;
        s.SupplierTypeId = SupplierTypeId;
        s.TaxpayerCountryId = TaxpayerCountryId;
        s.CountryNameId = CountryNameId;
        s.TaxRegistrationNumber = TaxRegistrationNumber;
        s.VatRegistered = VatRegistered;
        s.VatRegistrationTypeId = VatRegistrationTypeId;
        s.NumberOfVatGroupEntities = NumberOfVatGroupEntities;
        s.VatGroupEntityNames = VatGroupEntityNames;
        s.SupplierTin = SupplierTin;
        s.BusinessPartnerTypeId = BusinessPartnerTypeId;
        s.BusinessLocationTypeId = BusinessLocationTypeId;
        s.EInvoicingWaveId = EInvoicingWaveId;
        s.TradeLicenseNumber = TradeLicenseNumber;
        s.TradeLicenseIssueDate = TradeLicenseIssueDate;
        s.TradeLicenseExpiryDate = TradeLicenseExpiryDate;
        s.TradeLicenseIssuingBodyId = TradeLicenseIssuingBodyId;
        s.IssuingBodyIfOthers = IssuingBodyIfOthers;
        s.VatCodeId = VatCodeId;
        s.BusinessRelationshipId = BusinessRelationshipId;
        s.IsOneTimeSupplier = IsOneTimeSupplier;
        s.DunsNumber = DunsNumber;
        s.CorporateWebSite = CorporateWebSite;

        s.AddressLine1 = AddressLine1;
        s.AddressLine2 = AddressLine2;
        s.CityId = CityId;
        s.CountryId = CountryId;
        s.PostalCode = PostalCode;
        s.AddressName = AddressName;
        s.AdministrativeContact = string.IsNullOrWhiteSpace(AdministrativeContact) ? FirstName : AdministrativeContact;
        s.PhoneCountryCodeId = PhoneCountryCodeId;
        s.PhoneAreaCode = PhoneAreaCode;
        s.State = State;
        s.ProvinceId = ProvinceId;

        s.BankId = BankId;
        s.BankBranchId = BankBranchId;
        s.AccountCountryCodeId = AccountCountryCodeId;
        s.AccountName = AccountName;
        s.AccountNumber = AccountNumber;
        s.AccountCurrencyCodeId = AccountCurrencyCodeId;
        s.SwiftCode = SwiftCode;
        s.AllowInternationalPayments = AllowInternationalPayments;
        s.AccountStartDate = AccountStartDate;
        s.Iban = Iban;
        s.PaymentMethodCode = PaymentMethodCode;
        s.RemitAdviceEmail = RemitAdviceEmail;
        s.IntermediaryAccountApplicable = IntermediaryAccountApplicable;
        s.IntermediaryCountryId = IntermediaryCountryId;
        s.IntermediaryBankName = IntermediaryBankName;
        s.IntermediaryAccountNumber = IntermediaryAccountNumber;
        s.IntermediaryBranchNumber = IntermediaryBranchNumber;
        s.IntermediaryIban = IntermediaryIban;
        s.IntermediaryBicSwiftCode = IntermediaryBicSwiftCode;

        s.RemittanceEmail = RemittanceEmail;
        s.IsRfqOrBidding = IsRfqOrBidding;
        s.IsOrdering = IsOrdering;
        s.IsPay = IsPay;
        s.Phone = Phone;
        s.SupplierSiteBuId = SupplierSiteBuId;
        s.IsSourcingOnly = IsSourcingOnly;
        s.IsPurchasing = IsPurchasing;
        s.IsPrimaryPay = IsPrimaryPay;
        s.CommunicationMethodId = CommunicationMethodId;
        s.CommunicationEmail = CommunicationEmail;
        s.ContactEmail = ContactEmail;
        s.PaymentMethodId = PaymentMethodId;
        s.PaymentTermsId = PaymentTermsId;
        s.ProcurementBuId = ProcurementBuId;
        s.ClientBuId = ClientBuId;
        s.BillToBuId = BillToBuId;
    }
}
