using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models.Customer;

/// <summary>Well-known CST (Customer) registration status names. Mirrors the Supplier workflow so the
/// Business Relation department can move a record from data-entry through review to completion.</summary>
public static class CustomerStatuses
{
    public const string NotSubmitted = "Not Submitted";
    public const string AwaitingCustomerResponse = "Awaiting Customer Response";
    public const string ForReview = "For Review";
    public const string Complete = "Completed";

    // Retired status — kept as a constant only so legacy records / status-colour switches still resolve.
    public const string Returned = "Returned";

    /// <summary>The active customer workflow statuses: Not Submitted → Awaiting Customer Response →
    /// For Review (customer submitted, initiator notified) → Completed (reviewed and finalised).</summary>
    public static readonly string[] All = [NotSubmitted, AwaitingCustomerResponse, ForReview, Complete];
}

/// <summary>A customer account (the company/organisation being onboarded). Field set generated from the CST
/// onboarding sheet (63 account fields). Owned by the Business Relation department. Each field's per-role
/// control (Business vs Customer) and visibility is governed by CustomerFieldConfig.</summary>
public class Customer
{
    public int Id { get; set; }

    // ---- scaffolding / workflow ----
    /// <summary>Baan Ref # — internal key from the source system (sheet: hidden on the form).</summary>
    [MaxLength(60)] public string? BaanRef { get; set; }
    /// <summary>Customer Unique Number / Customer Id (sheet: hidden internal key).</summary>
    [MaxLength(60)] public string? CustomerId { get; set; }

    /// <summary>Customer Code — a short human-readable code for the customer. Optional.</summary>
    [MaxLength(60)] public string? CustomerCode { get; set; }

    [MaxLength(40)] public string Status { get; set; } = CustomerStatuses.NotSubmitted;

    /// <summary>Work priority (P1..P4), mirroring the supplier workflow. Null = unset. Drives the dashboard
    /// priority filter/column and sort.</summary>
    [MaxLength(4)] public string? Priority { get; set; }

    public int? BusinessUnitId { get; set; }
    public BusinessUnit? BusinessUnit { get; set; }

    /// <summary>The internal user (from Manage Users) this customer is assigned to / owned by. Optional;
    /// set to null (unassigned) if the user is later removed.</summary>
    public int? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    /// <summary>Draft flag mirroring the supplier workflow: a record still being entered by staff. Defaults
    /// to true on create; cleared once the record is progressed. Only an internal (non-token) save may change it.</summary>
    public bool IsDraft { get; set; } = true;

    [MaxLength(150)] public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ---- Declaration / digital signature (captured on self-service submit; mirrors the supplier) ----
    /// <summary>True once the customer has signed the declaration on submit.</summary>
    public bool DisclaimerAcknowledged { get; set; }
    public DateTime? DisclaimerAcknowledgedAt { get; set; }
    [MaxLength(150)] public string? DisclaimerAcknowledgedBy { get; set; }
    /// <summary>The full declaration wording the customer acknowledged (kept for audit).</summary>
    public string? DisclaimerText { get; set; }
    /// <summary>The typed full name used as the digital signature.</summary>
    [MaxLength(200)] public string? DisclaimerSignature { get; set; }
    /// <summary>The hand-drawn signature PNG bytes, carried from the form to the server to render into the
    /// signed declaration PDF. Not stored on the customer row (the PDF is stored as a document instead).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public byte[]? DisclaimerSignatureImage { get; set; }

    // ---- account fields (generated from the sheet) ----
    [MaxLength(300)] public string? LegalName { get; set; }  // Legal Name [required]
    [MaxLength(300)] public string? CommercialName { get; set; }  // Commercial Name
    [MaxLength(300)] public string? AccountNameArabic { get; set; }  // Account Name Arabic
    [MaxLength(300)] public string? AccountType { get; set; }  // Account Type [LOV]
    [MaxLength(300)] public string? TypeOfCustomer { get; set; }  // Type Of Customer [LOV]
    [MaxLength(300)] public string? TypeOfOrganization { get; set; }  // Type of Organization [LOV]
    [MaxLength(300)] public string? VATStructure { get; set; }  // VAT Structure
    [MaxLength(300)] public string? NumberOfEntities { get; set; }  // Number of Entities
    [MaxLength(300)] public string? CustomerBuyerTIN { get; set; }  // Customer/Buyer TIN
    [MaxLength(300)] public string? EINvoicingWaveApplicability { get; set; }  // eINvoicing Wave Applicability [LOV]
    [MaxLength(300)] public string? BusinessActivity { get; set; }  // Business Activity
    [MaxLength(300)] public string? PrimaryPhone { get; set; }  // Primary Phone
    [MaxLength(300)] public string? PrimaryEmail { get; set; }  // Primary Email
    [MaxLength(300)] public string? SecondaryEmailAddress { get; set; }  // Secondary Email Address
    [MaxLength(300)] public string? SecondaryPhone { get; set; }  // Secondary Phone
    [MaxLength(300)] public string? URL { get; set; }  // URL
    [MaxLength(300)] public string? BuyerLegalRegistrationIdentifierType { get; set; }  // Buyer Legal Registration Identifier Type [LOV]
    public bool? TRNApplicable { get; set; }  // TRN Applicable (Yes/No)
    [MaxLength(300)] public string? TRNNumber { get; set; }  // TRN Number
    [MaxLength(300)] public string? TradeLicenseNumber { get; set; }  // Trade License Number
    [MaxLength(300)] public string? LicensingAuthorityName { get; set; }  // Licensing Authority Name
    [MaxLength(300)] public string? AuthorityCode { get; set; }  // Authority Code
    [MaxLength(100)] public string? BusinessLocationType { get; set; }  // Business Location Type (Mainland / Free Zone) — sheet col 26
    [MaxLength(300)] public string? TradeLicenseIssuingBody { get; set; }  // Trade License Issuing Body
    public DateTime? TradeLicenseIssueDate { get; set; }  // Trade License Issue Date
    public DateTime? TradeLicenseExpiryDate { get; set; }  // Trade License Expiry Date
    [MaxLength(300)] public string? IncorporationCountry { get; set; }  // Incorporation Country
    [MaxLength(300)] public string? PlaceOfIncorporation { get; set; }  // Place of Incorporation
    public DateTime? EstablishmentDate { get; set; }  // Establishment Date
    [MaxLength(300)] public string? LandlineMobile { get; set; }  // Landline Mobile
    public bool? PEP { get; set; }  // PEP(Yes/No)
    [MaxLength(300)] public string? CustomerBankAccountName { get; set; }  // Customer Bank Account Name
    [MaxLength(300)] public string? IBANNumber { get; set; }  // IBAN Number
    [MaxLength(300)] public string? Owner { get; set; }  // Owner
    [MaxLength(300)] public string? CustomerType { get; set; }  // Customer Type [LOV]
    public bool? PowerOfAttorney { get; set; }  // Power of Attorney - (Yes/No)
    [MaxLength(300)] public string? PowerOfAttorney2 { get; set; }  // Power of Attorney
    [MaxLength(300)] public string? CountryCode { get; set; }  // Country Code [LOV]
    [MaxLength(300)] public string? CountryName { get; set; }  // Country Name [LOV]
    [MaxLength(300)] public string? AddressLine1 { get; set; }  // Address Line 1
    [MaxLength(300)] public string? AddressLine2 { get; set; }  // Address Line 2
    [MaxLength(300)] public string? POBox { get; set; }  // P.O. Box
    [MaxLength(300)] public string? CountrySubdivision { get; set; }  // Country Subdivision [LOV]
    [MaxLength(300)] public string? EmirateCity { get; set; }  // Emirate/City
    [MaxLength(300)] public string? Country { get; set; }  // Country (Arabic)
    [MaxLength(300)] public string? AddressLine12 { get; set; }  // Address Line 1 (Arabic)
    [MaxLength(300)] public string? AddressLine22 { get; set; }  // Address Line 2 (Arabic)
    [MaxLength(300)] public string? POBox2 { get; set; }  // P.O. Box (Arabic)
    [MaxLength(300)] public string? Emirate { get; set; }  // Emirate (Arabic)
    [MaxLength(300)] public string? Owner1 { get; set; }  // Owner 1
    [MaxLength(300)] public string? Owner1Nationality { get; set; }  // Owner 1 – Nationality [LOV]
    [MaxLength(300)] public string? Owner1Passport { get; set; }  // Owner 1 – Passport
    [MaxLength(300)] public string? Owner1PassportIssueCountry { get; set; }  // Owner 1 - Passport Issue Country
    [MaxLength(300)] public string? Owner1PassportIssuePlaceCountry { get; set; }  // Owner 1 - Passport Issue Place/Country
    [MaxLength(300)] public string? Owner1SharePercentage { get; set; }  // Owner 1 – Share Percentage
    [MaxLength(300)] public string? Owner2 { get; set; }  // Owner 2
    [MaxLength(300)] public string? Owner2Nationality { get; set; }  // Owner 2 – Nationality [LOV]
    [MaxLength(300)] public string? Owner2Passport { get; set; }  // Owner 2 – Passport
    [MaxLength(300)] public string? Owner2PassportIssueCountry { get; set; }  // Owner 2 - Passport Issue Country
    [MaxLength(300)] public string? Owner2PassportIssuePlaceCountry { get; set; }  // Owner 2 - Passport Issue Place/Country
    [MaxLength(300)] public string? Owner2SharePercentage { get; set; }  // Owner 2 – Share Percentage
    [MaxLength(300)] public string? PaymentTerms { get; set; }  // Payment Terms [LOV]

    // ---- relationships ----
    public CustomerContact? Contact { get; set; }
    public List<CustomerDocument> Documents { get; set; } = [];

    /// <summary>The entities registered under the customer's VAT Group. Captured on the Business &amp;
    /// Organization tab only when VAT Structure is "VAT Group" (mirrors the supplier). Empty otherwise.</summary>
    public List<CustomerVatGroupEntity> VatGroupEntities { get; set; } = [];
}

/// <summary>One legal entity within a customer's VAT group. Captured on the Business &amp; Organization tab
/// when the customer's VAT Structure is "VAT Group" — the customer lists every entity covered by the group's
/// single TRN. Mirrors <see cref="SupplierPortal.Web.Models.SupplierVatGroupEntity"/>, but the wave and
/// location-type are stored as plain strings (the customer portal is string/LOV-driven, not LookupValue-based).</summary>
public class CustomerVatGroupEntity
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [MaxLength(300)] public string? BusinessLegalName { get; set; }

    [MaxLength(500)] public string? Address { get; set; }

    /// <summary>E-Invoicing Wave applicability (Wave 1 / Wave 2).</summary>
    [MaxLength(100)] public string? EInvoicingWave { get; set; }

    [MaxLength(100)] public string? TrnNumber { get; set; }

    [MaxLength(100)] public string? TinNumber { get; set; }

    /// <summary>Business Location Type (Mainland / Free Zone).</summary>
    [MaxLength(100)] public string? BusinessLocationType { get; set; }

    /// <summary>Preserves the row order the entities were entered in.</summary>
    public int SortOrder { get; set; }
}

/// <summary>The customer's single primary contact (individual). Field set generated from the CST onboarding
/// sheet (74 contact fields), including the UAE Pass registered emails/mobiles.</summary>
public class CustomerContact
{
    public int Id { get; set; }
    public int CustomerId { get; set; }          // FK -> Customer.Id
    public Customer? Customer { get; set; }

    // ---- contact fields (generated from the sheet) ----
    [MaxLength(300)] public string? Title { get; set; }  // Title
    [MaxLength(300)] public string? FirstName { get; set; }  // First Name
    [MaxLength(300)] public string? MiddleName { get; set; }  // Middle Name
    [MaxLength(300)] public string? LastName { get; set; }  // Last Name
    [MaxLength(300)] public string? FullName { get; set; }  // Full Name (Arabic)
    [MaxLength(300)] public string? MobileNumber { get; set; }  // Mobile Number
    [MaxLength(300)] public string? Email { get; set; }  // Email
    [MaxLength(300)] public string? Owner { get; set; }  // Owner
    [MaxLength(300)] public string? Account { get; set; }  // Account
    [MaxLength(300)] public string? Gender { get; set; }  // Gender
    [MaxLength(300)] public string? MaritalStatus { get; set; }  // Marital Status [LOV]
    [MaxLength(300)] public string? PreferredLanguage { get; set; }  // Preferred Language [LOV]
    [MaxLength(300)] public string? BuyingRole { get; set; }  // Buying Role [LOV]
    [MaxLength(300)] public string? SecondaryEmail { get; set; }  // Secondary Email
    [MaxLength(300)] public string? SecondaryPhoneNumber { get; set; }  // Secondary Phone Number
    [MaxLength(300)] public string? IBANNumber { get; set; }  // IBAN Number
    [MaxLength(300)] public string? ResidentialStatus { get; set; }  // Residential Status [LOV]
    [MaxLength(300)] public string? Nationality { get; set; }  // Nationality [LOV]
    [MaxLength(300)] public string? Country { get; set; }  // Country [LOV]
    [MaxLength(300)] public string? AddressLine1 { get; set; }  // Address Line 1
    [MaxLength(300)] public string? AddressLine2 { get; set; }  // Address Line 2
    [MaxLength(300)] public string? POBox { get; set; }  // P.O. Box
    [MaxLength(300)] public string? EmirateCity { get; set; }  // Emirate/City
    [MaxLength(300)] public string? Country2 { get; set; }  // Country (Arabic)
    [MaxLength(300)] public string? AddressLine12 { get; set; }  // Address Line 1 (Arabic)
    [MaxLength(300)] public string? AddressLine22 { get; set; }  // Address Line 2 (Arabic)
    [MaxLength(300)] public string? POBox2 { get; set; }  // P.O. Box (Arabic)
    [MaxLength(300)] public string? Emirates { get; set; }  // Emirates (Arabic)
    public bool? Passport { get; set; }  // Passport (Yes/No)
    [MaxLength(300)] public string? PassportNumber { get; set; }  // Passport Number
    public DateTime? PassportIssueDate { get; set; }  // Passport Issue Date
    public DateTime? PassportExpiryDate { get; set; }  // Passport Expiry Date
    [MaxLength(300)] public string? PassportIssueCountryCode { get; set; }  // Passport Issue Country Code
    [MaxLength(300)] public string? PassportIssuePlaceCountry { get; set; }  // Passport Issue Place/Country
    public bool? DualPassport { get; set; }  // Dual Passport?(Yes/No)
    [MaxLength(300)] public string? DualPassportNumber { get; set; }  // Dual Passport Number
    public DateTime? DualPassportIssueDate { get; set; }  // Dual Passport Issue Date
    public DateTime? DualPassportExpiryDate { get; set; }  // Dual Passport Expiry Date
    public DateTime? VISAExpiryDate { get; set; }  // VISA Expiry Date
    [MaxLength(300)] public string? VISANumber { get; set; }  // VISA Number
    [MaxLength(300)] public string? TypeOfID { get; set; }  // Type of ID (Driving License / National ID / Voters ID)
    [MaxLength(300)] public string? CountryGovtIDNumber { get; set; }  // Country Govt ID Number
    public DateTime? CountryGovtIDIssueDate { get; set; }  // Country Govt ID Issue Date
    public DateTime? CountryGovtIDExpiryDate { get; set; }  // Country Govt ID Expiry Date
    [MaxLength(300)] public string? EmirateID { get; set; }  // Emirate ID
    public DateTime? EmirateIDExpiryDate { get; set; }  // Emirate ID Expiry Date
    public DateTime? EmiratesIDIssueDate { get; set; }  // Emirates ID Issue Date
    [MaxLength(300)] public string? IssueCountry { get; set; }  // Issue Country [LOV]
    [MaxLength(300)] public string? Occupation { get; set; }  // Occupation
    public DateTime? DateOfBirth { get; set; }  // Date of Birth
    [MaxLength(300)] public string? BirthPlace { get; set; }  // Birth Place
    [MaxLength(300)] public string? MothersName { get; set; }  // Mother’s Name
    [MaxLength(300)] public string? AliasName { get; set; }  // Alias Name
    [MaxLength(300)] public string? Nationality2 { get; set; }  // Nationality 2
    [MaxLength(300)] public string? TAXNumber { get; set; }  // TAX Number
    public bool? PEP { get; set; }  // PEP (Yes/No)
    [MaxLength(300)] public string? LandlineNumber { get; set; }  // Landline Number
    [MaxLength(300)] public string? EmployerName { get; set; }  // Employer Name
    [MaxLength(300)] public string? EmployerPhone { get; set; }  // Employer Phone
    [MaxLength(300)] public string? EmployerAddress { get; set; }  // Employer Address
    [MaxLength(300)] public string? SourceOfIncome { get; set; }  // Source Of Income
    public bool? ReceivedTradeLicense { get; set; }  // Received Trade License? (Yes/No)
    public bool? ReceivedVATCertificate { get; set; }  // Received VAT Certificate? (Yes/No)
    [MaxLength(300)] public string? TypeOfCustomer { get; set; }  // Type of Customer [LOV]
    [MaxLength(300)] public string? UAEPassRegisteredEmail1 { get; set; }  // UAEPass Registered Email 1
    [MaxLength(300)] public string? UAEPassRegisteredEmail2 { get; set; }  // UAEPass Registered Email 2
    [MaxLength(300)] public string? UAEPassRegisteredEmail3 { get; set; }  // UAEPass Registered Email 3
    [MaxLength(300)] public string? UAEPassRegisteredEmail4 { get; set; }  // UAEPass Registered Email 4
    [MaxLength(300)] public string? UAEPassMobileNo1 { get; set; }  // UAEPass Mobile No. 1
    [MaxLength(300)] public string? UAEPassMobileNo2 { get; set; }  // UAEPass Mobile No. 2
    [MaxLength(300)] public string? UAEPassMobileNo3 { get; set; }  // UAEPass Mobile No. 3
    [MaxLength(300)] public string? UAEPassMobileNo4 { get; set; }  // UAEPass Mobile No. 4
}

/// <summary>An uploaded supporting document for a customer registration (trade licence, VAT cert, EID, POA...).</summary>
public class CustomerDocument
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(100)] public string DocumentType { get; set; } = "";
    [MaxLength(260)] public string FileName { get; set; } = "";
    [MaxLength(150)] public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    /// <summary>Blob storage key when the file lives in Azure Blob Storage; then <see cref="Content"/> is
    /// empty. Null for older documents whose bytes are stored in <see cref="Content"/>.</summary>
    [MaxLength(400)] public string? BlobKey { get; set; }
    [MaxLength(150)] public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Control mode a field takes for a given role. Mirrors the onboarding sheet's vocabulary.</summary>
public static class CustomerFieldControls
{
    public const string Editable = "Editable";
    public const string Readonly = "Readonly";
    public const string Lookup = "Lookup";
    public static readonly string[] All = [Editable, Readonly, Lookup];
}

/// <summary>Per-field configuration for the CST portal, seeded from the onboarding sheet. For each customer
/// field it governs the control for the <b>Business</b> Relation department vs the <b>Customer</b>
/// (self-service) — Editable / Readonly / Lookup — plus whether the field is visible and required. Editing
/// this on the Field Settings screen changes how the forms render per role, with no code change.</summary>
public class CustomerFieldConfig
{
    public int Id { get; set; }

    /// <summary>The entity property this row governs (e.g. "LegalName"). Unique within a <see cref="Section"/>.</summary>
    [Required, MaxLength(100)] public string FieldKey { get; set; } = string.Empty;

    [Required, MaxLength(200)] public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>Optional short help/description. When not blank it is shown on the form as an info (ℹ) tooltip
    /// icon next to the field label.</summary>
    [MaxLength(500)] public string? HelpText { get; set; }

    /// <summary>Optional minimum number of characters for an editable text field (null = no minimum). Enforced
    /// on the customer forms with a Toast; ignored for non-text kinds (lookup / date / yes-no / phone).</summary>
    public int? MinLength { get; set; }

    /// <summary>Optional maximum number of characters for an editable text field (null = no maximum, capped by
    /// the underlying column). Enforced on the customer forms and as the input's maxlength.</summary>
    public int? MaxLength { get; set; }

    /// <summary>"Account" (the Customer entity) or "Contact" (the CustomerContact entity).</summary>
    [MaxLength(20)] public string Section { get; set; } = "Account";

    /// <summary>The wizard group/step this field belongs to (e.g. "Company Identity &amp; Classification").
    /// Fields are laid out as one chevron step per distinct group.</summary>
    [MaxLength(80)] public string GroupName { get; set; } = "";

    /// <summary>Display order within the section.</summary>
    public int Sequence { get; set; }

    /// <summary>How the field renders: text | lookup | date | yesno.</summary>
    [MaxLength(20)] public string FieldKind { get; set; } = "text";

    /// <summary>Control for the Business Relation department. See <see cref="CustomerFieldControls"/>.</summary>
    [MaxLength(20)] public string BusinessControl { get; set; } = CustomerFieldControls.Editable;

    /// <summary>Control for the Customer (self-service). See <see cref="CustomerFieldControls"/>.</summary>
    [MaxLength(20)] public string CustomerControl { get; set; } = CustomerFieldControls.Editable;

    public bool IsVisible { get; set; } = true;

    public bool IsRequired { get; set; }

    /// <summary>When true the field is removed from the form entirely (not shown to any role, never required)
    /// but kept so an admin can restore it. Mirrors the supplier field manager's remove/restore.</summary>
    public bool IsRemoved { get; set; }

    /// <summary>Optional conditional rule: the FieldKey of a controlling Yes/No field (in the same section).
    /// When set, this field is shown (and, if <see cref="IsRequired"/>, required) only while the controlling
    /// field's value equals <see cref="ControllingValue"/>; otherwise it is hidden and its value cleared.
    /// Null = always shown (the default).</summary>
    [MaxLength(100)] public string? ControllingFieldKey { get; set; }

    /// <summary>The controlling field's value that reveals this field: "true" (Yes) or "false" (No).</summary>
    [MaxLength(40)] public string? ControllingValue { get; set; }
}

/// <summary>Admin-configurable title, order, visibility and per-user edit restriction for one customer wizard
/// tab (a distinct <see cref="CustomerFieldConfig.GroupName"/> within a <see cref="CustomerFieldConfig.Section"/>).
/// Mirrors the supplier <c>SupplierTabSetting</c>. Seeded on first use from the groups present in the field
/// config. A hidden tab is skipped by both customer forms.</summary>
public class CustomerTabSetting
{
    public int Id { get; set; }

    /// <summary>"Account" (the Customer entity) or "Contact" (the CustomerContact entity).</summary>
    [Required, MaxLength(20)] public string Section { get; set; } = "Account";

    /// <summary>The group key this tab represents — matches <see cref="CustomerFieldConfig.GroupName"/>.
    /// Unique within a section.</summary>
    [Required, MaxLength(80)] public string GroupName { get; set; } = "";

    /// <summary>Display title (admin-editable). Defaults to the group name.</summary>
    [Required, MaxLength(150)] public string Title { get; set; } = string.Empty;

    /// <summary>Tab order within its section.</summary>
    public int Sequence { get; set; }

    public bool IsVisible { get; set; } = true;

    /// <summary>Comma-separated internal user Ids (<see cref="User.Id"/>) permitted to EDIT this tab on the
    /// internal customer form. Null/empty = no restriction; otherwise all other internal users see it
    /// read-only. Does not affect the customer self-service portal.</summary>
    [MaxLength(1000)] public string? EditableByUserIdsCsv { get; set; }
}
