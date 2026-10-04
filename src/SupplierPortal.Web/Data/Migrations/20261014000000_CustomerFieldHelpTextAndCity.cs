using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds a per-field HelpText (shown as a tooltip on the form when not blank), renames the
    /// account "Contact &amp; Address" Emirate/City field to "City" (UAE-emirate dropdown when the country is
    /// the UAE, else free text — handled in the forms), and removes the "Country Subdivision" field.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261014000000_CustomerFieldHelpTextAndCity")]
    public partial class CustomerFieldHelpTextAndCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HelpText",
                table: "CustomerFieldConfigs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'City' WHERE Section = N'Account' AND FieldKey = N'EmirateCity';");

            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1 WHERE Section = N'Account' AND FieldKey = N'CountrySubdivision';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0 WHERE Section = N'Account' AND FieldKey = N'CountrySubdivision';");
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Emirate/City' WHERE Section = N'Account' AND FieldKey = N'EmirateCity';");
            migrationBuilder.DropColumn(name: "HelpText", table: "CustomerFieldConfigs");
        }
    }
}
