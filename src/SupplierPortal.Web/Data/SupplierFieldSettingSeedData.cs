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
        ("PrefixId", "Prefix", 1, false),
        ("TaxOrganizationTypeId", "Tax Organization Type", 1, true),
        ("FirstName", "First Name", 1, true),
        ("Email", "Business E-Mail", 1, true),
        ("SupplierTypeId", "Supplier Type", 1, true),
        ("TaxpayerCountryId", "Taxpayer Country", 1, false),
        ("TaxRegistrationNumber", "Tax Registration Number", 1, true),
        ("TradeLicenseNumber", "Trade License Number", 1, true),
        ("VatCodeId", "VAT Code", 1, false),
        ("BusinessRelationshipId", "Business Relationship", 1, false),
        ("DunsNumber", "D-U-N-S Number", 1, false),
        ("CorporateWebSite", "Corporate Web Site", 1, false),
        ("IsOneTimeSupplier", "One-time supplier", 1, false),

        // Step 2: Contact Information
        ("AddressLine1", "Address Line 1", 2, false),
        ("AddressLine2", "Address Line 2", 2, false),
        ("CityId", "City", 2, true),
        ("State", "State", 2, false),
        ("ProvinceId", "Province", 2, true),
        ("CountryId", "Country", 2, false),
        ("CountryNameId", "Country Name", 2, false),
        ("PostalCode", "Postal Code", 2, false),
        ("AddressName", "Address Name", 2, false),
        ("AdministrativeContact", "Administrative Contact", 2, false),
        ("PhoneCountryCodeId", "Phone Country Code", 2, false),
        ("PhoneAreaCode", "Phone Area Code", 2, false),

        // Step 3: Payment / Banking Information
        ("BankId", "Bank Name", 3, true),
        ("BankBranchId", "Branch Name", 3, true),
        ("AccountCountryCodeId", "Account Country Code", 3, false),
        ("AccountName", "Account Name", 3, false),
        ("AccountNumber", "Account Number", 3, false),
        ("AccountCurrencyCodeId", "Account Currency Code", 3, false),
        ("SwiftCode", "Swift Code / BIC Code", 3, false),
        ("Iban", "IBAN", 3, false),
        ("AccountStartDate", "Account Start Date", 3, false),
        ("PaymentMethodCode", "Payment Method Code", 3, false),
        ("RemitAdviceEmail", "Remit Advice Email", 3, false),
        ("AllowInternationalPayments", "Allow International Payments", 3, false),

        // Step 4: Site / Business Unit
        ("SupplierSiteBuId", "Supplier Site", 4, false),
        ("ProcurementBuId", "Procurement BU", 4, false),
        ("ClientBuId", "Client BU", 4, false),
        ("BillToBuId", "Bill-to BU", 4, false),
        ("PaymentMethodId", "Payment Method", 4, false),
        ("PaymentTermsId", "Payment Terms", 4, false),
        ("CommunicationMethodId", "Communication Method", 4, false),
        ("CommunicationEmail", "Communication Email", 4, false),
        ("ContactEmail", "Contact Email", 4, true),
        ("Phone", "Phone", 4, false),
        ("RemittanceEmail", "Remittance E-mail", 4, false),
        // Site usages (checkboxes) — shown at the end of Step 4.
        ("IsRfqOrBidding", "RFQ or Bidding", 4, false),
        ("IsOrdering", "Ordering", 4, false),
        ("IsSourcingOnly", "Sourcing only", 4, false),
        ("IsPurchasing", "Purchasing", 4, false),
        ("IsPay", "Pay", 4, false),
        ("IsPrimaryPay", "Primary Pay", 4, false)
    ];
}
