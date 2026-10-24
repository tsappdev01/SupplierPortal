using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityDocumentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmiratesIdExpiryDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmiratesIdNumber",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdDocReadAt",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdDocReadSource",
                table: "Suppliers",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PassportExpiryDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumber",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmiratesIdExpiryDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "EmiratesIdNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IdDocReadAt",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IdDocReadSource",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PassportExpiryDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                table: "Suppliers");
        }
    }
}
