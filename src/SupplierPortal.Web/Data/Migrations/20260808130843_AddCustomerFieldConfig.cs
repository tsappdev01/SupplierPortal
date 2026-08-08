using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFieldConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerFieldConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    FieldKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BusinessControl = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerControl = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFieldConfigs", x => x.Id);
                });

            // Seed the per-field configuration from the CST onboarding sheet (133 fields:
            // 61 Account + 72 Contact). Governs per-role control + visibility + required.
            migrationBuilder.Sql(@"INSERT INTO [CustomerFieldConfigs] ([FieldKey],[DisplayLabel],[Section],[Sequence],[FieldKind],[BusinessControl],[CustomerControl],[IsVisible],[IsRequired]) VALUES
('LegalName','Legal Name','Account',1,'text','Editable','Editable',1,1),
('CommercialName','Commercial Name','Account',2,'text','Editable','Editable',1,0),
('AccountNameArabic','Account Name Arabic','Account',3,'text','Editable','Editable',1,0),
('AccountType','Account Type','Account',4,'lookup','Lookup','Lookup',1,0),
('TypeOfCustomer','Type Of Customer','Account',5,'lookup','Lookup','Lookup',1,0),
('TypeOfOrganization','Type of Organization','Account',6,'lookup','Lookup','Lookup',1,0),
('VATStructure','VAT Structure','Account',7,'text','Editable','Editable',1,0),
('NumberOfEntities','Number of Entities','Account',8,'text','Editable','Editable',1,0),
('CustomerBuyerTIN','Customer/Buyer TIN','Account',9,'text','Editable','Editable',1,0),
('EINvoicingWaveApplicability','eINvoicing Wave Applicability','Account',10,'lookup','Lookup','Lookup',1,0),
('BusinessActivity','Business Activity','Account',11,'text','Editable','Editable',1,0),
('PrimaryPhone','Primary Phone','Account',12,'text','Editable','Editable',1,0),
('PrimaryEmail','Primary Email','Account',13,'text','Editable','Editable',1,0),
('SecondaryEmailAddress','Secondary Email Address','Account',14,'text','Editable','Editable',1,0),
('SecondaryPhone','Secondary Phone','Account',15,'text','Editable','Editable',1,0),
('URL','URL','Account',16,'text','Editable','Editable',1,0),
('BuyerLegalRegistrationIdentifierType','Buyer Legal Registration Identifier Type','Account',17,'lookup','Lookup','Lookup',1,0),
('TRNApplicable','TRN Applicable (Yes/No)','Account',18,'yesno','Editable','Editable',1,0),
('TRNNumber','TRN Number','Account',19,'text','Editable','Editable',1,0),
('TradeLicenseNumber','Trade License Number','Account',20,'text','Editable','Editable',1,0),
('LicensingAuthorityName','Licensing Authority Name','Account',21,'text','Editable','Editable',1,0),
('AuthorityCode','Authority Code','Account',22,'text','Editable','Editable',1,0),
('TradeLicenseIssuingBody','Trade License Issuing Body','Account',23,'text','Editable','Editable',1,0),
('TradeLicenseIssueDate','Trade License Issue Date','Account',24,'date','Editable','Editable',1,0),
('TradeLicenseExpiryDate','Trade License Expiry Date','Account',25,'date','Editable','Editable',1,0),
('IncorporationCountry','Incorporation Country','Account',26,'text','Editable','Editable',1,0),
('PlaceOfIncorporation','Place of Incorporation','Account',27,'text','Editable','Editable',1,0),
('EstablishmentDate','Establishment Date','Account',28,'date','Editable','Editable',1,0),
('LandlineMobile','Landline Mobile','Account',29,'text','Editable','Editable',1,0),
('PEP','PEP(Yes/No)','Account',30,'yesno','Editable','Editable',1,0),
('CustomerBankAccountName','Customer Bank Account Name','Account',31,'text','Editable','Editable',1,0),
('IBANNumber','IBAN Number','Account',32,'text','Editable','Editable',1,0),
('Owner','Owner','Account',33,'text','Editable','Editable',1,0),
('CustomerType','Customer Type','Account',34,'lookup','Lookup','Lookup',1,0),
('PowerOfAttorney','Power of Attorney - (Yes/No)','Account',35,'yesno','Editable','Editable',1,0),
('PowerOfAttorney2','Power of Attorney','Account',36,'text','Editable','Editable',1,0),
('CountryCode','Country Code','Account',37,'lookup','Lookup','Lookup',1,0),
('CountryName','Country Name','Account',38,'lookup','Lookup','Lookup',1,0),
('AddressLine1','Address Line 1','Account',39,'text','Editable','Editable',1,0),
('AddressLine2','Address Line 2','Account',40,'text','Editable','Editable',1,0),
('POBox','P.O. Box','Account',41,'text','Editable','Editable',1,0),
('CountrySubdivision','Country Subdivision','Account',42,'lookup','Lookup','Lookup',1,0),
('EmirateCity','Emirate/City','Account',43,'text','Editable','Editable',1,0),
('Country','Country (Arabic)','Account',44,'text','Editable','Editable',1,0),
('AddressLine12','Address Line 1 (Arabic)','Account',45,'text','Editable','Editable',1,0),
('AddressLine22','Address Line 2 (Arabic)','Account',46,'text','Editable','Editable',1,0),
('POBox2','P.O. Box (Arabic)','Account',47,'text','Editable','Editable',1,0),
('Emirate','Emirate (Arabic)','Account',48,'text','Editable','Editable',1,0),
('Owner1','Owner 1','Account',49,'text','Editable','Editable',1,0),
('Owner1Nationality','Owner 1 – Nationality','Account',50,'lookup','Lookup','Lookup',1,0),
('Owner1Passport','Owner 1 – Passport','Account',51,'text','Editable','Editable',1,0),
('Owner1PassportIssueCountry','Owner 1 - Passport Issue Country','Account',52,'text','Editable','Editable',1,0),
('Owner1PassportIssuePlaceCountry','Owner 1 - Passport Issue Place/Country','Account',53,'text','Editable','Editable',1,0),
('Owner1SharePercentage','Owner 1 – Share Percentage','Account',54,'text','Editable','Editable',1,0),
('Owner2','Owner 2','Account',55,'text','Editable','Editable',1,0),
('Owner2Nationality','Owner 2 – Nationality','Account',56,'lookup','Lookup','Lookup',1,0),
('Owner2Passport','Owner 2 – Passport','Account',57,'text','Editable','Editable',1,0),
('Owner2PassportIssueCountry','Owner 2 - Passport Issue Country','Account',58,'text','Editable','Editable',1,0),
('Owner2PassportIssuePlaceCountry','Owner 2 - Passport Issue Place/Country','Account',59,'text','Editable','Editable',1,0),
('Owner2SharePercentage','Owner 2 – Share Percentage','Account',60,'text','Editable','Editable',1,0),
('PaymentTerms','Payment Terms','Account',61,'lookup','Lookup','Lookup',1,0),
('Title','Title','Contact',1,'text','Editable','Editable',1,0),
('FirstName','First Name','Contact',2,'text','Editable','Editable',1,0),
('MiddleName','Middle Name','Contact',3,'text','Editable','Editable',1,0),
('LastName','Last Name','Contact',4,'text','Editable','Editable',1,0),
('FullName','Full Name (Arabic)','Contact',5,'text','Editable','Editable',1,0),
('MobileNumber','Mobile Number','Contact',6,'text','Editable','Editable',1,0),
('Email','Email','Contact',7,'text','Editable','Editable',1,0),
('Owner','Owner','Contact',8,'text','Editable','Editable',1,0),
('Account','Account','Contact',9,'text','Editable','Editable',1,0),
('Gender','Gender','Contact',10,'text','Editable','Editable',1,0),
('MaritalStatus','Marital Status','Contact',11,'lookup','Lookup','Lookup',1,0),
('PreferredLanguage','Preferred Language','Contact',12,'lookup','Lookup','Lookup',1,0),
('BuyingRole','Buying Role','Contact',13,'lookup','Lookup','Lookup',1,0),
('SecondaryEmail','Secondary Email','Contact',14,'text','Editable','Editable',1,0),
('SecondaryPhoneNumber','Secondary Phone Number','Contact',15,'text','Editable','Editable',1,0),
('IBANNumber','IBAN Number','Contact',16,'text','Editable','Editable',1,0),
('ResidentialStatus','Residential Status','Contact',17,'lookup','Lookup','Lookup',1,0),
('Nationality','Nationality','Contact',18,'lookup','Lookup','Lookup',1,0),
('Country','Country','Contact',19,'lookup','Lookup','Lookup',1,0),
('AddressLine1','Address Line 1','Contact',20,'text','Editable','Editable',1,0),
('AddressLine2','Address Line 2','Contact',21,'text','Editable','Editable',1,0),
('POBox','P.O. Box','Contact',22,'text','Editable','Editable',1,0),
('EmirateCity','Emirate/City','Contact',23,'text','Editable','Editable',1,0),
('Country2','Country (Arabic)','Contact',24,'text','Editable','Editable',1,0),
('AddressLine12','Address Line 1 (Arabic)','Contact',25,'text','Editable','Editable',1,0),
('AddressLine22','Address Line 2 (Arabic)','Contact',26,'text','Editable','Editable',1,0),
('POBox2','P.O. Box (Arabic)','Contact',27,'text','Editable','Editable',1,0),
('Emirates','Emirates (Arabic)','Contact',28,'text','Editable','Editable',1,0),
('Passport','Passport (Yes/No)','Contact',29,'yesno','Editable','Editable',1,0),
('PassportNumber','Passport Number','Contact',30,'text','Editable','Editable',1,0),
('PassportIssueDate','Passport Issue Date','Contact',31,'date','Editable','Editable',1,0),
('PassportExpiryDate','Passport Expiry Date','Contact',32,'date','Editable','Editable',1,0),
('PassportIssueCountryCode','Passport Issue Country Code','Contact',33,'text','Editable','Editable',1,0),
('PassportIssuePlaceCountry','Passport Issue Place/Country','Contact',34,'text','Editable','Editable',1,0),
('DualPassport','Dual Passport?(Yes/No)','Contact',35,'yesno','Editable','Editable',1,0),
('DualPassportNumber','Dual Passport Number','Contact',36,'text','Editable','Editable',1,0),
('DualPassportIssueDate','Dual Passport Issue Date','Contact',37,'date','Editable','Editable',1,0),
('DualPassportExpiryDate','Dual Passport Expiry Date','Contact',38,'date','Editable','Editable',1,0),
('VISAExpiryDate','VISA Expiry Date','Contact',39,'date','Editable','Editable',1,0),
('VISANumber','VISA Number','Contact',40,'text','Editable','Editable',1,0),
('TypeOfID','Type of ID (Driving License / National ID / Voters ID)','Contact',41,'text','Editable','Editable',1,0),
('CountryGovtIDNumber','Country Govt ID Number','Contact',42,'text','Editable','Editable',1,0),
('CountryGovtIDIssueDate','Country Govt ID Issue Date','Contact',43,'date','Editable','Editable',1,0),
('CountryGovtIDExpiryDate','Country Govt ID Expiry Date','Contact',44,'date','Editable','Editable',1,0),
('EmirateID','Emirate ID','Contact',45,'text','Editable','Editable',1,0),
('EmirateIDExpiryDate','Emirate ID Expiry Date','Contact',46,'date','Editable','Editable',1,0),
('EmiratesIDIssueDate','Emirates ID Issue Date','Contact',47,'date','Editable','Editable',1,0),
('IssueCountry','Issue Country','Contact',48,'lookup','Lookup','Lookup',1,0),
('Occupation','Occupation','Contact',49,'text','Editable','Editable',1,0),
('DateOfBirth','Date of Birth','Contact',50,'date','Editable','Editable',1,0),
('BirthPlace','Birth Place','Contact',51,'text','Editable','Editable',1,0),
('MothersName','Mother’s Name','Contact',52,'text','Editable','Editable',1,0),
('AliasName','Alias Name','Contact',53,'text','Editable','Editable',1,0),
('Nationality2','Nationality 2','Contact',54,'text','Editable','Editable',1,0),
('TAXNumber','TAX Number','Contact',55,'text','Editable','Editable',1,0),
('PEP','PEP (Yes/No)','Contact',56,'yesno','Editable','Editable',1,0),
('LandlineNumber','Landline Number','Contact',57,'text','Editable','Editable',1,0),
('EmployerName','Employer Name','Contact',58,'text','Editable','Editable',1,0),
('EmployerPhone','Employer Phone','Contact',59,'text','Editable','Editable',1,0),
('EmployerAddress','Employer Address','Contact',60,'text','Editable','Editable',1,0),
('SourceOfIncome','Source Of Income','Contact',61,'text','Editable','Editable',1,0),
('ReceivedTradeLicense','Received Trade License? (Yes/No)','Contact',62,'yesno','Editable','Editable',1,0),
('ReceivedVATCertificate','Received VAT Certificate? (Yes/No)','Contact',63,'yesno','Editable','Editable',1,0),
('TypeOfCustomer','Type of Customer','Contact',64,'lookup','Lookup','Lookup',1,0),
('UAEPassRegisteredEmail1','UAEPass Registered Email 1','Contact',65,'text','Editable','Editable',1,0),
('UAEPassRegisteredEmail2','UAEPass Registered Email 2','Contact',66,'text','Editable','Editable',1,0),
('UAEPassRegisteredEmail3','UAEPass Registered Email 3','Contact',67,'text','Editable','Editable',1,0),
('UAEPassRegisteredEmail4','UAEPass Registered Email 4','Contact',68,'text','Editable','Editable',1,0),
('UAEPassMobileNo1','UAEPass Mobile No. 1','Contact',69,'text','Editable','Editable',1,0),
('UAEPassMobileNo2','UAEPass Mobile No. 2','Contact',70,'text','Editable','Editable',1,0),
('UAEPassMobileNo3','UAEPass Mobile No. 3','Contact',71,'text','Editable','Editable',1,0),
('UAEPassMobileNo4','UAEPass Mobile No. 4','Contact',72,'text','Editable','Editable',1,0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerFieldConfigs");
        }
    }
}
