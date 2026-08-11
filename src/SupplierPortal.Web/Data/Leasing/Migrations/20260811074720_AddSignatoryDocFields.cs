using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddSignatoryDocFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                schema: "leasing",
                table: "LandlordDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignatoryDocStatus",
                schema: "leasing",
                table: "LandlordDocuments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                schema: "leasing",
                table: "LandlordDocuments");

            migrationBuilder.DropColumn(
                name: "SignatoryDocStatus",
                schema: "leasing",
                table: "LandlordDocuments");
        }
    }
}
