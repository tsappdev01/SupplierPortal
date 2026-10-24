using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeLicenseAutoReadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TlActivities",
                table: "Suppliers",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlAddress",
                table: "Suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlCapitalCurrency",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlDcciNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlDunsNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlEmail",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TlExpiryDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TlIssueDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlIssuingAuthority",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlLegalName",
                table: "Suppliers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlLegalType",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlLicenseCategory",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlLicenseNumber",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlLicenseType",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlMainLicenseNumber",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlMobile",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TlPaidCapital",
                table: "Suppliers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlParcelId",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlPhone",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlPoBox",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlPremises",
                table: "Suppliers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TlReadAt",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlReadSource",
                table: "Suppliers",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlRegisterNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TlTradeName",
                table: "Suppliers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TlActivities",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlAddress",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlCapitalCurrency",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlDcciNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlDunsNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlEmail",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlExpiryDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlIssueDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlIssuingAuthority",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlLegalName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlLegalType",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlLicenseCategory",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlLicenseNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlLicenseType",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlMainLicenseNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlMobile",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlPaidCapital",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlParcelId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlPhone",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlPoBox",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlPremises",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlReadAt",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlReadSource",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlRegisterNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TlTradeName",
                table: "Suppliers");
        }
    }
}
