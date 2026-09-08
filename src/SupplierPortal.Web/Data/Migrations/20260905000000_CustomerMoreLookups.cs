using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Turns two more customer fields into lookups (dropdowns) matching the LOV workbook:
    /// Licensing Authority Name (Account) and Gender (Contact). Data-only and re-runnable.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260905000000_CustomerMoreLookups")]
    public partial class CustomerMoreLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'lookup' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'LicensingAuthorityName';");
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'lookup' " +
                "WHERE [Section] = 'Contact' AND [FieldKey] = 'Gender';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'text' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'LicensingAuthorityName';");
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'text' " +
                "WHERE [Section] = 'Contact' AND [FieldKey] = 'Gender';");
        }
    }
}
