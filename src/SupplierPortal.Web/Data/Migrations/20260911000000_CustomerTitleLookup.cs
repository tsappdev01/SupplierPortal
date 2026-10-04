using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Turns the customer contact "Title" field into a lookup (dropdown) with Mr / Mrs / Ms / M/s /
    /// Dr. Data-only and re-runnable.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260911000000_CustomerTitleLookup")]
    public partial class CustomerTitleLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'lookup' " +
                "WHERE [Section] = 'Contact' AND [FieldKey] = 'Title';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] SET [FieldKind] = 'text' " +
                "WHERE [Section] = 'Contact' AND [FieldKey] = 'Title';");
        }
    }
}
