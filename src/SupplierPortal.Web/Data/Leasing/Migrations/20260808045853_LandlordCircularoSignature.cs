using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class LandlordCircularoSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CircularoDocumentId",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CircularoSigningUrl",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureSentAt",
                schema: "leasing",
                table: "Landlords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureSentTo",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureSignedAt",
                schema: "leasing",
                table: "Landlords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureStatus",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CircularoDocumentId",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "CircularoSigningUrl",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "SignatureSentAt",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "SignatureSentTo",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "SignatureSignedAt",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "SignatureStatus",
                schema: "leasing",
                table: "Landlords");
        }
    }
}
