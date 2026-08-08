using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFieldGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "CustomerFieldConfigs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            // Assign each field to its wizard group/step.
            migrationBuilder.Sql(@"UPDATE [CustomerFieldConfigs] SET [GroupName]='Company Identity & Classification' WHERE [Section]='Account' AND [FieldKey] IN ('LegalName','CommercialName','AccountNameArabic','AccountType','TypeOfCustomer','TypeOfOrganization','NumberOfEntities','EINvoicingWaveApplicability','BusinessActivity','Owner','CustomerType','PowerOfAttorney','PowerOfAttorney2');
UPDATE [CustomerFieldConfigs] SET [GroupName]='Tax, Trade License & Registration' WHERE [Section]='Account' AND [FieldKey] IN ('VATStructure','CustomerBuyerTIN','BuyerLegalRegistrationIdentifierType','TRNApplicable','TRNNumber','TradeLicenseNumber','LicensingAuthorityName','AuthorityCode','TradeLicenseIssuingBody','TradeLicenseIssueDate','TradeLicenseExpiryDate','PlaceOfIncorporation','EstablishmentDate','PEP');
UPDATE [CustomerFieldConfigs] SET [GroupName]='Contact & Address' WHERE [Section]='Account' AND [FieldKey] IN ('PrimaryPhone','PrimaryEmail','SecondaryEmailAddress','SecondaryPhone','URL','IncorporationCountry','LandlineMobile','CountryCode','CountryName','AddressLine1','AddressLine2','POBox','CountrySubdivision','EmirateCity','Country','AddressLine12','AddressLine22','POBox2','Emirate');
UPDATE [CustomerFieldConfigs] SET [GroupName]='Ownership, Banking & Payment' WHERE [Section]='Account' AND [FieldKey] IN ('CustomerBankAccountName','IBANNumber','Owner1','Owner1Nationality','Owner1Passport','Owner1PassportIssueCountry','Owner1PassportIssuePlaceCountry','Owner1SharePercentage','Owner2','Owner2Nationality','Owner2Passport','Owner2PassportIssueCountry','Owner2PassportIssuePlaceCountry','Owner2SharePercentage','PaymentTerms');
UPDATE [CustomerFieldConfigs] SET [GroupName]='Personal Details' WHERE [Section]='Contact' AND [FieldKey] IN ('Title','FirstName','MiddleName','LastName','FullName','Owner','Account','Gender','MaritalStatus','PreferredLanguage','BuyingRole','ResidentialStatus','Nationality','DateOfBirth','BirthPlace','MothersName','AliasName','Nationality2');
UPDATE [CustomerFieldConfigs] SET [GroupName]='Contact & UAE Pass' WHERE [Section]='Contact' AND [FieldKey] IN ('MobileNumber','Email','SecondaryEmail','SecondaryPhoneNumber','IBANNumber','Country','AddressLine1','AddressLine2','POBox','EmirateCity','Country2','AddressLine12','AddressLine22','POBox2','Emirates','LandlineNumber','EmployerPhone','EmployerAddress','UAEPassRegisteredEmail1','UAEPassRegisteredEmail2','UAEPassRegisteredEmail3','UAEPassRegisteredEmail4','UAEPassMobileNo1','UAEPassMobileNo2','UAEPassMobileNo3','UAEPassMobileNo4');
UPDATE [CustomerFieldConfigs] SET [GroupName]='Identification' WHERE [Section]='Contact' AND [FieldKey] IN ('Passport','PassportNumber','PassportIssueDate','PassportExpiryDate','PassportIssueCountryCode','PassportIssuePlaceCountry','DualPassport','DualPassportNumber','DualPassportIssueDate','DualPassportExpiryDate','VISAExpiryDate','VISANumber','TypeOfID','CountryGovtIDNumber','CountryGovtIDIssueDate','CountryGovtIDExpiryDate','EmirateID','EmirateIDExpiryDate','EmiratesIDIssueDate','IssueCountry');
UPDATE [CustomerFieldConfigs] SET [GroupName]='Employment & Other' WHERE [Section]='Contact' AND [FieldKey] IN ('Occupation','TAXNumber','PEP','EmployerName','SourceOfIncome','ReceivedTradeLicense','ReceivedVATCertificate','TypeOfCustomer');");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "CustomerFieldConfigs");
        }
    }
}
