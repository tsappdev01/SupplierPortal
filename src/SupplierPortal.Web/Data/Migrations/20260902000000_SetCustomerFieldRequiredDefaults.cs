using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Seeds the mandatory/optional default for every CST customer field per the business's
    /// onboarding specification. Data-only: it resets each section's Required flag and then marks the
    /// mandatory fields, so it is safe to re-run. Administrators can still override any of these from the
    /// Customer Field Settings screen afterwards.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260902000000_SetCustomerFieldRequiredDefaults")]
    public partial class SetCustomerFieldRequiredDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Start from a clean slate for each section, then flag the mandatory fields. A hidden field is
            // never required, so guard on IsVisible.
            migrationBuilder.Sql("UPDATE [CustomerFieldConfigs] SET [IsRequired] = 0;");

            migrationBuilder.Sql(@"UPDATE [CustomerFieldConfigs] SET [IsRequired] = 1
                WHERE [Section] = 'Account' AND [IsVisible] = 1 AND [FieldKey] IN (
                    'LegalName','AccountNameArabic','AccountType','TypeOfCustomer','TypeOfOrganization',
                    'VATStructure','NumberOfEntities','CustomerBuyerTIN','EINvoicingWaveApplicability',
                    'PrimaryPhone','PrimaryEmail','BuyerLegalRegistrationIdentifierType','TRNApplicable',
                    'TRNNumber','TradeLicenseNumber','LicensingAuthorityName','AuthorityCode',
                    'TradeLicenseIssuingBody','TradeLicenseIssueDate','TradeLicenseExpiryDate',
                    'IncorporationCountry','PlaceOfIncorporation','EstablishmentDate','CustomerType',
                    'PowerOfAttorney','CountryCode','CountryName','AddressLine1','CountrySubdivision',
                    'EmirateCity','PaymentTerms');");

            migrationBuilder.Sql(@"UPDATE [CustomerFieldConfigs] SET [IsRequired] = 1
                WHERE [Section] = 'Contact' AND [IsVisible] = 1 AND [FieldKey] IN (
                    'Title','FirstName','LastName','FullName','MobileNumber','Email','Account',
                    'ResidentialStatus','Nationality','Country','AddressLine1','EmirateCity',
                    'PassportNumber','PassportIssueDate','PassportExpiryDate','PassportIssueCountryCode',
                    'PassportIssuePlaceCountry','VISAExpiryDate','VISANumber','EmirateID',
                    'EmirateIDExpiryDate','EmiratesIDIssueDate','IssueCountry');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-reversible data seed: clearing the flags is the closest safe inverse.
            migrationBuilder.Sql("UPDATE [CustomerFieldConfigs] SET [IsRequired] = 0;");
        }
    }
}
