using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260821000000_AddSupplierDisclaimerAcknowledgement")]
    public partial class AddSupplierDisclaimerAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisclaimerAcknowledged",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisclaimerAcknowledgedAt",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisclaimerAcknowledgedBy",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisclaimerText",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisclaimerAcknowledged",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DisclaimerAcknowledgedAt",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DisclaimerAcknowledgedBy",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DisclaimerText",
                table: "Suppliers");
        }
    }
}
