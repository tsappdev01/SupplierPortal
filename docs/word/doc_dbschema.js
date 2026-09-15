const g = require("./docgen.js");
const { para, h1, h2, h3, bullet, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];
const W = [3400, 2100, 1900, 2100]; // Column, Type, Key/FK, Notes  (total 9500)

function ent(rows) {
  return table(["Column", "Type", "Key / FK", "Notes"], rows, W);
}

kids.push(...titlePage("Supplier Portal", "Database Schema & Relationships", [
  ["Document", "Database Schema & Entity Relationships"],
  ["Application", "Supplier Portal (.NET 8, EF Core 8.0.11, SQL Server / Azure SQL)"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Overview"));
kids.push(para("The Supplier Portal persists data in Microsoft SQL Server (on-premises) or Azure SQL Database, accessed through Entity Framework Core 8 using the DbContextFactory pattern. Two database contexts map the schema:"));
kids.push(bullet([g.t("ApplicationDbContext", { bold: true }), g.t(" — the supplier, customer, user and master-data schema (default "), g.t("dbo", { italics: true }), g.t(" schema).")]));
kids.push(bullet([g.t("LeasingDbContext", { bold: true }), g.t(" — the Landlord/Leasing bounded context ("), g.t("leasing", { italics: true }), g.t(" schema), with its own migration-history table so leasing tables never clash with the supplier schema.")]));
kids.push(para("Conventions: every entity has an identity integer primary key named Id unless stated. String lengths shown are the declared [MaxLength]. Foreign keys use ON DELETE RESTRICT unless noted (child collections cascade). Navigation properties are not columns.", { size: 18, color: g.colors.GREY }));

kids.push(h1("2. Master Data & Security (dbo)"));

kids.push(h2("2.1 BusinessUnit"));
kids.push(para("A Dubai Investments group entity / company. Suppliers, customers and users are scoped to business units."));
kids.push(ent([
  ["Id","int","PK",""],["Code","string(20)","","Unique index"],["Name","string(200)","",""],
  ["Domain","string(200)?","","Self-service host per BU"],["LogoUrl","string(500)?","",""],
  ["Logo / Thumbnail","byte[]?","","Branding images (DB)"],["LogoBlobKey / ThumbnailBlobKey","string(400)?","","Blob-stored branding"],
  ["LogoContentType / ThumbnailContentType","string(100)?","",""],["CreatedAt","DateTime","",""],
]));

kids.push(h2("2.2 Department"));
kids.push(ent([["Id","int","PK",""],["Code","string(30)","",""],["Name","string(150)","",""],["IsActive","bool","",""]]));

kids.push(h2("2.3 User"));
kids.push(para("An internal application user (staff). Authenticated locally, via Active Directory, or via Entra SSO."));
kids.push(ent([
  ["Id","int","PK",""],["Username","string(100)","",""],["FullName","string(200)?","",""],["Designation","string(150)?","",""],
  ["DepartmentId","int?","FK → Department","Optional"],["Email","string(256)","","Unique index"],
  ["ProfilePhoto","byte[]?","","or ProfilePhotoBlobKey"],["PasswordHash","string","",""],
  ["AuthSource","enum","","Local / ActiveDirectory"],["AdLogin","string(256)?","",""],["Subject","string(100)?","","SSO subject id"],
  ["UserType","enum","","Administrator / NormalUser / LeasingManager"],
  ["PortalAccess","enum [Flags]","","Supplier / Landlord / Broker / Customer"],
  ["CompanyBusinessUnitId","int?","FK → BusinessUnit","Restrict"],
  ["IsActive / IsDefaultAdmin / IsPendingRegistration","bool","",""],["CreatedAt","DateTime","",""],
]));
kids.push(para("Many-to-many: User ⇄ BusinessUnit through the join table UserBusinessUnits (composite PK UserId+BusinessUnitId, both FKs cascade).", { size: 18, color: g.colors.GREY }));

kids.push(h2("2.4 LookupValue (shared dropdown table)"));
kids.push(para("A single table holding every dropdown option, keyed by a Category enum. One row = one option in a category. Uniqueness is enforced by UNIQUE (Category, Code)."));
kids.push(ent([
  ["Id","int","PK",""],["Category","enum","","18 categories (see below)"],["Code","string(50)","","Unique per category"],
  ["DisplayName","string(200)","","Shown text"],["PhoneCode","string(10)?","","Country category only"],["IsActive","bool","","Default true"],
]));
kids.push(para("Categories: TaxOrganizationType, SupplierType, Country, Province, VatCode, PaymentMethod, PaymentTerms, Prefix, Currency, BusinessRelationship, CommunicationMethod, SupplierStatus, VatRegistrationType, BusinessPartnerType, BusinessLocationType, EInvoicingWave, LicensingAuthority.", { size: 18, color: g.colors.GREY }));

kids.push(h2("2.5 Bank & BankBranch"));
kids.push(ent([["Bank.Id","int","PK",""],["Bank.Name","string(200)","",""],["BankBranch.Id","int","PK",""],["BankBranch.BankId","int","FK → Bank","Cascade"],["BankBranch.Name","string(200)","",""]]));

kids.push(h2("2.6 AuditLog"));
kids.push(ent([
  ["Id","int","PK",""],["Timestamp","DateTime","",""],["ActorUsername","string(100)","",""],
  ["Action","enum","","Login/Logout/Create/Update/Activate/Deactivate/Delete/Notify"],
  ["EntityType","string(50)","",""],["EntityId","int?","","Loose reference"],["Details","string(500)?","",""],
]));

kids.push(h2("2.7 Configuration tables"));
kids.push(ent([
  ["SupplierFieldSetting","","","Per-field label, Step, Sequence, IsMandatory, IsVisibleToSupplier, IsRemoved. Unique FieldKey."],
  ["SupplierTabSetting","","","Wizard tab Step (unique), Title, IsVisible."],
  ["CustomerDocumentType","","","Configurable KYC checklist: Name, Requirement, IsRequired, Sequence, IsActive."],
  ["CustomerFieldConfig","","","Per-field: Section, GroupName, Sequence, FieldKind, Business/Customer control, IsVisible, IsRequired."],
]));

kids.push(h1("3. Supplier Domain (dbo)"));
kids.push(h2("3.1 Supplier"));
kids.push(para("The central supplier record (~150 columns). Columns are grouped below by section; primary-contact and one address are held as flat columns, with additional contacts, bank accounts and VAT-group entities as child tables."));
kids.push(table(["Group", "Representative columns", "Lookup / FK"],
 [["Identity","SupplierNumber, Priority, SupplierName, Email, IsDraft, IsActive, CreatedAt/By, UpdatedAt/By","BusinessUnitId → BusinessUnit (required)"],
  ["Classification / tax","TaxRegistrationNumber, VatRegistered, SupplierTin, DunsNumber, CorporateWebSite, TradeLicense* , LicenseType, IsOneTimeSupplier","TaxOrganizationTypeId, SupplierTypeId, TaxpayerCountryId, CountryNameId, VatRegistrationTypeId, BusinessPartnerTypeId, BusinessLocationTypeId, EInvoicingWaveId, TradeLicenseIssuingBodyId, BusinessRelationshipId → LookupValue"],
  ["Address","AddressLine1/2, City/State, PostalCode, VAT-certificate address block, address-purpose flags, RemittanceEmail, business phone","CityId, CountryId, BusinessPhoneCountryCodeId → LookupValue (VatCityId/VatCountryId are plain columns, no FK)"],
  ["Primary contact","Prefix, First/Middle/Last name, ContactEmail, Phone/Mobile + area codes","PrefixId, PhoneCountryCodeId, MobileCountryCodeId → LookupValue"],
  ["Site / assignment","SupplierSite, communication e-mail, function flags (IsOrdering, IsSourcingOnly, IsPurchasing, IsPay, IsPrimaryPay)","SupplierSiteBuId, ProcurementBuId, ClientBuId, BillToBuId → BusinessUnit; CommunicationMethodId → LookupValue"],
  ["Payment / banking","AccountName/Number, SwiftCode, Iban, dates, intermediary-bank block, AllowInternationalPayments","PaymentMethodId, PaymentTermsId, VatCodeId, CurrencyId, AccountCountryCodeId, AccountCurrencyCodeId, IntermediaryCountryId → LookupValue; BankId → Bank; BankBranchId → BankBranch"],
  ["Status / ownership / disclaimer","ReviewRemarks, DisclaimerAcknowledged/At/By/Text/Signature, DisclaimerSignatureImage [NotMapped]","StatusId → LookupValue; AssignedToUserId → User; DepartmentId → Department"]],
 [1800, 4900, 2800]));
kids.push(para("In total Supplier declares 25 foreign keys to LookupValue, five to BusinessUnit (one required), one each to Bank and BankBranch, and convention FKs to User (AssignedToUserId) and Department (DepartmentId). All LookupValue/BusinessUnit/Bank FKs use ON DELETE RESTRICT.", { size: 18, color: g.colors.GREY }));

kids.push(h2("3.2 Supplier child tables"));
kids.push(ent([
  ["SupplierContact","","FK → Supplier (Cascade)","Additional contacts; Prefix/Phone/Mobile country codes → LookupValue; SortOrder"],
  ["SupplierBankAccount","","FK → Supplier (Cascade)","Bank, BankBranch, currency/country codes; SortOrder"],
  ["SupplierVatGroupEntity","","FK → Supplier (Cascade)","VAT-group members; EInvoicingWaveId/BusinessLocationTypeId are plain columns"],
  ["SupplierPartner","","FK → Supplier (Cascade)","Trade-licence owners/partners (name, share, role, nationality)"],
  ["SupplierDocument","","FK → Supplier (Cascade)","Type (TradeLicense/VatCertification/Other/BankConfirmationLetter/SignedDeclaration); Content (byte[]) or BlobKey; index (SupplierId, DocumentType)"],
  ["SupplierAccessToken","","FK → Supplier (Cascade)","Self-service link + OTP + session hashes; unique TokenHash"],
  ["SupplierFieldChange","","FK → Supplier (Cascade)","Self-service change audit; FK → SupplierAccessToken (NoAction); index (SupplierId, IsAcknowledged)"],
]));

kids.push(h1("4. Customer Domain (dbo)"));
kids.push(ent([
  ["Customer","","FK → BusinessUnit (Restrict, optional)","CST/KYC master (~60 account fields, mostly free text). Status Draft→For Review→Completed/Returned. BaanRef/CustomerId/CustomerCode"],
  ["CustomerContact","","FK → Customer (Cascade, 1-to-1)","~74 KYC contact fields (identity, passport, visa, UAE Pass, PEP, employer)"],
  ["CustomerDocument","","FK → Customer (Cascade)","KYC files; Content (byte[]) or BlobKey; DocumentType (string)"],
  ["CustomerAccessToken","","FK → Customer (Cascade)","Self-service link + OTP + session hashes"],
]));
kids.push(para("Customer LOV-style fields (account type, nationality, country, etc.) are stored as free-text string columns, not foreign keys. The Customer's only real foreign key is BusinessUnitId.", { size: 18, color: g.colors.GREY }));

kids.push(h1("5. Leasing / Landlord Domain (leasing schema)"));
kids.push(para("A separate bounded context (LeasingDbContext) with its own migration history. Reads the read-only dbo.Property view; owns the leasing.* tables."));
kids.push(ent([
  ["Premises (view)","","dbo.Property (read-only view)","Property/building/lease reference data; never migrated"],
  ["Landlord","","leasing.Landlords","Registration record; Status (Draft…RegistrationComplete), signing rule, review/reject/complete audit, Circularo signature fields; BusinessUnitId (no cross-context FK)"],
  ["LandlordPremises","","FK → Landlord (Cascade)","Join to Premises (co-ownership); unique (LandlordId, PremisesId)"],
  ["LandlordContact","","FK → Landlord (Cascade)","Owner/representative/manager/legal; Emirates-ID details"],
  ["LandlordSignatory","","FK → Landlord (Cascade)","Signing parties; order; primary flag"],
  ["LandlordDocument","","FK → Landlord (Cascade)","EmiratesId/TradeLicense/MOA/POA/TitleDeed/… ; content as Base64 (varchar max); versioned"],
  ["LandlordRegistrationComment","","FK → Landlord (Cascade)","Correction/Rejection/Note; index (LandlordId, IsResolved)"],
  ["LandlordAccessToken","","FK → Landlord (Cascade)","Link + OTP + session + UAE Pass verification fields"],
  ["LandlordFieldChange / LandlordEvent","","LandlordId indexed (loose)","Self-service change audit and lifecycle event log"],
  ["MaintainTenantUaePassEmail","","dbo (shared, external)","Tenant UAE Pass e-mail ownership list"],
]));

kids.push(h1("6. Relationships Summary"));
kids.push(h2("6.1 Supplier schema"));
kids.push(bullet("BusinessUnit 1—* Supplier (BusinessUnitId, required, Restrict) plus 4 optional BU refs (Site/Procurement/Client/BillTo)."));
kids.push(bullet("User ⇄ BusinessUnit many-to-many (UserBusinessUnits); BusinessUnit 1—* User (CompanyBusinessUnitId)."));
kids.push(bullet("Department 1—* User and 1—* Supplier; User 1—* Supplier (AssignedToUserId)."));
kids.push(bullet("Supplier 1—* {Contact, BankAccount, VatGroupEntity, AccessToken, FieldChange, Document, Partner} — all cascade."));
kids.push(bullet("Bank 1—* BankBranch (cascade); LookupValue 1—* Supplier/SupplierContact/SupplierBankAccount (Restrict)."));
kids.push(bullet("SupplierAccessToken 1—* SupplierFieldChange (NoAction, to avoid multiple cascade paths)."));
kids.push(h2("6.2 Customer schema"));
kids.push(bullet("BusinessUnit 1—* Customer; Customer 1—1 CustomerContact; Customer 1—* {Document, AccessToken} (cascade)."));
kids.push(h2("6.3 Leasing schema"));
kids.push(bullet("Landlord 1—* {Premises, Contact, Signatory, Document, Comment, AccessToken} (cascade)."));
kids.push(bullet("LandlordPremises is the join for Landlord *—* Premises co-ownership (Premises is a read-only view; PremisesId is a snapshot key, no DB FK)."));
kids.push(bullet("LandlordFieldChange / LandlordEvent associate by LandlordId (indexed) with no configured FK. Leasing is a separate context, so there is no EF-level FK from Landlord to BusinessUnit."));

kids.push(h1("7. Indexes & Integrity Notes"));
kids.push(bullet("Unique indexes: BusinessUnit.Code, User.Email, LookupValue(Category, Code), SupplierFieldSetting.FieldKey, SupplierTabSetting.Step, SupplierAccessToken.TokenHash, LandlordPremises(LandlordId, PremisesId)."));
kids.push(bullet("[NotMapped] (not columns): Supplier.DisclaimerSignatureImage, Premises.Zone."));
kids.push(bullet("Blob columns (byte[]): BusinessUnit.Logo/Thumbnail, User.ProfilePhoto, SupplierDocument.Content, CustomerDocument.Content. LandlordDocument stores Base64 in a varchar(max) column."));
kids.push(bullet("Both contexts are registered with EnableRetryOnFailure() for Azure SQL transient-fault resilience."));

write(buildDoc({ title: "Supplier Portal — Database Schema", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Database Schema.docx");
