namespace SupplierPortal.Web.Data;

/// <summary>
/// Default Mandatory/Optional starting point for every admin-configurable supplier field, grouped
/// by registration wizard step. Administrators can change these via Settings &gt; Supplier Fields.
/// </summary>
public static class SupplierFieldSettingSeedData
{
    public static readonly (string FieldKey, string DisplayLabel, int Step, bool IsMandatory)[] Fields =
    [
        // Step 1: Business / Organization Information
        ("SupplierName", "Supplier Name", 1, true),
        ("TaxOrganizationTypeId", "Tax Organization Type", 1, true),
        ("Email", "Business E-Mail", 1, true),
        ("SupplierTypeId", "Supplier Type", 1, true),
        ("TaxpayerCountryId", "Taxpayer Country", 1, false),
        ("CountryNameId", "Taxpayer Country Name", 1, false),
        ("TaxRegistrationNumber", "Tax Registration Number", 1, true),
        ("VatRegistered", "VAT Registered?", 1, false),
        ("VatRegistrationTypeId", "VAT Registration Type", 1, false),
        ("NumberOfVatGroupEntities", "Number of Entities if VAT Group", 1, false),
        ("VatGroupEntityNames", "Legal Names of all Entities under the VAT Group", 1, false),
        ("SupplierTin", "Supplier TIN", 1, false),
        ("BusinessPartnerTypeId", "Business Partner Type", 1, false),
        ("BusinessLocationTypeId", "Business Location Type", 1, false),
        ("EInvoicingWaveId", "eINvoicing Wave Applicability", 1, false),
        ("TradeLicenseNumber", "Trade License Number", 1, true),
        ("TradeLicenseIssueDate", "Trade License Issue Date", 1, true),
        ("TradeLicenseExpiryDate", "Trade License Expiry Date", 1, true),
        ("TradeLicenseIssuingBodyId", "Trade License Issuing Body", 1, true),
        ("IssuingBodyIfOthers", "Issuing Body if Others", 1, false),
        ("VatCodeId", "VAT Code", 1, false),
        ("BusinessRelationshipId", "Business Relationship", 1, false),
        ("DunsNumber", "D-U-N-S Number", 1, false),
        ("IsOneTimeSupplier", "One-time supplier", 1, false),

        // Step 2: Address Information
        ("AddressName", "Address Name", 2, false),
        ("AddressLine1", "Address Line 1", 2, false),
        ("AddressLine2", "Address Line 2", 2, false),
        ("CityId", "City / Emirate", 2, true),
        ("CountryId", "Country", 2, false),
        ("PostalCode", "PO Box / Postal Code", 2, false),
        ("State", "State", 2, false),
        ("ProvinceId", "Province", 2, true),
        ("CorporateWebSite", "Corporate Web Site", 2, false),
        ("AddressPurposeOrdering", "Address Purpose — Ordering", 2, false),
        ("AddressPurposeRemitTo", "Address Purpose — Remit to", 2, false),
        ("AddressPurposeRfqBidding", "Address Purpose — RFQ or Bidding", 2, false),

        // Step 3: Contact Information
        ("PrefixId", "Prefix", 3, false),
        ("FirstName", "First Name", 3, true),
        ("MiddleName", "Middle Name", 3, false),
        ("LastName", "Last Name", 3, false),
        ("AdministrativeContact", "Administrative Contact", 3, false),
        ("ContactEmail", "Contacts E-Mail", 3, true),
        ("PhoneCountryCodeId", "Phone Country Code", 3, false),
        ("PhoneAreaCode", "Phone Area Code", 3, false),
        ("Phone", "Phone", 3, false),
        ("MobileCountryCodeId", "Mobile Country Code", 3, false),
        ("MobileAreaCode", "Mobile Area Code", 3, false),
        ("Mobile", "Mobile", 3, false),

        // Step 4: Payment / Banking Information
        ("BankId", "Bank Name", 4, true),
        ("BankBranchId", "Branch Name", 4, true),
        ("AccountCountryCodeId", "Account Country Code", 4, false),
        ("AccountName", "Account Name", 4, false),
        ("AccountNumber", "Account Number", 4, false),
        ("AccountCurrencyCodeId", "Account Currency Code", 4, false),
        ("SwiftCode", "Swift Code / BIC Code", 4, false),
        ("Iban", "IBAN", 4, false),
        ("AccountStartDate", "Account Start Date", 4, false),
        ("RemitAdviceEmail", "Remit Advice Email", 4, false),
        ("AllowInternationalPayments", "Allow International Payments", 4, false),
        ("IntermediaryAccountApplicable", "Intermediary Account Applicable?", 4, false),
        ("IntermediaryCountryId", "Intermediary Country Name", 4, false),
        ("IntermediaryBankName", "Intermediary Bank Name", 4, false),
        ("IntermediaryAccountNumber", "Intermediary Account Number", 4, false),
        ("IntermediaryBranchNumber", "Intermediary Branch Number", 4, false),
        ("IntermediaryIban", "Intermediary IBAN", 4, false),
        ("IntermediaryBicSwiftCode", "Intermediary BIC/Swift Code", 4, false),

        // Step 5: Site / Business Unit
        ("SupplierSite", "Supplier Site", 5, false),
        ("ProcurementBuId", "Procurement BU", 5, false),
        ("ClientBuId", "Client BU", 5, false),
        ("BillToBuId", "Bill-to BU", 5, false),
        ("PaymentMethodId", "Payment Method", 5, false),
        ("PaymentTermsId", "Payment Terms", 5, false),
        // Site usages (checkboxes) — shown at the end of Step 5.
        ("IsRfqOrBidding", "RFQ or Bidding", 5, false),
        ("IsOrdering", "Ordering", 5, false),
        ("IsSourcingOnly", "Sourcing only", 5, false),
        ("IsPurchasing", "Purchasing", 5, false),
        ("IsPay", "Pay", 5, false),
        ("IsPrimaryPay", "Primary Pay", 5, false)
    ];
}
