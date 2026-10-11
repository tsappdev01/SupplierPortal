using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class LandlordLegacySourceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacySourceId",
                schema: "leasing",
                table: "Landlords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Landlords_LegacySourceId",
                schema: "leasing",
                table: "Landlords",
                column: "LegacySourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Landlords_LegacySourceId",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "LegacySourceId",
                schema: "leasing",
                table: "Landlords");
        }
    }
}
