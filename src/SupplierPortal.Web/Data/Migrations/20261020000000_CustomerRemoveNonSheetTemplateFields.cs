using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Removes the customer fields that are NOT on the business "Customer Data Template" sheet, so the
    /// upload/download template (now built from all non-removed fields) matches the sheet exactly in set and
    /// order. These were previously only hidden (IsVisible=0); now they are marked removed so they drop out of
    /// the template entirely:
    ///   Account:  Customer Code (the auto-generated code; not a sheet column).
    ///   Contact:  Passport (Yes/No) toggle, Type of Customer, and the eight UAE Pass email/mobile fields.
    /// Reversible: Down clears IsRemoved (leaving them hidden, as they were after the alignment migration). The
    /// underlying Customer.* / CustomerContact.* properties are untouched — only the field-config rows change.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261020000000_CustomerRemoveNonSheetTemplateFields")]
    public partial class CustomerRemoveNonSheetTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1, IsVisible = 0 WHERE Section = N'Account' AND FieldKey = N'CustomerCode';");

            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1, IsVisible = 0
WHERE Section = N'Contact' AND FieldKey IN (
    N'Passport', N'TypeOfCustomer',
    N'UAEPassRegisteredEmail1', N'UAEPassRegisteredEmail2', N'UAEPassRegisteredEmail3', N'UAEPassRegisteredEmail4',
    N'UAEPassMobileNo1', N'UAEPassMobileNo2', N'UAEPassMobileNo3', N'UAEPassMobileNo4');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore to the post-alignment state (present but hidden).
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0 WHERE Section = N'Account' AND FieldKey = N'CustomerCode';");

            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0
WHERE Section = N'Contact' AND FieldKey IN (
    N'Passport', N'TypeOfCustomer',
    N'UAEPassRegisteredEmail1', N'UAEPassRegisteredEmail2', N'UAEPassRegisteredEmail3', N'UAEPassRegisteredEmail4',
    N'UAEPassMobileNo1', N'UAEPassMobileNo2', N'UAEPassMobileNo3', N'UAEPassMobileNo4');");
        }
    }
}
