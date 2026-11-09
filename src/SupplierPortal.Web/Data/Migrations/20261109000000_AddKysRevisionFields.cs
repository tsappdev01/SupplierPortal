using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SupplierPortal.Web.Data.ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261109000000_AddKysRevisionFields")]
    public partial class AddKysRevisionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PeppolId",
                table: "Suppliers",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ApprovedServiceProviderId",
                table: "Suppliers",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "KysOperatingAddressDifferent",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysActualOperatingAddress",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAddressChangeReason",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysRepsChangeReason",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAccountHolderName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysBankName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysBankBranch",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "KysBankCountryId",
                table: "Suppliers",
                type: "int",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAccountNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysIban",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysSwiftBic",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "KysAccountCurrencyId",
                table: "Suppliers",
                type: "int",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAltAccountHolderName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAltBankName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAltBankBranch",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "KysAltBankCountryId",
                table: "Suppliers",
                type: "int",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAltAccountNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAltIban",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysAltSwiftBic",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "KysAltAccountCurrencyId",
                table: "Suppliers",
                type: "int",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysElectronicPaymentDetails",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysPricingBasisType",
                table: "Suppliers",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysPricingBasisOther",
                table: "Suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysLegalOwnerDetails",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysSupportingDocs",
                table: "Suppliers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "KysSupportingDocsOther",
                table: "Suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "KysGoodsOriginCountryId",
                table: "Suppliers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeppolId",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "ApprovedServiceProviderId",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysOperatingAddressDifferent",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysActualOperatingAddress",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAddressChangeReason",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysRepsChangeReason",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAccountHolderName",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysBankName",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysBankBranch",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysBankCountryId",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAccountNumber",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysIban",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysSwiftBic",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAccountCurrencyId",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltAccountHolderName",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltBankName",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltBankBranch",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltBankCountryId",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltAccountNumber",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltIban",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltSwiftBic",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysAltAccountCurrencyId",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysElectronicPaymentDetails",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysPricingBasisType",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysPricingBasisOther",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysLegalOwnerDetails",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysSupportingDocs",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysSupportingDocsOther",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "KysGoodsOriginCountryId",
                table: "Suppliers");
        }
    }
}
