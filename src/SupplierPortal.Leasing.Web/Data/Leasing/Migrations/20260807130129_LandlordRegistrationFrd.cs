using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class LandlordRegistrationFrd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionNote",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "PrimaryContactName",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "SecondaryContactMobile",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "SecondaryContactName",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "TaxRegistrationNo",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "TradeLicenseNo",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.RenameColumn(
                name: "SecondaryContactEmail",
                schema: "leasing",
                table: "Landlords",
                newName: "CompanyName");

            migrationBuilder.RenameColumn(
                name: "EmiratesId",
                schema: "leasing",
                table: "Landlords",
                newName: "LandLordCode");

            migrationBuilder.AddColumn<string>(
                name: "Building",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                schema: "leasing",
                table: "Landlords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedBy",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LinkActive",
                schema: "leasing",
                table: "Landlords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PropertyCode",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                schema: "leasing",
                table: "Landlords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                schema: "leasing",
                table: "Landlords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestartedFromId",
                schema: "leasing",
                table: "Landlords",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Detail",
                schema: "leasing",
                table: "LandlordEvents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ByType",
                schema: "leasing",
                table: "LandlordEvents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ip",
                schema: "leasing",
                table: "LandlordEvents",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewStatus",
                schema: "leasing",
                table: "LandlordEvents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldStatus",
                schema: "leasing",
                table: "LandlordEvents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContactId",
                schema: "leasing",
                table: "LandlordDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                schema: "leasing",
                table: "LandlordDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LandlordContactId",
                schema: "leasing",
                table: "LandlordDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "leasing",
                table: "LandlordDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LandlordComments",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    CommentType = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordComments_Landlords_LandlordId",
                        column: x => x.LandlordId,
                        principalSchema: "leasing",
                        principalTable: "Landlords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandlordContacts",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ContactType = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmiratesId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordContacts_Landlords_LandlordId",
                        column: x => x.LandlordId,
                        principalSchema: "leasing",
                        principalTable: "Landlords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandlordDocuments_LandlordContactId",
                schema: "leasing",
                table: "LandlordDocuments",
                column: "LandlordContactId");

            migrationBuilder.CreateIndex(
                name: "IX_LandlordComments_LandlordId_IsResolved",
                schema: "leasing",
                table: "LandlordComments",
                columns: new[] { "LandlordId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_LandlordContacts_LandlordId",
                schema: "leasing",
                table: "LandlordContacts",
                column: "LandlordId");

            migrationBuilder.AddForeignKey(
                name: "FK_LandlordDocuments_LandlordContacts_LandlordContactId",
                schema: "leasing",
                table: "LandlordDocuments",
                column: "LandlordContactId",
                principalSchema: "leasing",
                principalTable: "LandlordContacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LandlordDocuments_LandlordContacts_LandlordContactId",
                schema: "leasing",
                table: "LandlordDocuments");

            migrationBuilder.DropTable(
                name: "LandlordComments",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "LandlordContacts",
                schema: "leasing");

            migrationBuilder.DropIndex(
                name: "IX_LandlordDocuments_LandlordContactId",
                schema: "leasing",
                table: "LandlordDocuments");

            migrationBuilder.DropColumn(
                name: "Building",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "LinkActive",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "PropertyCode",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "RestartedFromId",
                schema: "leasing",
                table: "Landlords");

            migrationBuilder.DropColumn(
                name: "ByType",
                schema: "leasing",
                table: "LandlordEvents");

            migrationBuilder.DropColumn(
                name: "Ip",
                schema: "leasing",
                table: "LandlordEvents");

            migrationBuilder.DropColumn(
                name: "NewStatus",
                schema: "leasing",
                table: "LandlordEvents");

            migrationBuilder.DropColumn(
                name: "OldStatus",
                schema: "leasing",
                table: "LandlordEvents");

            migrationBuilder.DropColumn(
                name: "ContactId",
                schema: "leasing",
                table: "LandlordDocuments");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                schema: "leasing",
                table: "LandlordDocuments");

            migrationBuilder.DropColumn(
                name: "LandlordContactId",
                schema: "leasing",
                table: "LandlordDocuments");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "leasing",
                table: "LandlordDocuments");

            migrationBuilder.RenameColumn(
                name: "LandLordCode",
                schema: "leasing",
                table: "Landlords",
                newName: "EmiratesId");

            migrationBuilder.RenameColumn(
                name: "CompanyName",
                schema: "leasing",
                table: "Landlords",
                newName: "SecondaryContactEmail");

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryContactName",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryContactMobile",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryContactName",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxRegistrationNo",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeLicenseNo",
                schema: "leasing",
                table: "Landlords",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Detail",
                schema: "leasing",
                table: "LandlordEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
