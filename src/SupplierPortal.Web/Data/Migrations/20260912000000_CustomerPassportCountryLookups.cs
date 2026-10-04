using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Turns the contact passport country fields into lookups: Passport Issue Place/Country shows
    /// the country name list, Passport Issue Country Code shows the ISO code list. Data-only, re-runnable.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260912000000_CustomerPassportCountryLookups")]
    public partial class CustomerPassportCountryLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'lookup' " +
                "WHERE [Section] = 'Contact' AND [FieldKey] IN ('PassportIssuePlaceCountry','PassportIssueCountryCode');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'text' " +
                "WHERE [Section] = 'Contact' AND [FieldKey] IN ('PassportIssuePlaceCountry','PassportIssueCountryCode');");
        }
    }
}
