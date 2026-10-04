using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Makes Incorporation Country a lookup (Code - Name) and hides the redundant Passport Issue
    /// Country Code field (Passport Issue Place/Country now carries the Code - Name). Data-only, re-runnable.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260913000000_CustomerIncorporationCountryLookupHidePassportCode")]
    public partial class CustomerIncorporationCountryLookupHidePassportCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'lookup' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'IncorporationCountry';");

            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [IsVisible] = 0, [IsRequired] = 0 " +
                "WHERE [Section] = 'Contact' AND [FieldKey] = 'PassportIssueCountryCode';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'text' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'IncorporationCountry';");

            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [IsVisible] = 1 " +
                "WHERE [Section] = 'Contact' AND [FieldKey] = 'PassportIssueCountryCode';");
        }
    }
}
