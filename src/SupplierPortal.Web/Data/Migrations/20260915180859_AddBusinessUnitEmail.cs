using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessUnitEmail : Migration
    {
        // Purely additive: adds the optional Email column to BusinessUnits. Any unrelated FK/index operations
        // the scaffolder produced from pre-existing model-snapshot drift were removed so this migration only
        // touches the new column and cannot alter existing foreign keys or indexes.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "BusinessUnits",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "BusinessUnits");
        }
    }
}
