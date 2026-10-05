using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds <c>ControllingOperator</c> to CustomerFieldConfigs: how a conditional field's
    /// <c>ControllingValue</c> is compared to the controlling field's current value — "eq" (equals, the previous
    /// and default behaviour) or "ne" (not equals, e.g. show VISA fields only when Nationality is NOT
    /// "United Arab Emirates"). Existing conditional rows are backfilled to "eq" so behaviour is unchanged.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261031000000_CustomerFieldConditionalOperator")]
    public partial class CustomerFieldConditionalOperator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControllingOperator",
                table: "CustomerFieldConfigs",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            // Preserve existing behaviour: any field that already has a depends-on rule compares with "equals".
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET ControllingOperator = N'eq' " +
                "WHERE ControllingFieldKey IS NOT NULL AND ControllingOperator IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ControllingOperator",
                table: "CustomerFieldConfigs");
        }
    }
}
