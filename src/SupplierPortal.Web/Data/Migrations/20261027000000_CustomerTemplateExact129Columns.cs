using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Aligns the customer import/export template (and the "all fields" Excel export) with the exact
    /// 129-column specification supplied by the business: 63 Account columns (Legal Name … Payment Terms) and
    /// 62 Contact columns (Title … Received VAT Certificate). The column order is driven by
    /// <c>CustomerFieldConfig.Sequence</c>, and the seeded order already matches the spec, so this migration only
    /// needs to reconcile the field SET and the three renamed owner labels:
    ///   • Removes the three ERP identifier columns (Party ID, Party Number, ERP Bill To Account) that were
    ///     appended after Payment Terms — the 129-spec does not include them.
    ///   • Removes the Contact columns that are not in the 129-spec: the Passport (Yes/No) flag, the contact-level
    ///     Type of Customer, and the eight UAE Pass registered email/mobile columns.
    ///   • Restores "Owner 2 - Passport Issue Place/Country" (row 61), which a prior template-alignment migration
    ///     had dropped; the spec lists it for Owner 2 as well as Owner 1.
    ///   • Reverts the Owner 1/2 passport-country labels to the spec wording ("… Passport Issue Country" /
    ///     "… Passport Issue Place/Country"), undoing the earlier "Country Code" / "Place/Country Name" rename.
    /// Fields are hidden with IsRemoved (not deleted) so they remain restorable from the Field Manager, and the
    /// template emits every non-removed column regardless of per-form visibility. Pure data; reversible.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261027000000_CustomerTemplateExact129Columns")]
    public partial class CustomerTemplateExact129Columns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop the ERP identifier columns from the template (not in the 129-spec).
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1, IsVisible = 0 " +
                "WHERE Section = N'Account' AND FieldKey IN (N'PartyId', N'PartyNumber', N'ErpBillToAccount');");

            // 2. Drop the Contact columns that are not in the 129-spec (Passport Yes/No flag, contact-level
            //    Type of Customer, and the eight UAE Pass registered email/mobile columns).
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1, IsVisible = 0 " +
                "WHERE Section = N'Contact' AND FieldKey IN (" +
                "N'Passport', N'TypeOfCustomer', " +
                "N'UAEPassRegisteredEmail1', N'UAEPassRegisteredEmail2', N'UAEPassRegisteredEmail3', N'UAEPassRegisteredEmail4', " +
                "N'UAEPassMobileNo1', N'UAEPassMobileNo2', N'UAEPassMobileNo3', N'UAEPassMobileNo4');");

            // 3. Restore Owner 2's Place/Country column (spec row 61). Its stored Sequence already places it
            //    between "Owner 2 - Passport Issue Country" and "Owner 2 - Share Percentage".
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0, IsVisible = 1 " +
                "WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssuePlaceCountry';");

            // 4. Revert the owner passport-country labels to the spec wording.
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Country' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssueCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Place/Country' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssuePlaceCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 2 - Passport Issue Country' WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssueCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 2 - Passport Issue Place/Country' WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssuePlaceCountry';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-apply the "Country Code" / "Place/Country Name" labels and re-drop Owner 2's Place/Country.
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Country Code' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssueCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Place/Country Name' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssuePlaceCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 2 - Passport Issue Country Code' WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssueCountry';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1, IsVisible = 0 " +
                "WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssuePlaceCountry';");

            // Restore the Contact columns removed above.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0, IsVisible = 1 " +
                "WHERE Section = N'Contact' AND FieldKey IN (" +
                "N'Passport', N'TypeOfCustomer', " +
                "N'UAEPassRegisteredEmail1', N'UAEPassRegisteredEmail2', N'UAEPassRegisteredEmail3', N'UAEPassRegisteredEmail4', " +
                "N'UAEPassMobileNo1', N'UAEPassMobileNo2', N'UAEPassMobileNo3', N'UAEPassMobileNo4');");

            // Restore the ERP identifier columns.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0, IsVisible = 1 " +
                "WHERE Section = N'Account' AND FieldKey IN (N'PartyId', N'PartyNumber', N'ErpBillToAccount');");
        }
    }
}
