using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedSupplierFieldsAndLovs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessLocationTypeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessPartnerTypeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EInvoicingWaveId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IntermediaryAccountApplicable",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryAccountNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryBankName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryBicSwiftCode",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryBranchNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntermediaryCountryId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryIban",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuingBodyIfOthers",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfVatGroupEntities",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierTin",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TradeLicenseExpiryDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TradeLicenseIssueDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TradeLicenseIssuingBodyId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatGroupEntityNames",
                table: "Suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VatRegistered",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VatRegistrationTypeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BusinessLocationTypeId",
                table: "Suppliers",
                column: "BusinessLocationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BusinessPartnerTypeId",
                table: "Suppliers",
                column: "BusinessPartnerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_EInvoicingWaveId",
                table: "Suppliers",
                column: "EInvoicingWaveId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_IntermediaryCountryId",
                table: "Suppliers",
                column: "IntermediaryCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TradeLicenseIssuingBodyId",
                table: "Suppliers",
                column: "TradeLicenseIssuingBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_VatRegistrationTypeId",
                table: "Suppliers",
                column: "VatRegistrationTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessLocationTypeId",
                table: "Suppliers",
                column: "BusinessLocationTypeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessPartnerTypeId",
                table: "Suppliers",
                column: "BusinessPartnerTypeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_EInvoicingWaveId",
                table: "Suppliers",
                column: "EInvoicingWaveId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_IntermediaryCountryId",
                table: "Suppliers",
                column: "IntermediaryCountryId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_TradeLicenseIssuingBodyId",
                table: "Suppliers",
                column: "TradeLicenseIssuingBodyId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_VatRegistrationTypeId",
                table: "Suppliers",
                column: "VatRegistrationTypeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessLocationTypeId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessPartnerTypeId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_EInvoicingWaveId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_IntermediaryCountryId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_TradeLicenseIssuingBodyId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_VatRegistrationTypeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BusinessLocationTypeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BusinessPartnerTypeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_EInvoicingWaveId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_IntermediaryCountryId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TradeLicenseIssuingBodyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_VatRegistrationTypeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BusinessLocationTypeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTypeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "EInvoicingWaveId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IntermediaryAccountApplicable",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IntermediaryAccountNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IntermediaryBankName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IntermediaryBicSwiftCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IntermediaryBranchNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IntermediaryCountryId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IntermediaryIban",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IssuingBodyIfOthers",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "NumberOfVatGroupEntities",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SupplierTin",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TradeLicenseExpiryDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TradeLicenseIssueDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TradeLicenseIssuingBodyId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "VatGroupEntityNames",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "VatRegistered",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "VatRegistrationTypeId",
                table: "Suppliers");
        }
    }
}
