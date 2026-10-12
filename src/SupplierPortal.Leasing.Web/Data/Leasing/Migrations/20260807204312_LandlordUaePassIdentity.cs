using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class LandlordUaePassIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UaePassEmail",
                schema: "leasing",
                table: "LandlordAccessTokens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UaePassEmiratesId",
                schema: "leasing",
                table: "LandlordAccessTokens",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UaePassFullName",
                schema: "leasing",
                table: "LandlordAccessTokens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UaePassMobile",
                schema: "leasing",
                table: "LandlordAccessTokens",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UaePassState",
                schema: "leasing",
                table: "LandlordAccessTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UaePassSub",
                schema: "leasing",
                table: "LandlordAccessTokens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UaePassVerifiedAt",
                schema: "leasing",
                table: "LandlordAccessTokens",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UaePassEmail",
                schema: "leasing",
                table: "LandlordAccessTokens");

            migrationBuilder.DropColumn(
                name: "UaePassEmiratesId",
                schema: "leasing",
                table: "LandlordAccessTokens");

            migrationBuilder.DropColumn(
                name: "UaePassFullName",
                schema: "leasing",
                table: "LandlordAccessTokens");

            migrationBuilder.DropColumn(
                name: "UaePassMobile",
                schema: "leasing",
                table: "LandlordAccessTokens");

            migrationBuilder.DropColumn(
                name: "UaePassState",
                schema: "leasing",
                table: "LandlordAccessTokens");

            migrationBuilder.DropColumn(
                name: "UaePassSub",
                schema: "leasing",
                table: "LandlordAccessTokens");

            migrationBuilder.DropColumn(
                name: "UaePassVerifiedAt",
                schema: "leasing",
                table: "LandlordAccessTokens");
        }
    }
}
