using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessUnitDomainAndUserCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyBusinessUnitId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "BusinessUnits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyBusinessUnitId",
                table: "Users",
                column: "CompanyBusinessUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BusinessUnits_CompanyBusinessUnitId",
                table: "Users",
                column: "CompanyBusinessUnitId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_BusinessUnits_CompanyBusinessUnitId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyBusinessUnitId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyBusinessUnitId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Domain",
                table: "BusinessUnits");
        }
    }
}
