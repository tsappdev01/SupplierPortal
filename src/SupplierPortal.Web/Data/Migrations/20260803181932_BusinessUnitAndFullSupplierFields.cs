using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class BusinessUnitAndFullSupplierFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Companies_CompanyId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_BuNameId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Companies_CompanyId",
                table: "Users");

            // Preserve existing rows: rename the table instead of drop+recreate.
            migrationBuilder.RenameTable(
                name: "Companies",
                newName: "BusinessUnits");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "BusinessUnits",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Users",
                newName: "BusinessUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_CompanyId",
                table: "Users",
                newName: "IX_Users_BusinessUnitId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Suppliers",
                newName: "BusinessUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_Suppliers_CompanyId",
                table: "Suppliers",
                newName: "IX_Suppliers_BusinessUnitId");

            // BuNameId pointed at LookupValues (the old generic "Business Unit" lookup); the new
            // ProcurementBuId points at BusinessUnits instead, so the old values are not reusable.
            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BuNameId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BuNameId",
                table: "Suppliers");

            migrationBuilder.AddColumn<int>(
                name: "AccountCountryCodeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountCurrencyCodeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccountStartDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdministrativeContact",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowInternationalPayments",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BillToBuId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessRelationshipId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientBuId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunicationEmail",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommunicationMethodId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorporateWebSite",
                table: "Suppliers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DunsNumber",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOneTimeSupplier",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOrdering",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPay",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryPay",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPurchasing",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRfqOrBidding",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSourcingOnly",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodCode",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneAreaCode",
                table: "Suppliers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhoneCountryCodeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementBuId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemitAdviceEmail",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemittanceEmail",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierSite",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwiftCode",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeLicenseNumber",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_AccountCountryCodeId",
                table: "Suppliers",
                column: "AccountCountryCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_AccountCurrencyCodeId",
                table: "Suppliers",
                column: "AccountCurrencyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BillToBuId",
                table: "Suppliers",
                column: "BillToBuId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BusinessRelationshipId",
                table: "Suppliers",
                column: "BusinessRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_ClientBuId",
                table: "Suppliers",
                column: "ClientBuId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CommunicationMethodId",
                table: "Suppliers",
                column: "CommunicationMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PhoneCountryCodeId",
                table: "Suppliers",
                column: "PhoneCountryCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_ProcurementBuId",
                table: "Suppliers",
                column: "ProcurementBuId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessUnits_Code",
                table: "BusinessUnits",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_BusinessUnits_BillToBuId",
                table: "Suppliers",
                column: "BillToBuId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_BusinessUnits_BusinessUnitId",
                table: "Suppliers",
                column: "BusinessUnitId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_BusinessUnits_ClientBuId",
                table: "Suppliers",
                column: "ClientBuId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_BusinessUnits_ProcurementBuId",
                table: "Suppliers",
                column: "ProcurementBuId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_AccountCountryCodeId",
                table: "Suppliers",
                column: "AccountCountryCodeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_AccountCurrencyCodeId",
                table: "Suppliers",
                column: "AccountCurrencyCodeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessRelationshipId",
                table: "Suppliers",
                column: "BusinessRelationshipId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_CommunicationMethodId",
                table: "Suppliers",
                column: "CommunicationMethodId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_PhoneCountryCodeId",
                table: "Suppliers",
                column: "PhoneCountryCodeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BusinessUnits_BusinessUnitId",
                table: "Users",
                column: "BusinessUnitId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_BusinessUnits_BillToBuId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_BusinessUnits_BusinessUnitId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_BusinessUnits_ClientBuId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_BusinessUnits_ProcurementBuId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_AccountCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_AccountCurrencyCodeId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_BusinessRelationshipId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_CommunicationMethodId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_PhoneCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_BusinessUnits_BusinessUnitId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_AccountCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_AccountCurrencyCodeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BillToBuId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BusinessRelationshipId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_ClientBuId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CommunicationMethodId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_PhoneCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_ProcurementBuId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_BusinessUnits_Code",
                table: "BusinessUnits");

            migrationBuilder.DropColumn(
                name: "ProcurementBuId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AccountCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AccountCurrencyCodeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AccountStartDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AddressName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AdministrativeContact",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AllowInternationalPayments",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BillToBuId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BusinessRelationshipId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ClientBuId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CommunicationEmail",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CommunicationMethodId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CorporateWebSite",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DunsNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsOneTimeSupplier",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsOrdering",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsPay",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsPrimaryPay",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsPurchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsRfqOrBidding",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsSourcingOnly",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PaymentMethodCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PhoneAreaCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PhoneCountryCodeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "RemitAdviceEmail",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "RemittanceEmail",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SupplierSite",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SwiftCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TradeLicenseNumber",
                table: "Suppliers");

            migrationBuilder.RenameColumn(
                name: "BusinessUnitId",
                table: "Users",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_BusinessUnitId",
                table: "Users",
                newName: "IX_Users_CompanyId");

            migrationBuilder.AddColumn<int>(
                name: "BuNameId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "BusinessUnitId",
                table: "Suppliers",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_Suppliers_BusinessUnitId",
                table: "Suppliers",
                newName: "IX_Suppliers_CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BuNameId",
                table: "Suppliers",
                column: "BuNameId");

            // Reverse of the Up() rename: drop the Code column and rename the table back.
            migrationBuilder.DropColumn(
                name: "Code",
                table: "BusinessUnits");

            migrationBuilder.RenameTable(
                name: "BusinessUnits",
                newName: "Companies");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Companies_CompanyId",
                table: "Suppliers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_BuNameId",
                table: "Suppliers",
                column: "BuNameId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Companies_CompanyId",
                table: "Users",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
