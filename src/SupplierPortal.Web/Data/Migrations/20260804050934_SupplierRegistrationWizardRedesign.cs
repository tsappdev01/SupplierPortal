using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierRegistrationWizardRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SupplierSite",
                table: "Suppliers");

            migrationBuilder.AlterColumn<string>(
                name: "AddressLine1",
                table: "Suppliers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SupplierSiteBuId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CityId",
                table: "Suppliers",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierSiteBuId",
                table: "Suppliers",
                column: "SupplierSiteBuId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_BusinessUnits_SupplierSiteBuId",
                table: "Suppliers",
                column: "SupplierSiteBuId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_CityId",
                table: "Suppliers",
                column: "CityId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_BusinessUnits_SupplierSiteBuId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_CityId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CityId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_SupplierSiteBuId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SupplierSiteBuId",
                table: "Suppliers");

            migrationBuilder.AlterColumn<string>(
                name: "AddressLine1",
                table: "Suppliers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupplierSite",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
