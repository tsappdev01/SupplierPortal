using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierKysFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_AssignedToUserId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Departments_DepartmentId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_AssignedToUserId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccessTokens_TokenHash",
                table: "CustomerAccessTokens");

            migrationBuilder.AddColumn<bool>(
                name: "KysAddressChangedTwice",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysAdverseMedia",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysAdverseMediaDetails",
                table: "Suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysAvailableToMeet",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysBankAuthorisedUae",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysCommercialReason",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysCoveringActivity",
                table: "Suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysExpectedVolumeValue",
                table: "Suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysFtaPenalty",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysFtaPenaltyDetails",
                table: "Suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysGoodsDescription",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysGoodsGenuine",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysGoodsGenuineProof",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysGoodsOrigin",
                table: "Suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KysIdExpiryDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysIdNumber",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysLicensedActivities",
                table: "Suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysOwnGoodsAtSupply",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysPaymentToOtherParty",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysPaymentsElectronicOnly",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysPricingBasis",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KysRepPosition",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KysRepsChangedTwice",
                table: "Suppliers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_AssignedToUserId",
                table: "Customers",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Departments_DepartmentId",
                table: "Suppliers",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_AssignedToUserId",
                table: "Suppliers",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_AssignedToUserId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Departments_DepartmentId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_AssignedToUserId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KysAddressChangedTwice",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysAdverseMedia",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysAdverseMediaDetails",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysAvailableToMeet",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysBankAuthorisedUae",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysCommercialReason",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysCoveringActivity",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysExpectedVolumeValue",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysFtaPenalty",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysFtaPenaltyDetails",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysGoodsDescription",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysGoodsGenuine",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysGoodsGenuineProof",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysGoodsOrigin",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysIdExpiryDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysIdNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysLicensedActivities",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysOwnGoodsAtSupply",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysPaymentToOtherParty",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysPaymentsElectronicOnly",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysPricingBasis",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysRepPosition",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "KysRepsChangedTwice",
                table: "Suppliers");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccessTokens_TokenHash",
                table: "CustomerAccessTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_AssignedToUserId",
                table: "Customers",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Departments_DepartmentId",
                table: "Suppliers",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_AssignedToUserId",
                table: "Suppliers",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
