using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the "is the address the same as the Trade License / VAT Certificate?" answer and,
    /// when it is not, the alternate address exactly as it appears on the VAT Certificate.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260914000000_AddSupplierVatCertificateAddress")]
    public partial class AddSupplierVatCertificateAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AddressSameAsTradeLicense",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatCertificateAddress",
                table: "Suppliers",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressSameAsTradeLicense",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "VatCertificateAddress",
                table: "Suppliers");
        }
    }
}
