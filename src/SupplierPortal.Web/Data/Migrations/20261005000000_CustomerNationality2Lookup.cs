using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Makes the contact "Nationality 2" field render as a lookup (country list), matching
    /// "Nationality", instead of a free-text box.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261005000000_CustomerNationality2Lookup")]
    public partial class CustomerNationality2Lookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET FieldKind = 'lookup' WHERE Section = 'Contact' AND FieldKey = 'Nationality2';");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET FieldKind = 'text' WHERE Section = 'Contact' AND FieldKey = 'Nationality2';");
    }
}
