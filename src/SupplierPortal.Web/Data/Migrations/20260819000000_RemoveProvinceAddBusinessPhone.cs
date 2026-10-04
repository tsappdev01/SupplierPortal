using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260819000000_RemoveProvinceAddBusinessPhone")]
    public partial class RemoveProvinceAddBusinessPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the supplier Province field.
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_ProvinceId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_ProvinceId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "Suppliers");

            // Add the business-level contact phone (Address tab).
            migrationBuilder.AddColumn<string>(
                name: "BusinessPhone",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPhoneAreaCode",
                table: "Suppliers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessPhoneCountryCodeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BusinessPhoneCountryCodeId",
                table: "Suppliers",
                column: "BusinessPhoneCountryCodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessPhoneCountryCodeId",
                table: "Suppliers",
                column: "BusinessPhoneCountryCodeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessPhoneCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BusinessPhoneCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(name: "BusinessPhone", table: "Suppliers");
            migrationBuilder.DropColumn(name: "BusinessPhoneAreaCode", table: "Suppliers");
            migrationBuilder.DropColumn(name: "BusinessPhoneCountryCodeId", table: "Suppliers");

            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_ProvinceId",
                table: "Suppliers",
                column: "ProvinceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_ProvinceId",
                table: "Suppliers",
                column: "ProvinceId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
