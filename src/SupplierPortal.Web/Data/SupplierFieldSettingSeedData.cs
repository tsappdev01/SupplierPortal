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
        // Supplier Status (Active/Inactive) — an internal admin flag, not a supplier-facing form field. It is
        // listed here so an administrator can show/hide the Active/Inactive column on the Manage Supplier grid
        // from Settings > Supplier Fields (its "Visible" toggle / Remove drives the grid column).
        ("IsActive", "Supplier Status", 1, false),
        ("SupplierName", "Supplier Name", 1, true),
        ("TaxOrganizationTypeId", "Tax Organization Type", 1, true),
        ("SupplierTypeId", "Supplier Type", 1, true),
        ("TaxpayerCountryId", "Taxpayer Country", 1, false),
        ("TaxRegistrationNumber", "TRN/VAT Registration Number", 1, true),
        ("VatRegistered", "VAT Registered?", 1, false),
        ("VatRegistrationTypeId", "VAT Registration Type", 1, false),
        ("NumberOfVatGroupEntities", "Number of Entities if VAT Group", 1, false),
        ("VatGroupEntityNames", "Legal Names of all Entities under the VAT Group", 1, false),
        ("SupplierTin", "e-Invoicing TIN", 1, false),
        ("BusinessPartnerTypeId", "Business Partner Type", 1, false),
        ("BusinessLocationTypeId", "Business Location Type", 1, false),
        ("EInvoicingWaveId", "eINvoicing Wave Applicability", 1, false),
        ("PeppolId", "Peppol ID", 1, false),
        ("ApprovedServiceProviderId", "Approved Service Provider ID", 1, false),
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
        ("CorporateWebSite", "Corporate Web Site", 2, false),
        ("Email", "Business E-Mail", 2, true),
        ("BusinessPhone", "Business Contact Number", 2, false),
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

        // Step 4: Site / Business Unit
        ("SupplierSite", "Supplier Site", 4, false),
        ("ProcurementBuId", "Procurement BU", 4, false),
        ("ClientBuId", "Client BU", 4, false),
        ("BillToBuId", "Bill-to BU", 4, false),
        ("PaymentMethodId", "Payment Method", 4, false),
        ("PaymentTermsId", "Payment Terms", 4, false),
        // Site usages (checkboxes) — shown at the end of Step 4.
        ("IsRfqOrBidding", "RFQ or Bidding", 4, false),
        ("IsOrdering", "Ordering", 4, false),
        ("IsSourcingOnly", "Sourcing only", 4, false),
        ("IsPurchasing", "Purchasing", 4, false),
        ("IsPay", "Pay", 4, false),
        ("IsPrimaryPay", "Primary Pay", 4, false),

        // Step 5: Payment / Banking Information
        ("BankId", "Bank Name", 5, true),
        ("BankBranchId", "Branch Name", 5, true),
        ("AccountCountryCodeId", "Account Country Code", 5, false),
        ("AccountName", "Account Name", 5, false),
        ("AccountNumber", "Account Number", 5, false),
        ("AccountCurrencyCodeId", "Account Currency Code", 5, false),
        ("SwiftCode", "Swift Code / BIC Code", 5, false),
        ("Iban", "IBAN", 5, false),
        ("AccountStartDate", "Account Start Date", 5, false),
        ("RemitAdviceEmail", "Remit Advice Email", 5, false),
        ("AllowInternationalPayments", "Allow International Payments", 5, false),
        ("IntermediaryAccountApplicable", "Intermediary Account Applicable?", 5, false),
        ("IntermediaryCountryId", "Intermediary Country Name", 5, false),
        ("IntermediaryBankName", "Intermediary Bank Name", 5, false),
        ("IntermediaryAccountNumber", "Intermediary Account Number", 5, false),
        ("IntermediaryBranchNumber", "Intermediary Branch Number", 5, false),
        ("IntermediaryIban", "Intermediary IBAN", 5, false),
        ("IntermediaryBicSwiftCode", "Intermediary BIC/Swift Code", 5, false),

        // Step 6: KYS (Know Your Supplier) due-diligence. These fields drive the KYS tab's labels and
        // questions; their text is admin-editable via Settings > Supplier Fields (KYS Field Labels).
        // The KYS layout is fixed, so only the label is honoured for these (not reorder/mandatory).
        // Identity of the Supplier
        ("KysLicensedActivities", "Licensed business activities (as per trade licence)", 6, false),
        ("KysIdNumber", "Emirates ID / Passport No. of the Person Authorized to Represent the Supplier", 6, false),
        ("KysIdExpiryDate", "ID expiry date", 6, false),
        ("KysSignatoryName", "Name of signatory", 6, false),
        ("KysRepPosition", "Job title", 6, false),
        // Address
        ("KysOperatingAddressDifferent", "Is the supplier's actual operating address different from the address provided in the trade license?", 6, false),
        ("KysActualOperatingAddress", "Please provide the supplier's actual operating address", 6, false),
        // Recent Changes in Business Circumstances
        ("KysAddressChangedTwice", "Has the registered address (as per trade license) changed more than twice in the last 12 months?", 6, false),
        ("KysAddressChangeReason", "Please provide reasons for the registered address change", 6, false),
        ("KysRepsChangedTwice", "Have the supplier's key employees or persons authorised to represent the supplier changed more than twice in the last 12 months?", 6, false),
        ("KysRepsChangeReason", "Please provide reasons for the change in key employees / authorised persons", 6, false),
        // Bank Account Details
        ("KysAccountHolderName", "Account Holder Name", 6, false),
        ("KysBankName", "Bank Name", 6, false),
        ("KysBankBranch", "Bank Branch", 6, false),
        ("KysBankCountryId", "Country", 6, false),
        ("KysAccountNumber", "Account Number", 6, false),
        ("KysIban", "IBAN", 6, false),
        ("KysSwiftBic", "SWIFT / BIC Code", 6, false),
        ("KysAccountCurrencyId", "Bank Account Currency", 6, false),
        ("KysPaymentToOtherParty", "Will payment ever be requested to a party other than the named account holder?", 6, false),
        ("KysAltAccountHolderName", "Account Holder Name (alternative beneficiary)", 6, false),
        ("KysAltBankName", "Bank Name (alternative beneficiary)", 6, false),
        ("KysAltBankBranch", "Bank Branch (alternative beneficiary)", 6, false),
        ("KysAltBankCountryId", "Country (alternative beneficiary)", 6, false),
        ("KysAltAccountNumber", "Account Number (alternative beneficiary)", 6, false),
        ("KysAltIban", "IBAN (alternative beneficiary)", 6, false),
        ("KysAltSwiftBic", "SWIFT / BIC Code (alternative beneficiary)", 6, false),
        ("KysAltAccountCurrencyId", "Bank Account Currency (alternative beneficiary)", 6, false),
        ("KysPaymentsElectronicOnly", "Do you confirm that all payments will be received by electronic means and not in cash?", 6, false),
        ("KysElectronicPaymentDetails", "Please provide further details", 6, false),
        // Supply and Pricing
        ("KysGoodsDescription", "Description of goods / services to be supplied to us", 6, false),
        ("KysCoveringActivity", "Which activity listed on the supplier's Trade License covers the goods or services being supplied?", 6, false),
        ("KysPricingBasisType", "Basis of pricing", 6, false),
        ("KysPricingBasisOther", "Please specify the basis of pricing", 6, false),
        ("KysOwnGoodsAtSupply", "Do you own the goods at the time of supply?", 6, false),
        ("KysLegalOwnerDetails", "Please provide details of the legal owner of the goods and explain your role in the supply.", 6, false),
        ("KysSupportingDocs", "What supporting documentation is available?", 6, false),
        ("KysSupportingDocsOther", "Please specify the supporting documentation available", 6, false),
        ("KysGoodsOriginCountryId", "Country of origin of the goods", 6, false),
        ("KysGoodsOrigin", "Manufacturer / source", 6, false),
        // Tax-compliance declarations
        ("KysFtaPenalty", "Has the business or any owner / manager been subject to an FTA penalty, or investigated or convicted for Tax Evasion, in the UAE or elsewhere?", 6, false),
        ("KysFtaPenaltyDetails", "If yes, please explain", 6, false)
    ];
}
