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

    [Required(ErrorMessage = "Business E-Mail is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Supplier Type is required.")]
    public int? SupplierTypeId { get; set; }

    public int? TaxpayerCountryId { get; set; }

    // Tax Registration Number and Trade License Number are retained on the entity and Excel import
    // for historical data, but are no longer collected on the registration form (removed from the
    // UI), so they are no longer required here.
    public string TaxRegistrationNumber { get; set; } = "";

    public string TradeLicenseNumber { get; set; } = "";

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
    public int? CountryNameId { get; set; }
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
        Email = s.Email ?? "",
        SupplierTypeId = s.SupplierTypeId,
        TaxpayerCountryId = s.TaxpayerCountryId,
        TaxRegistrationNumber = s.TaxRegistrationNumber,
        TradeLicenseNumber = s.TradeLicenseNumber ?? "",
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
        CountryNameId = s.CountryNameId,
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
        s.Email = Email;
        s.SupplierTypeId = SupplierTypeId;
        s.TaxpayerCountryId = TaxpayerCountryId;
        s.TaxRegistrationNumber = TaxRegistrationNumber;
        s.TradeLicenseNumber = TradeLicenseNumber;
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
        s.CountryNameId = CountryNameId;
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
