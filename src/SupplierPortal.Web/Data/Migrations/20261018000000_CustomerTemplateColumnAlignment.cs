using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Aligns the customer upload/download template (generated from the visible CustomerFieldConfig
    /// rows, in Section + Sequence order) with the business's "Customer Data Template" workbook, so the
    /// exported/imported columns match the sheet exactly and round-trip with Oracle Fusion.
    ///
    /// Data-only, fully reversible (each change is undone in Down). Applies the reversible Field-Manager
    /// changes only:
    ///   1. Hide the extra leading "Customer Code" account column (not on the sheet) — it shifted the whole
    ///      account block by one.
    ///   4. Restore the "Country Subdivision" account column (previously removed).
    ///   5. Restore the "IBAN Number" contact column (previously removed).
    ///   6. Restore the "Passport Issue Country Code" contact column (previously hidden).
    ///   7. Hide the contact columns that are not on the sheet (Passport Yes/No toggle, Type of Customer,
    ///      and the eight UAE Pass email/mobile fields).
    ///   8. Restore the sheet's labels: Account &amp; Contact "Emirate/City" (shown as "City"), Contact "Email"
    ///      (shown as "Contact Email").
    ///
    /// NOTE: two sheet columns are still absent because they have no backing field yet — "Legal Names of all
    /// Entities under the VAT Group" (sheet col 11; modelled as the CustomerVatGroupEntity child grid) and
    /// "Business Location Type" (sheet col 26). Those are additive (new field) changes handled separately;
    /// until then the account block matches the sheet except for those two positions.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261018000000_CustomerTemplateColumnAlignment")]
    public partial class CustomerTemplateColumnAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Hide the extra leading "Customer Code" account column (not present on the sheet).
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsVisible = 0 WHERE Section = N'Account' AND FieldKey = N'CustomerCode';");

            // 4. Restore "Country Subdivision" (Account) — sheet col 46.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0 WHERE Section = N'Account' AND FieldKey = N'CountrySubdivision';");

            // 5. Restore "IBAN Number" (Contact) — sheet col 85.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0 WHERE Section = N'Contact' AND FieldKey = N'IBANNumber';");

            // 6. Restore "Passport Issue Country Code" (Contact) — sheet col 101.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsVisible = 1 WHERE Section = N'Contact' AND FieldKey = N'PassportIssueCountryCode';");

            // 7. Hide the contact columns that are not on the sheet.
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs SET IsVisible = 0
WHERE Section = N'Contact' AND FieldKey IN (
    N'Passport', N'TypeOfCustomer',
    N'UAEPassRegisteredEmail1', N'UAEPassRegisteredEmail2', N'UAEPassRegisteredEmail3', N'UAEPassRegisteredEmail4',
    N'UAEPassMobileNo1', N'UAEPassMobileNo2', N'UAEPassMobileNo3', N'UAEPassMobileNo4');");

            // 8. Restore the sheet's labels.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Emirate/City' WHERE Section = N'Account' AND FieldKey = N'EmirateCity';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Emirate/City' WHERE Section = N'Contact' AND FieldKey = N'EmirateCity';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Email' WHERE Section = N'Contact' AND FieldKey = N'Email';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert each change to its pre-migration state.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsVisible = 1 WHERE Section = N'Account' AND FieldKey = N'CustomerCode';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1 WHERE Section = N'Account' AND FieldKey = N'CountrySubdivision';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1 WHERE Section = N'Contact' AND FieldKey = N'IBANNumber';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsVisible = 0 WHERE Section = N'Contact' AND FieldKey = N'PassportIssueCountryCode';");
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs SET IsVisible = 1
WHERE Section = N'Contact' AND FieldKey IN (
    N'Passport', N'TypeOfCustomer',
    N'UAEPassRegisteredEmail1', N'UAEPassRegisteredEmail2', N'UAEPassRegisteredEmail3', N'UAEPassRegisteredEmail4',
    N'UAEPassMobileNo1', N'UAEPassMobileNo2', N'UAEPassMobileNo3', N'UAEPassMobileNo4');");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'City' WHERE Section = N'Account' AND FieldKey = N'EmirateCity';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'City' WHERE Section = N'Contact' AND FieldKey = N'EmirateCity';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Contact Email' WHERE Section = N'Contact' AND FieldKey = N'Email';");
        }
    }
}
