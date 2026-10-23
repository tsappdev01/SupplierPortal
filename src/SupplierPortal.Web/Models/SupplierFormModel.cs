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

    /// <summary>Internal triage priority: P1 / P2 / P3 / P4.</summary>
    public string? Priority { get; set; }

    /// <summary>Supplier active/inactive status (internal only). Shown on the Business/Organization tab;
    /// an inactive supplier is hidden from the default supplier list.</summary>
    public bool IsActive { get; set; } = true;

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
    public bool? AddressSameAsTradeLicense { get; set; }
    public string? VatAddressLine1 { get; set; }
    public string? VatAddressLine2 { get; set; }
    public int? VatCityId { get; set; }
    public string? VatCityText { get; set; }
    public int? VatCountryId { get; set; }
    public string? VatPostalCode { get; set; }

    [Required(ErrorMessage = "City is required.")]
    public int? CityId { get; set; }
    public string? CityText { get; set; }
    public bool AddressPurposeOrdering { get; set; }
    public bool AddressPurposeRemitTo { get; set; }
    public bool AddressPurposeRfqBidding { get; set; }

    public int? CountryId { get; set; } = null;
    public string? PostalCode { get; set; }
    public string? AddressName { get; set; }
    public string? AdministrativeContact { get; set; }
    public int? PhoneCountryCodeId { get; set; }
    public string? PhoneAreaCode { get; set; }
    public int? MobileCountryCodeId { get; set; }
    public string? MobileAreaCode { get; set; }
    public string? Mobile { get; set; }
    public string? State { get; set; }

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
    public DateTime? AccountEndDate { get; set; }
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

    // Business-level contact phone (Address tab).
    public int? BusinessPhoneCountryCodeId { get; set; }
    public string? BusinessPhoneAreaCode { get; set; }
    public string? BusinessPhone { get; set; }

    // -- Step 4: Site / Business Unit --
    [EmailAddress(ErrorMessage = "Enter a valid Remittance E-mail.")]
    public string? RemittanceEmail { get; set; }
    public bool IsRfqOrBidding { get; set; }
    public bool IsOrdering { get; set; }
    public bool IsPay { get; set; }
    public string? Phone { get; set; }
    public int? SupplierSiteBuId { get; set; }
    public string? SupplierSite { get; set; }
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

    /// <summary>Additional contacts beyond the primary contact (the flat FirstName/ContactEmail/… fields).</summary>
    public List<SupplierContactModel> AdditionalContacts { get; set; } = new();

    /// <summary>Additional bank accounts beyond the primary account (the flat BankId/AccountNumber/… fields).</summary>
    public List<SupplierBankAccountModel> AdditionalBankAccounts { get; set; } = new();

    /// <summary>VAT group entities (required when VAT Registration Type is "VAT Group").</summary>
    public List<VatGroupEntityModel> VatGroupEntities { get; set; } = new();

    public static SupplierFormModel FromSupplier(Supplier s) => new()
    {
        SupplierNumber = s.SupplierNumber,
        Priority = s.Priority,
        IsActive = s.IsActive,
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
        AdditionalContacts = s.Contacts
            .OrderBy(c => c.SortOrder)
            .Select(SupplierContactModel.FromEntity)
            .ToList(),
        AdditionalBankAccounts = s.BankAccounts
            .OrderBy(a => a.SortOrder)
            .Select(SupplierBankAccountModel.FromEntity)
            .ToList(),
        VatGroupEntities = s.VatGroupEntities
            .OrderBy(e => e.SortOrder)
            .Select(VatGroupEntityModel.FromEntity)
            .ToList(),
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
        AddressSameAsTradeLicense = s.AddressSameAsTradeLicense,
        VatAddressLine1 = s.VatAddressLine1,
        VatAddressLine2 = s.VatAddressLine2,
        VatCityId = s.VatCityId,
        VatCityText = s.VatCityText,
        VatCountryId = s.VatCountryId,
        VatPostalCode = s.VatPostalCode,
        CityId = s.CityId,
        CityText = s.CityText,
        AddressPurposeOrdering = s.AddressPurposeOrdering,
        AddressPurposeRemitTo = s.AddressPurposeRemitTo,
        AddressPurposeRfqBidding = s.AddressPurposeRfqBidding,
        CountryId = s.CountryId,
        PostalCode = s.PostalCode,
        AddressName = s.AddressName,
        AdministrativeContact = s.AdministrativeContact,
        PhoneCountryCodeId = s.PhoneCountryCodeId,
        PhoneAreaCode = s.PhoneAreaCode,
        MobileCountryCodeId = s.MobileCountryCodeId,
        MobileAreaCode = s.MobileAreaCode,
        Mobile = s.Mobile,
        State = s.State,

        BankId = s.BankId,
        BankBranchId = s.BankBranchId,
        AccountCountryCodeId = s.AccountCountryCodeId,
        AccountName = s.AccountName,
        AccountNumber = s.AccountNumber,
        AccountCurrencyCodeId = s.AccountCurrencyCodeId,
        SwiftCode = s.SwiftCode,
        AllowInternationalPayments = s.AllowInternationalPayments,
        AccountStartDate = s.AccountStartDate,
        AccountEndDate = s.AccountEndDate,
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
        BusinessPhoneCountryCodeId = s.BusinessPhoneCountryCodeId,
        BusinessPhoneAreaCode = s.BusinessPhoneAreaCode,
        BusinessPhone = s.BusinessPhone,
        IsRfqOrBidding = s.IsRfqOrBidding,
        IsOrdering = s.IsOrdering,
        IsPay = s.IsPay,
        Phone = s.Phone,
        SupplierSiteBuId = s.SupplierSiteBuId,
        SupplierSite = s.SupplierSite,
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
        s.Priority = Priority;
        s.IsActive = IsActive;
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
        s.AddressSameAsTradeLicense = AddressSameAsTradeLicense;
        // Only persist the alternate VAT-certificate address when the answer is "No"; clear it otherwise.
        var keepVat = AddressSameAsTradeLicense == false;
        s.VatAddressLine1 = keepVat ? VatAddressLine1 : null;
        s.VatAddressLine2 = keepVat ? VatAddressLine2 : null;
        s.VatCityId = keepVat ? VatCityId : null;
        s.VatCityText = keepVat ? VatCityText : null;
        s.VatCountryId = keepVat ? VatCountryId : null;
        s.VatPostalCode = keepVat ? VatPostalCode : null;
        s.CityId = CityId;
        s.CityText = CityText;
        s.AddressPurposeOrdering = AddressPurposeOrdering;
        s.AddressPurposeRemitTo = AddressPurposeRemitTo;
        s.AddressPurposeRfqBidding = AddressPurposeRfqBidding;
        s.CountryId = CountryId;
        s.PostalCode = PostalCode;
        s.AddressName = AddressName;
        s.AdministrativeContact = AdministrativeContact;
        s.PhoneCountryCodeId = PhoneCountryCodeId;
        s.PhoneAreaCode = PhoneAreaCode;
        s.MobileCountryCodeId = MobileCountryCodeId;
        s.MobileAreaCode = MobileAreaCode;
        s.Mobile = Mobile;
        s.State = State;

        s.BankId = BankId;
        s.BankBranchId = BankBranchId;
        s.AccountCountryCodeId = AccountCountryCodeId;
        s.AccountName = AccountName;
        s.AccountNumber = AccountNumber;
        s.AccountCurrencyCodeId = AccountCurrencyCodeId;
        s.SwiftCode = SwiftCode;
        s.AllowInternationalPayments = AllowInternationalPayments;
        s.AccountStartDate = AccountStartDate;
        s.AccountEndDate = AccountEndDate;
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
        s.BusinessPhoneCountryCodeId = BusinessPhoneCountryCodeId;
        s.BusinessPhoneAreaCode = BusinessPhoneAreaCode;
        s.BusinessPhone = BusinessPhone;
        s.IsRfqOrBidding = IsRfqOrBidding;
        s.IsOrdering = IsOrdering;
        s.IsPay = IsPay;
        s.Phone = Phone;
        s.SupplierSiteBuId = SupplierSiteBuId;
        // Supplier Site is a free-text field: store exactly what was entered (may be blank), no defaulting.
        s.SupplierSite = SupplierSite;
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

        ApplyContactsTo(s);
        ApplyBankAccountsTo(s);
        ApplyVatGroupEntitiesTo(s);
    }

    /// <summary>
    /// Reconciles the VAT group entities onto the supplier: updates rows still present, adds new ones,
    /// and drops those removed in the form. Blank rows are ignored. When the supplier is not a VAT
    /// Group all entities are cleared.
    /// </summary>
    private void ApplyVatGroupEntitiesTo(Supplier s)
    {
        var kept = VatGroupEntities.Where(e => !e.IsBlank).ToList();
        var keptIds = kept.Where(e => e.Id != 0).Select(e => e.Id).ToHashSet();

        s.VatGroupEntities.RemoveAll(existing => !keptIds.Contains(existing.Id));

        var order = 0;
        foreach (var model in kept)
        {
            var entity = model.Id != 0 ? s.VatGroupEntities.FirstOrDefault(e => e.Id == model.Id) : null;
            if (entity is null)
            {
                entity = new SupplierVatGroupEntity();
                s.VatGroupEntities.Add(entity);
            }

            entity.BusinessLegalName = model.BusinessLegalName;
            entity.Address = model.Address;
            entity.EInvoicingWaveId = model.EInvoicingWaveId;
            entity.TrnNumber = model.TrnNumber;
            entity.TinNumber = model.TinNumber;
            entity.BusinessLocationTypeId = model.BusinessLocationTypeId;
            entity.SortOrder = order++;
        }

        // Keep the legacy summary columns in sync with the structured rows (used by exports / round-trip).
        s.NumberOfVatGroupEntities = kept.Count > 0 ? kept.Count : null;
        s.VatGroupEntityNames = kept.Count > 0
            ? string.Join("\n", kept.Select(e => e.BusinessLegalName).Where(n => !string.IsNullOrWhiteSpace(n)))
            : null;
    }

    /// <summary>
    /// Reconciles the additional-contacts collection onto the supplier: updates rows still present,
    /// adds new ones, and drops those removed in the form. Blank contact cards are ignored so an empty
    /// "Add New Contact" row is never persisted.
    /// </summary>
    private void ApplyContactsTo(Supplier s)
    {
        var kept = AdditionalContacts.Where(c => !c.IsBlank).ToList();
        var keptIds = kept.Where(c => c.Id != 0).Select(c => c.Id).ToHashSet();

        s.Contacts.RemoveAll(existing => !keptIds.Contains(existing.Id));

        var order = 0;
        foreach (var model in kept)
        {
            var entity = model.Id != 0 ? s.Contacts.FirstOrDefault(c => c.Id == model.Id) : null;
            if (entity is null)
            {
                entity = new SupplierContact();
                s.Contacts.Add(entity);
            }

            entity.PrefixId = model.PrefixId;
            entity.FirstName = model.FirstName;
            entity.MiddleName = model.MiddleName;
            entity.LastName = model.LastName;
            entity.AdministrativeContact = model.AdministrativeContact;
            entity.ContactEmail = model.ContactEmail;
            entity.PhoneCountryCodeId = model.PhoneCountryCodeId;
            entity.PhoneAreaCode = model.PhoneAreaCode;
            entity.Phone = model.Phone;
            entity.MobileCountryCodeId = model.MobileCountryCodeId;
            entity.MobileAreaCode = model.MobileAreaCode;
            entity.Mobile = model.Mobile;
            entity.SortOrder = order++;
        }
    }

    /// <summary>
    /// Reconciles the additional-bank-account collection onto the supplier: updates rows still present,
    /// adds new ones, and drops those removed in the form. Blank cards are ignored.
    /// </summary>
    private void ApplyBankAccountsTo(Supplier s)
    {
        var kept = AdditionalBankAccounts.Where(a => !a.IsBlank).ToList();
        var keptIds = kept.Where(a => a.Id != 0).Select(a => a.Id).ToHashSet();

        s.BankAccounts.RemoveAll(existing => !keptIds.Contains(existing.Id));

        var order = 0;
        foreach (var model in kept)
        {
            var entity = model.Id != 0 ? s.BankAccounts.FirstOrDefault(a => a.Id == model.Id) : null;
            if (entity is null)
            {
                entity = new SupplierBankAccount();
                s.BankAccounts.Add(entity);
            }

            entity.BankId = model.BankId;
            entity.BankBranchId = model.BankBranchId;
            entity.AccountCountryCodeId = model.AccountCountryCodeId;
            entity.AccountName = model.AccountName;
            entity.AccountNumber = model.AccountNumber;
            entity.AccountCurrencyCodeId = model.AccountCurrencyCodeId;
            entity.SwiftCode = model.SwiftCode;
            entity.Iban = model.Iban;
            entity.AccountStartDate = model.AccountStartDate;
            entity.AccountEndDate = model.AccountEndDate;
            entity.SortOrder = order++;
        }
    }
}

/// <summary>Form view-model for a single additional supplier bank account.</summary>
public class SupplierBankAccountModel
{
    public int Id { get; set; }
    public int? BankId { get; set; }
    public int? BankBranchId { get; set; }
    public int? AccountCountryCodeId { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public int? AccountCurrencyCodeId { get; set; }
    public string? SwiftCode { get; set; }
    public string? Iban { get; set; }
    public DateTime? AccountStartDate { get; set; }
    public DateTime? AccountEndDate { get; set; }

    /// <summary>A bank-account card with no meaningful data entered — not persisted.</summary>
    public bool IsBlank =>
        Id == 0
        && BankId is null
        && BankBranchId is null
        && AccountCountryCodeId is null
        && string.IsNullOrWhiteSpace(AccountName)
        && string.IsNullOrWhiteSpace(AccountNumber)
        && AccountCurrencyCodeId is null
        && string.IsNullOrWhiteSpace(SwiftCode)
        && string.IsNullOrWhiteSpace(Iban)
        && AccountStartDate is null
        && AccountEndDate is null;

    public static SupplierBankAccountModel FromEntity(SupplierBankAccount a) => new()
    {
        Id = a.Id,
        BankId = a.BankId,
        BankBranchId = a.BankBranchId,
        AccountCountryCodeId = a.AccountCountryCodeId,
        AccountName = a.AccountName,
        AccountNumber = a.AccountNumber,
        AccountCurrencyCodeId = a.AccountCurrencyCodeId,
        SwiftCode = a.SwiftCode,
        Iban = a.Iban,
        AccountStartDate = a.AccountStartDate,
        AccountEndDate = a.AccountEndDate
    };
}

/// <summary>Form view-model for a single VAT group entity row.</summary>
public class VatGroupEntityModel
{
    public int Id { get; set; }
    public string? BusinessLegalName { get; set; }
    public string? Address { get; set; }
    public int? EInvoicingWaveId { get; set; }
    public string? TrnNumber { get; set; }
    public string? TinNumber { get; set; }
    public int? BusinessLocationTypeId { get; set; }

    /// <summary>A row with nothing entered — not persisted.</summary>
    public bool IsBlank =>
        Id == 0
        && string.IsNullOrWhiteSpace(BusinessLegalName)
        && string.IsNullOrWhiteSpace(Address)
        && EInvoicingWaveId is null
        && string.IsNullOrWhiteSpace(TrnNumber)
        && string.IsNullOrWhiteSpace(TinNumber)
        && BusinessLocationTypeId is null;

    /// <summary>True when every required column on the row is filled.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(BusinessLegalName)
        && !string.IsNullOrWhiteSpace(Address)
        && EInvoicingWaveId is not null
        && !string.IsNullOrWhiteSpace(TrnNumber)
        && !string.IsNullOrWhiteSpace(TinNumber)
        && BusinessLocationTypeId is not null;

    public static VatGroupEntityModel FromEntity(SupplierVatGroupEntity e) => new()
    {
        Id = e.Id,
        BusinessLegalName = e.BusinessLegalName,
        Address = e.Address,
        EInvoicingWaveId = e.EInvoicingWaveId,
        TrnNumber = e.TrnNumber,
        TinNumber = e.TinNumber,
        BusinessLocationTypeId = e.BusinessLocationTypeId
    };
}

/// <summary>Form view-model for a single additional supplier contact.</summary>
public class SupplierContactModel
{
    public int Id { get; set; }
    public int? PrefixId { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? AdministrativeContact { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid contact email address.")]
    public string? ContactEmail { get; set; }

    public int? PhoneCountryCodeId { get; set; }
    public string? PhoneAreaCode { get; set; }
    public string? Phone { get; set; }
    public int? MobileCountryCodeId { get; set; }
    public string? MobileAreaCode { get; set; }
    public string? Mobile { get; set; }

    /// <summary>A contact card with no meaningful data entered — not persisted.</summary>
    public bool IsBlank =>
        Id == 0
        && PrefixId is null
        && string.IsNullOrWhiteSpace(FirstName)
        && string.IsNullOrWhiteSpace(MiddleName)
        && string.IsNullOrWhiteSpace(LastName)
        && string.IsNullOrWhiteSpace(AdministrativeContact)
        && string.IsNullOrWhiteSpace(ContactEmail)
        // Country codes are auto-defaulted (UAE) by the phone control, so they don't count
        // towards "has the user entered anything" — only the actual numbers do.
        && string.IsNullOrWhiteSpace(PhoneAreaCode)
        && string.IsNullOrWhiteSpace(Phone)
        && string.IsNullOrWhiteSpace(MobileAreaCode)
        && string.IsNullOrWhiteSpace(Mobile);

    public static SupplierContactModel FromEntity(SupplierContact c) => new()
    {
        Id = c.Id,
        PrefixId = c.PrefixId,
        FirstName = c.FirstName,
        MiddleName = c.MiddleName,
        LastName = c.LastName,
        AdministrativeContact = c.AdministrativeContact,
        ContactEmail = c.ContactEmail,
        PhoneCountryCodeId = c.PhoneCountryCodeId,
        PhoneAreaCode = c.PhoneAreaCode,
        Phone = c.Phone,
        MobileCountryCodeId = c.MobileCountryCodeId,
        MobileAreaCode = c.MobileAreaCode,
        Mobile = c.Mobile
    };
}
