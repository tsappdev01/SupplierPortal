using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Replaces the single free-text VatCertificateAddress with a full structured address block
    /// (line 1/2, city/emirate, country, PO box) captured when the supplier's address differs from the
    /// Trade License / VAT Certificate address.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260915000000_SupplierVatCertificateAddressFields")]
    public partial class SupplierVatCertificateAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatCertificateAddress",
                table: "Suppliers");

            migrationBuilder.AddColumn<string>(
                name: "VatAddressLine1", table: "Suppliers", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "VatAddressLine2", table: "Suppliers", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "VatCityId", table: "Suppliers", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "VatCityText", table: "Suppliers", type: "nvarchar(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "VatCountryId", table: "Suppliers", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "VatPostalCode", table: "Suppliers", type: "nvarchar(20)", maxLength: 20, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VatAddressLine1", table: "Suppliers");
            migrationBuilder.DropColumn(name: "VatAddressLine2", table: "Suppliers");
            migrationBuilder.DropColumn(name: "VatCityId", table: "Suppliers");
            migrationBuilder.DropColumn(name: "VatCityText", table: "Suppliers");
            migrationBuilder.DropColumn(name: "VatCountryId", table: "Suppliers");
            migrationBuilder.DropColumn(name: "VatPostalCode", table: "Suppliers");

            migrationBuilder.AddColumn<string>(
                name: "VatCertificateAddress", table: "Suppliers", type: "nvarchar(600)", maxLength: 600, nullable: true);
        }
    }
}
