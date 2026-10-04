using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierMobileAndAddressPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AddressPurposeOrdering",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AddressPurposeRemitTo",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AddressPurposeRfqBidding",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobileAreaCode",
                table: "Suppliers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MobileCountryCodeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_MobileCountryCodeId",
                table: "Suppliers",
                column: "MobileCountryCodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_MobileCountryCodeId",
                table: "Suppliers",
                column: "MobileCountryCodeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_MobileCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_MobileCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AddressPurposeOrdering",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AddressPurposeRemitTo",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AddressPurposeRfqBidding",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Mobile",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "MobileAreaCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "MobileCountryCodeId",
                table: "Suppliers");
        }
    }
}
