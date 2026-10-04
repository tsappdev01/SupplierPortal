using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Turns the customer "Trade License Issuing Body" field into a lookup (dropdown) with the
    /// values "Mainland" / "Free Zone". Data-only and re-runnable.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260910000000_CustomerTradeLicenseIssuingBodyLookup")]
    public partial class CustomerTradeLicenseIssuingBodyLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'lookup' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'TradeLicenseIssuingBody';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'text' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'TradeLicenseIssuingBody';");
        }
    }
}
