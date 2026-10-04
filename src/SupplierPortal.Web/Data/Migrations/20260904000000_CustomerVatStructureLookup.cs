using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Turns the customer "VAT Structure" field into a lookup (dropdown) with the values
    /// "VAT Group" / "Individual VAT", matching the LOV workbook. Data-only and re-runnable.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260904000000_CustomerVatStructureLookup")]
    public partial class CustomerVatStructureLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'lookup' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'VATStructure';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'text' " +
                "WHERE [Section] = 'Account' AND [FieldKey] = 'VATStructure';");
        }
    }
}
